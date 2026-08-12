using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Notifications;
using AuditX.Application.Notifications.Commands;
using AuditX.Application.Notifications.Services;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using AuditX.Domain.Notifications;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AuditX.Application.Tests.Notifications;

public sealed class NotificationIngestServiceTests
{
    private readonly INotificationRuleRepository _rules = Substitute.For<INotificationRuleRepository>();
    private readonly INotificationTemplateRepository _templates = Substitute.For<INotificationTemplateRepository>();
    private readonly INotificationDispatchRepository _dispatches = Substitute.For<INotificationDispatchRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ITemplateRenderer _renderer = Substitute.For<ITemplateRenderer>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly ISmsSender _sms = Substitute.For<ISmsSender>();
    private readonly ITeamsSender _teams = Substitute.For<ITeamsSender>();
    private readonly IAuditRecorder _audit = Substitute.For<IAuditRecorder>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private static readonly User Recipient =
        User.ProvisionFromDirectory("jdoe", "jdoe@bank.local", "S-1-5-21-99", "jdoe@bank.local", "John", "Doe");

    private NotificationIngestService Build()
    {
        _clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        _users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Recipient);
        _renderer.Render(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>())
            .Returns(("Subject", "Body"));
        _templates.ResolveAsync(Arg.Any<string>(), Arg.Any<NotificationChannel>(), Arg.Any<CancellationToken>())
            .Returns(NotificationTemplate.Create("exception_raised", NotificationChannel.Email, TemplateScope.System, "S", "B"));
        _dispatches.ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<NotificationChannel>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _dispatches.TryClaimAsync(Arg.Any<NotificationDispatch>(), Arg.Any<CancellationToken>()).Returns(true);
        _email.SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.Sent("msg-1"));
        _teams.SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.Sent("teams-1"));
        return new NotificationIngestService(_rules, _templates, _dispatches, _users, _renderer, _email, _sms, _teams, _audit, _clock, _uow,
            Substitute.For<ILogger<NotificationIngestService>>());
    }

    private static NotificationRule Rule(string channelsJson) => NotificationRule.Create(
        "exception_raised", "rule", "{\"type\":\"payload_derived\",\"value\":\"OwnerUserId\"}", channelsJson, "exception_raised",
        isActive: true, isSystemDefault: true);

    private static DomainEventEnvelope Envelope(string severity) => new(
        "exception_raised", Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, null,
        $"{{\"OwnerUserId\":\"{Guid.NewGuid()}\",\"Severity\":\"{severity}\"}}");

    [Fact]
    public async Task ProcessAsync_claims_and_sends_email_for_a_matching_active_rule()
    {
        var svc = Build();
        _rules.GetActiveByEventTypeAsync("exception_raised", Arg.Any<CancellationToken>()).Returns([Rule("[\"email\"]")]);

        await svc.ProcessAsync(Envelope("High"));

        await _dispatches.Received(1).TryClaimAsync(Arg.Any<NotificationDispatch>(), Arg.Any<CancellationToken>());
        await _email.Received(1).SendAsync(Recipient.Email!, "Subject", "Body", Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_skips_when_an_identical_dispatch_already_exists()
    {
        var svc = Build();
        _dispatches.ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<NotificationChannel>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _rules.GetActiveByEventTypeAsync("exception_raised", Arg.Any<CancellationToken>()).Returns([Rule("[\"email\"]")]);

        await svc.ProcessAsync(Envelope("High"));

        await _dispatches.DidNotReceive().TryClaimAsync(Arg.Any<NotificationDispatch>(), Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_does_not_send_when_claim_loses_the_idempotency_race()
    {
        var svc = Build();
        _dispatches.TryClaimAsync(Arg.Any<NotificationDispatch>(), Arg.Any<CancellationToken>()).Returns(false);
        _rules.GetActiveByEventTypeAsync("exception_raised", Arg.Any<CancellationToken>()).Returns([Rule("[\"email\"]")]);

        await svc.ProcessAsync(Envelope("High"));

        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Critical_severity_forces_an_email_even_for_an_sms_only_rule()
    {
        var svc = Build();
        _rules.GetActiveByEventTypeAsync("exception_raised", Arg.Any<CancellationToken>()).Returns([Rule("[\"sms\"]")]);

        await svc.ProcessAsync(Envelope("Critical"));

        // SMS is dormant (no phone), but Critical guarantees a deliverable channel: the email still goes out.
        await _email.Received(1).SendAsync(Recipient.Email!, "Subject", "Body", Arg.Any<CancellationToken>());
        await _sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Teams_channel_posts_once_per_rule_as_a_broadcast_not_per_recipient()
    {
        var svc = Build();
        _teams.IsConfigured(Arg.Any<string?>()).Returns(true);
        // Two recipients so a per-recipient fan-out would post to Teams twice; the broadcast must post exactly once.
        var other = User.ProvisionFromDirectory("asmith", "asmith@bank.local", "S-1-5-21-100", "asmith@bank.local", "Ann", "Smith");
        _users.GetActiveByRoleNameAsync("Auditee", Arg.Any<CancellationToken>()).Returns([Recipient, other]);
        var roleRule = NotificationRule.Create(
            "exception_raised", "rule", "{\"type\":\"role\",\"value\":\"Auditee\"}", "[\"email\",\"teams\"]", "exception_raised", true, true);
        _rules.GetActiveByEventTypeAsync("exception_raised", Arg.Any<CancellationToken>()).Returns([roleRule]);

        await svc.ProcessAsync(Envelope("High"));

        // One Teams post to the channel reference (never the URL); both recipients still get their own email.
        await _teams.Received(1).SendAsync(ITeamsSender.DefaultChannelRef, Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _email.Received(2).SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Teams_channel_is_skipped_when_no_webhook_is_configured()
    {
        var svc = Build();
        _teams.IsConfigured(Arg.Any<string?>()).Returns(false);
        _rules.GetActiveByEventTypeAsync("exception_raised", Arg.Any<CancellationToken>()).Returns([Rule("[\"email\",\"teams\"]")]);

        await svc.ProcessAsync(Envelope("High"));

        await _teams.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _email.Received(1).SendAsync(Recipient.Email!, "Subject", "Body", Arg.Any<CancellationToken>()); // email still sent
    }

    [Fact]
    public async Task Invalid_recipient_json_on_a_rule_is_isolated_and_does_not_throw()
    {
        var svc = Build();
        var badRule = NotificationRule.Create("exception_raised", "bad", "not-json", "[\"email\"]", "exception_raised", true, true);
        _rules.GetActiveByEventTypeAsync("exception_raised", Arg.Any<CancellationToken>()).Returns([badRule]);

        await svc.ProcessAsync(Envelope("High")); // must not throw — the per-rule guard logs and continues.

        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewAsync_resolves_recipients_and_renders_without_sending()
    {
        var svc = Build();

        var preview = await svc.PreviewAsync(
            "{\"type\":\"payload_derived\",\"value\":\"OwnerUserId\"}", "exception_raised",
            $"{{\"OwnerUserId\":\"{Guid.NewGuid()}\"}}");

        Assert.Contains(Recipient.Email!, preview.ResolvedRecipientAddresses);
        Assert.Equal("Body", preview.RenderedBody);
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

public sealed class RetryDispatchCommandHandlerTests
{
    [Fact]
    public async Task Retrying_an_already_delivered_dispatch_is_rejected()
    {
        var dispatch = NotificationDispatch.Create(
            Guid.NewGuid(), "exception_raised", Guid.NewGuid(), Guid.NewGuid(), "a@b.com",
            NotificationChannel.Email, "k", 1, "s", "b", "High");
        dispatch.RecordDelivered(DateTimeOffset.UnixEpoch, "m", null);

        var dispatches = Substitute.For<INotificationDispatchRepository>();
        dispatches.GetByIdAsync(dispatch.Id, Arg.Any<CancellationToken>()).Returns(dispatch);
        var email = Substitute.For<IEmailSender>();

        var handler = new RetryDispatchCommandHandler(
            dispatches, email, Substitute.For<ISmsSender>(), Substitute.For<ITeamsSender>(), Substitute.For<IAuditRecorder>(),
            Substitute.For<IClock>(), Substitute.For<IUnitOfWork>());

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new RetryDispatchCommand(dispatch.Id), CancellationToken.None));

        Assert.Equal("notification.not_retryable", ex.ErrorCode);
        await email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

public sealed class NotificationEventsTests
{
    [Theory]
    [InlineData("ExceptionRaisedEvent", "exception_raised")]
    [InlineData("AuditTeamMemberAddedEvent", "audit_team_member_added")]
    [InlineData("MapSubmittedEvent", "map_submitted")]
    [InlineData("ExceptionOwnerReassignedEvent", "exception_owner_reassigned")]
    public void Derive_converts_event_type_names_to_snake_case_keys(string typeName, string expected)
        => Assert.Equal(expected, NotificationEvents.Derive(typeName));
}
