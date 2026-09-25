using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Organization;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace AuditX.Application.Notifications.Services;

/// <summary>
/// The M10 notification pipeline: given a raised domain event, load the active rules for its type, resolve
/// recipients, render the template per channel, create a <see cref="NotificationDispatch"/> and attempt to
/// send it (recording the outcome). Resilient per-rule/per-recipient so one bad rule never sinks the batch.
/// </summary>
public sealed class NotificationIngestService(
    INotificationRuleRepository rules,
    INotificationTemplateRepository templates,
    INotificationDispatchRepository dispatches,
    IUserRepository users,
    IReportingLineResolver reportingLine,
    ITemplateRenderer renderer,
    IEmailSender emailSender,
    ISmsSender smsSender,
    ITeamsSender teamsSender,
    IAppUrlProvider appUrls,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork,
    ILogger<NotificationIngestService> logger)
{
    private sealed record Recipient(Guid UserId, string? Email);

    /// <summary>Resolve recipients + render a template against a sample payload WITHOUT sending (US-M10-010).</summary>
    public async Task<Dtos.RulePreviewDto> PreviewAsync(string recipientResolutionJson, string templateKey, string samplePayloadJson, CancellationToken cancellationToken = default)
    {
        using var doc = ParseOrThrow(samplePayloadJson, "notification.invalid_payload_json");
        var payload = doc.RootElement;
        var model = new Dictionary<string, object?>(BuildModel(payload), StringComparer.Ordinal) { ["AppBaseUrl"] = appUrls.WebBaseUrl };
        var recipients = await ResolveRecipientsAsync(recipientResolutionJson, payload, cancellationToken);

        var template = await templates.ResolveAsync(templateKey, NotificationChannel.Email, cancellationToken);
        var (subject, body) = template is null
            ? ($"AuditX notification: {templateKey}", $"(no template '{templateKey}' configured)")
            : renderer.Render(template.SubjectTemplate, template.BodyTemplate, model);

        var addresses = recipients.Where(r => !string.IsNullOrWhiteSpace(r.Email)).Select(r => r.Email!).ToArray();
        return new Dtos.RulePreviewDto(addresses, subject, body);
    }

    public async Task ProcessAsync(DomainEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var activeRules = await rules.GetActiveByEventTypeAsync(envelope.EventType, cancellationToken);
        if (activeRules.Count == 0)
        {
            return;
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(envelope.PayloadJson) ? "{}" : envelope.PayloadJson);
        var payload = doc.RootElement;
        var severity = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("Severity", out var sev) && sev.ValueKind == JsonValueKind.String
            ? sev.GetString()
            : null;
        var model = new Dictionary<string, object?>(BuildModel(payload), StringComparer.Ordinal) { ["AppBaseUrl"] = appUrls.WebBaseUrl };

        foreach (var rule in activeRules)
        {
            try
            {
                // Each dispatch self-commits (claim-then-send-then-record) so one bad rule never rolls back others.
                await ApplyRuleAsync(rule, envelope, payload, severity, model, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification rule {RuleId} failed for event {EventType} ({EventId})", rule.Id, envelope.EventType, envelope.EventId);
            }
        }
    }

    /// <summary>Re-attempt dispatches whose retry time has arrived (US-M10-005). Returns the number retried.</summary>
    public async Task<int> RetryDueAsync(CancellationToken cancellationToken = default)
    {
        var due = await dispatches.GetDueForRetryAsync(clock.UtcNow, max: 100, cancellationToken);
        var retried = 0;
        foreach (var dispatch in due)
        {
            try
            {
                var result = await ChannelDispatcher.SendAsync(dispatch, emailSender, smsSender, teamsSender, cancellationToken);

                if (result.Success)
                {
                    dispatch.RecordDelivered(clock.UtcNow, result.ProviderMessageId, result.ProviderResponse);
                }
                else
                {
                    dispatch.RecordFailure(clock.UtcNow, result.Error ?? "send failed", result.IsPermanentFailure);
                }

                audit.RecordAs(ActorType.System, "notifications", null, AuditEventTypes.NotificationRetried, AuditTargetTypes.NotificationDispatch, dispatch.Id,
                    after: new { dispatch.EventType, status = dispatch.Status.ToString(), dispatch.Attempts });

                // Persist per-dispatch so one failure (e.g. a concurrency conflict with a manual retry) cannot
                // discard the recorded outcomes of dispatches already re-sent in this sweep.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                retried++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification retry failed for dispatch {DispatchId}; stopping sweep, remaining items retry next run.", dispatch.Id);
                break;
            }
        }

        return retried;
    }

    private async Task ApplyRuleAsync(NotificationRule rule, DomainEventEnvelope envelope, JsonElement payload, string? severity, IReadOnlyDictionary<string, object?> model, CancellationToken cancellationToken)
    {
        var recipients = await ResolveRecipientsAsync(rule.RecipientResolutionJson, payload, cancellationToken);
        var channels = ParseChannels(rule.ChannelsJson, severity);

        // Teams is channel-scoped (a webhook posts to a team channel, not a person), so it fires ONCE per
        // (event, rule) — not once per recipient — and only when a webhook is configured. The null-recipient
        // row makes the unique idempotency index enforce that single post.
        if (channels.Contains(NotificationChannel.Teams) && teamsSender.IsConfigured())
        {
            await CreateAndSendTeamsBroadcastAsync(rule, envelope, model, severity, cancellationToken);
        }

        foreach (var recipient in recipients)
        {
            foreach (var channel in channels)
            {
                if (channel == NotificationChannel.Teams)
                {
                    continue; // handled once above as a broadcast, not per recipient.
                }

                if (channel == NotificationChannel.Sms)
                {
                    // SMS is dormant until the directory provides phone numbers; honour non-critical opt-out otherwise.
                    if (!await AllowsNonCriticalSmsAsync(recipient.UserId, severity, cancellationToken))
                    {
                        continue;
                    }

                    logger.LogDebug("SMS channel skipped for {UserId} (no phone number available)", recipient.UserId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(recipient.Email))
                {
                    continue;
                }

                // Cheap pre-check for the common duplicate case; the unique index + TryClaimAsync are the
                // authoritative backstop against concurrent ingest of the same event.
                if (await dispatches.ExistsAsync(envelope.EventId, rule.Id, recipient.UserId, channel, cancellationToken))
                {
                    continue;
                }

                await CreateAndSendAsync(rule, envelope, channel, recipient.UserId, recipient.Email!, severity, model, cancellationToken);
            }
        }
    }

    private async Task CreateAndSendAsync(NotificationRule rule, DomainEventEnvelope envelope, NotificationChannel channel, Guid recipientUserId, string address, string? severity, IReadOnlyDictionary<string, object?> model, CancellationToken cancellationToken)
    {
        var template = await templates.ResolveAsync(rule.TemplateKey, channel, cancellationToken);
        var (subject, body) = template is null
            ? ($"AuditX notification: {envelope.EventType}", $"Event '{envelope.EventType}' occurred.")
            : renderer.Render(template.SubjectTemplate, template.BodyTemplate, model);

        var dispatch = NotificationDispatch.Create(
            envelope.EventId, envelope.EventType, rule.Id, recipientUserId, address, channel,
            rule.TemplateKey, template?.Version ?? 0, subject, body, severity);

        // Persist the Pending dispatch as the idempotency claim BEFORE the irreversible send, so a job re-run
        // or a concurrent worker sees the committed row (via the unique index) and does not send a duplicate.
        if (!await dispatches.TryClaimAsync(dispatch, cancellationToken))
        {
            return; // another worker already claimed this (event, rule, recipient, channel).
        }

        var result = await ChannelDispatcher.SendAsync(dispatch, emailSender, smsSender, teamsSender, cancellationToken);

        if (result.Success)
        {
            dispatch.RecordDelivered(clock.UtcNow, result.ProviderMessageId, result.ProviderResponse);
        }
        else
        {
            dispatch.RecordFailure(clock.UtcNow, result.Error ?? "send failed", result.IsPermanentFailure);
        }

        audit.RecordAs(ActorType.System, "notifications", null, AuditEventTypes.NotificationDispatched, AuditTargetTypes.NotificationDispatch, dispatch.Id,
            after: new { dispatch.EventType, channel = channel.ToString(), status = dispatch.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Create + send a single Teams broadcast for a rule (no per-recipient fan-out). Renders the rule's Email
    /// template — bodies are channel-agnostic and no Teams-specific templates are seeded — and addresses the
    /// dispatch to the stable channel reference (never the secret webhook URL).
    /// </summary>
    private async Task CreateAndSendTeamsBroadcastAsync(NotificationRule rule, DomainEventEnvelope envelope, IReadOnlyDictionary<string, object?> model, string? severity, CancellationToken cancellationToken)
    {
        if (await dispatches.ExistsAsync(envelope.EventId, rule.Id, recipientUserId: null, NotificationChannel.Teams, cancellationToken))
        {
            return;
        }

        var template = await templates.ResolveAsync(rule.TemplateKey, NotificationChannel.Email, cancellationToken);
        var (subject, body) = template is null
            ? ($"AuditX notification: {envelope.EventType}", $"Event '{envelope.EventType}' occurred.")
            : renderer.Render(template.SubjectTemplate, template.BodyTemplate, model);

        var dispatch = NotificationDispatch.Create(
            envelope.EventId, envelope.EventType, rule.Id, recipientUserId: null, ITeamsSender.DefaultChannelRef,
            NotificationChannel.Teams, rule.TemplateKey, template?.Version ?? 0, subject, body, severity);

        if (!await dispatches.TryClaimAsync(dispatch, cancellationToken))
        {
            return; // another worker already posted this (event, rule) to Teams.
        }

        var result = await ChannelDispatcher.SendAsync(dispatch, emailSender, smsSender, teamsSender, cancellationToken);
        if (result.Success)
        {
            dispatch.RecordDelivered(clock.UtcNow, result.ProviderMessageId, result.ProviderResponse);
        }
        else
        {
            dispatch.RecordFailure(clock.UtcNow, result.Error ?? "send failed", result.IsPermanentFailure);
        }

        audit.RecordAs(ActorType.System, "notifications", null, AuditEventTypes.NotificationDispatched, AuditTargetTypes.NotificationDispatch, dispatch.Id,
            after: new { dispatch.EventType, channel = "Teams", status = dispatch.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Recipient>> ResolveRecipientsAsync(string recipientResolutionJson, JsonElement payload, CancellationToken cancellationToken)
    {
        using var doc = ParseOrThrow(recipientResolutionJson, "notification.invalid_recipient_json");
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
        var value = root.TryGetProperty("value", out var v) ? v.GetString() : null;

        switch (type)
        {
            case "role" when !string.IsNullOrWhiteSpace(value):
            {
                var found = await users.GetActiveByRoleNameAsync(value, cancellationToken);
                return found.Select(u => new Recipient(u.Id, u.Email)).ToArray();
            }

            case "named_users" when !string.IsNullOrWhiteSpace(value):
            {
                var ids = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null).Where(g => g is not null).Select(g => g!.Value).ToArray();
                var found = await users.GetByIdsAsync(ids, cancellationToken);
                return found.Select(u => new Recipient(u.Id, u.Email)).ToArray();
            }

            case "payload_derived" when !string.IsNullOrWhiteSpace(value):
            {
                if (TryGetPayloadUserId(payload, value, out var userId)
                    && await users.GetByIdAsync(userId, cancellationToken) is { } user)
                {
                    return [new Recipient(user.Id, user.Email)];
                }

                return [];
            }

            // The line manager of a payload user (value = the payload field holding the subject's user id).
            // Resolves the effective manager (explicit User.ManagerId, else the head of their org unit).
            case "line_manager" when !string.IsNullOrWhiteSpace(value):
            {
                if (TryGetPayloadUserId(payload, value, out var subjectId)
                    && await reportingLine.GetLineManagerAsync(subjectId, cancellationToken) is { } managerId
                    && await users.GetByIdAsync(managerId, cancellationToken) is { } manager)
                {
                    return [new Recipient(manager.Id, manager.Email)];
                }

                return [];
            }

            // Every manager up the reporting chain above a payload user (value = the payload field with the subject id).
            case "reporting_chain" when !string.IsNullOrWhiteSpace(value):
            {
                if (!TryGetPayloadUserId(payload, value, out var subjectId))
                {
                    return [];
                }

                var chain = await reportingLine.GetReportingChainAsync(subjectId, cancellationToken);
                if (chain.Count == 0)
                {
                    return [];
                }

                var managers = await users.GetByIdsAsync(chain, cancellationToken);
                return managers.Select(u => new Recipient(u.Id, u.Email)).ToArray();
            }

            default:
                return [];
        }
    }

    /// <summary>Reads a Guid user id from a string field of the event payload (as payload_derived recipients do).</summary>
    private static bool TryGetPayloadUserId(JsonElement payload, string field, out Guid userId)
    {
        userId = Guid.Empty;
        return payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(field, out var el)
            && el.ValueKind == JsonValueKind.String
            && Guid.TryParse(el.GetString(), out userId);
    }

    private async Task<bool> AllowsNonCriticalSmsAsync(Guid userId, string? severity, CancellationToken cancellationToken)
    {
        if (string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return true; // Critical SMS ignores opt-out.
        }

        // Read the M1 per-user preferences (User.NotificationPreferencesJson); default: non-critical SMS off.
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(user?.NotificationPreferencesJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(user.NotificationPreferencesJson);
            return doc.RootElement.TryGetProperty("sms_non_critical", out var flag) && flag.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyDictionary<string, object?> BuildModel(JsonElement payload)
    {
        var model = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return model;
        }

        foreach (var prop in payload.EnumerateObject())
        {
            model[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.GetRawText(),
            };
        }

        return model;
    }

    private static IReadOnlyList<NotificationChannel> ParseChannels(string channelsJson, string? severity)
    {
        var set = new HashSet<NotificationChannel>();
        try
        {
            using var doc = JsonDocument.Parse(channelsJson);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var name = el.GetString();
                if (string.Equals(name, "email", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(NotificationChannel.Email);
                }
                else if (string.Equals(name, "sms", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(NotificationChannel.Sms);
                }
                else if (string.Equals(name, "teams", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(NotificationChannel.Teams);
                }
                else if (string.Equals(name, "both", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(NotificationChannel.Email);
                    set.Add(NotificationChannel.Sms);
                }
            }
        }
        catch
        {
            set.Add(NotificationChannel.Email);
        }

        if (set.Count == 0)
        {
            set.Add(NotificationChannel.Email);
        }

        // Critical severity escalates to SMS in addition to email (US-M10-004). Email is forced too so a
        // Critical notification always has a deliverable channel even for an SMS-only rule (SMS is dormant).
        if (string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            set.Add(NotificationChannel.Email);
            set.Add(NotificationChannel.Sms);
        }

        return set.ToArray();
    }

    private static JsonDocument ParseOrThrow(string? json, string errorCode)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException)
        {
            throw new DomainException(errorCode, "The supplied value is not valid JSON.");
        }
    }
}
