using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Application.Exceptions.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Configuration;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using FluentValidation;

namespace AuditX.Application.Exceptions.Commands;

internal static class ExceptionParsing
{
    public static ExceptionSeverity ParseSeverity(string? value)
        => Enum.TryParse<ExceptionSeverity>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var s)
            ? s
            : throw new DomainException("exception.invalid_severity", $"Unknown severity '{value}'.");
}

public sealed record RaiseExceptionCommand(
    Guid AuditId, Guid ChecklistItemId, string Title, string? Severity, string RootCause, string Recommendation,
    string? Category, string? RootCauseCategory, Guid OwnerUserId, DateOnly? TargetDateOverride, string? OverrideRationale,
    string? NonConformanceCategory = null) : ICommand<ExceptionDto>;

public sealed class RaiseExceptionCommandValidator : AbstractValidator<RaiseExceptionCommand>
{
    public RaiseExceptionCommandValidator()
    {
        RuleFor(x => x.AuditId).NotEmpty();
        RuleFor(x => x.ChecklistItemId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        // Severity is required only when the item carries no risk rating to derive it from — enforced in the
        // handler, which is the only place that has the item loaded.
        RuleFor(x => x.RootCause).NotEmpty();
        RuleFor(x => x.Recommendation).NotEmpty();
        RuleFor(x => x.OwnerUserId).NotEmpty();
    }
}

public sealed class RaiseExceptionCommandHandler(
    IAuditRepository audits,
    IExceptionRepository exceptions,
    IExceptionRaisingRuleRepository raisingRules,
    IFindingLinkRepository findingLinks,
    IUserRepository users,
    IPermissionResolver permissions,
    IExceptionDefaults defaults,
    IConfigurationSnapshotter configSnapshotter,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RaiseExceptionCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(RaiseExceptionCommand command, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var item = auditEntity.ChecklistItems.FirstOrDefault(i => i.Id == command.ChecklistItemId)
            ?? throw new NotFoundException("Checklist item", command.ChecklistItemId);

        // Severity is derived from the item's risk rating when it has one — the item's own assessed risk is the
        // authoritative severity, not a free user choice. Only an unrated item falls back to a manual selection.
        var severity = item.RiskRating ?? (string.IsNullOrWhiteSpace(command.Severity)
            ? throw new DomainException("exception.severity_required", "This item has no risk rating, so a severity must be selected.")
            : ExceptionParsing.ParseSeverity(command.Severity));

        // Eligibility gate (US-M5-017 / BR-M6-001): a Fail verdict is always eligible; a bank-configured
        // ExceptionRaisingRule can extend eligibility for this item's response type to N/A and/or a low score
        // (e.g. a poor Rating response). Unconfigured response types keep the original Fail-only behaviour.
        var response = auditEntity.Responses.FirstOrDefault(r => r.ChecklistItemId == item.Id && !r.IsDraft);
        var rule = await raisingRules.GetByResponseTypeAsync(item.ResponseType, cancellationToken);
        var eligible = response is not null
            && (response.Verdict == ResponseVerdict.Fail || (rule?.IsEligible(response.Verdict, response.Score) ?? false));
        if (!eligible)
        {
            throw new DomainException("exception.item_not_fail", "An exception can only be raised on an item with a Fail response (or another response this bank has configured as exception-eligible).");
        }

        var owner = await users.GetByIdAsync(command.OwnerUserId, cancellationToken);
        if (owner is null || owner.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("exception.user_not_assignable", "The owner does not exist or is deactivated.");
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var defaultTarget = today.AddDays(defaults.TargetDays(severity));
        var overridden = command.TargetDateOverride is { } ov && ov != defaultTarget;
        var targetDate = command.TargetDateOverride ?? defaultTarget;
        if (overridden)
        {
            if (!await permissions.HasPermissionAsync(userId, PermissionKeys.ManageAudit, auditEntity.Id.ToString(), cancellationToken))
            {
                throw new ForbiddenAccessException("Only an audit manager may override the remediation target date.");
            }

            _ = Guard.NotNullOrWhiteSpace(command.OverrideRationale, "exception.override_rationale_required", "A rationale is required when overriding the target date.");
        }

        // Denormalise the universe entity for recurrence keying; null (an ad-hoc audit) → recurrence skipped.
        var auditableEntityId = auditEntity.AuditableEntityId;

        // Normalise the category the same way the domain persists it, so the recurrence lookup keys match.
        var normalizedCategory = string.IsNullOrWhiteSpace(command.Category) ? null : command.Category.Trim();
        var normalizedRootCauseCategory = string.IsNullOrWhiteSpace(command.RootCauseCategory) ? null : command.RootCauseCategory.Trim();
        var normalizedNonConformanceCategory = string.IsNullOrWhiteSpace(command.NonConformanceCategory) ? null : command.NonConformanceCategory.Trim();

        var isRecurrence = false;
        Guid? recurrenceOf = null;
        if (auditableEntityId is { } entityId)
        {
            var sinceUtc = clock.UtcNow.AddMonths(-defaults.RecurrenceWindowMonths);
            var match = await exceptions.FindClosedForRecurrenceAsync(entityId, normalizedCategory, sinceUtc, cancellationToken);
            if (match is not null)
            {
                isRecurrence = true;
                recurrenceOf = match.Id;
            }
        }

        // Honest snapshot-on-raise (S5): stamp the active exception_defaults version that produced this target date.
        // SINGLE-STORE only — sanctions_grid / notification_rules are NOT stamped (no integer version there).
        var configSnapshot = configSnapshotter.Capture(ConfigurationDomains.ExceptionDefaults);

        var exception = AuditException.Raise(
            command.AuditId, command.ChecklistItemId, auditableEntityId, command.Title, severity, command.RootCause,
            command.Recommendation, normalizedCategory, normalizedRootCauseCategory, command.OwnerUserId, userId, targetDate, overridden,
            command.OverrideRationale, isRecurrence, recurrenceOf, configSnapshot, clock.UtcNow,
            nonConformanceCategory: normalizedNonConformanceCategory);

        exceptions.Add(exception);
        auditEntity.SetItemException(command.ChecklistItemId, true); // M5 write-back (cleared only on cancel)
        audit.Record(AuditEventTypes.ExceptionRaised, AuditTargetTypes.Exception, exception.Id,
            after: new { exception.Title, severity = severity.ToString(), exception.OwnerUserId, exception.IsRecurrence });

        // Auto-link the finding to the control this item tests (P1-B): a failed control test IS a control
        // deficiency, so the finding↔control link is created without the auditor re-picking the control.
        if (item.ControlId is { } controlId)
        {
            findingLinks.AddControlLink(Domain.Compliance.ExceptionControlLink.Create(exception.Id, controlId, userId));
            audit.Record(AuditEventTypes.FindingLinkAdded, AuditTargetTypes.Exception, exception.Id,
                payload: new { ExceptionId = exception.Id, kind = "control", ControlId = controlId, auto = true });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(today);
    }
}

public sealed record ChangeSeverityCommand(Guid ExceptionId, string Severity, string Reason, string Version) : ICommand<ExceptionDto>;

public sealed class ChangeSeverityCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeSeverityCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(ChangeSeverityCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        exception.EnsureVersion(command.Version);

        var before = exception.Severity;
        exception.ChangeSeverity(ExceptionParsing.ParseSeverity(command.Severity), command.Reason, currentUser.UserId ?? Guid.Empty);
        audit.Record(AuditEventTypes.ExceptionSeverityChanged, AuditTargetTypes.Exception, exception.Id,
            before: new { severity = before.ToString() }, after: new { severity = exception.Severity.ToString(), command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record ReassignExceptionOwnerCommand(Guid ExceptionId, Guid OwnerUserId, string Version) : ICommand<ExceptionDto>;

public sealed class ReassignExceptionOwnerCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IUserRepository users, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ReassignExceptionOwnerCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(ReassignExceptionOwnerCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        exception.EnsureVersion(command.Version);

        var owner = await users.GetByIdAsync(command.OwnerUserId, cancellationToken);
        if (owner is null || owner.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("exception.user_not_assignable", "The owner does not exist or is deactivated.");
        }

        exception.Reassign(command.OwnerUserId, currentUser.UserId ?? Guid.Empty);
        audit.Record(AuditEventTypes.ExceptionOwnerReassigned, AuditTargetTypes.Exception, exception.Id, payload: new { command.OwnerUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record CancelExceptionCommand(Guid ExceptionId, string Reason, string Version) : ICommand<ExceptionDto>;

public sealed class CancelExceptionCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CancelExceptionCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(CancelExceptionCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        exception.EnsureVersion(command.Version);

        exception.Cancel(command.Reason, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        auditEntity.SetItemException(exception.ChecklistItemId, false); // clear the M5 flag (BR-M6-010)
        audit.Record(AuditEventTypes.ExceptionCancelled, AuditTargetTypes.Exception, exception.Id, payload: new { command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}
