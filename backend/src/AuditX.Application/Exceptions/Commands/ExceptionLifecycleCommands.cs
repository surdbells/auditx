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
    Guid AuditId, Guid ChecklistItemId, string Title, string Severity, string RootCause, string Recommendation,
    string? Category, Guid OwnerUserId, DateOnly? TargetDateOverride, string? OverrideRationale) : ICommand<ExceptionDto>;

public sealed class RaiseExceptionCommandValidator : AbstractValidator<RaiseExceptionCommand>
{
    public RaiseExceptionCommandValidator()
    {
        RuleFor(x => x.AuditId).NotEmpty();
        RuleFor(x => x.ChecklistItemId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Severity).NotEmpty();
        RuleFor(x => x.RootCause).NotEmpty();
        RuleFor(x => x.Recommendation).NotEmpty();
        RuleFor(x => x.OwnerUserId).NotEmpty();
    }
}

public sealed class RaiseExceptionCommandHandler(
    IAuditRepository audits,
    IExceptionRepository exceptions,
    IAnnualPlanRepository plans,
    IUserRepository users,
    IPermissionResolver permissions,
    IExceptionDefaults defaults,
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
        var severity = ExceptionParsing.ParseSeverity(command.Severity);

        var item = auditEntity.ChecklistItems.FirstOrDefault(i => i.Id == command.ChecklistItemId)
            ?? throw new NotFoundException("Checklist item", command.ChecklistItemId);

        // Pass/N-A gate (US-M5-017 / BR-M6-001): an exception can only be raised on a finalised Fail.
        var hasFail = auditEntity.Responses.Any(r => r.ChecklistItemId == item.Id && !r.IsDraft && r.Verdict == ResponseVerdict.Fail);
        if (!hasFail)
        {
            throw new DomainException("exception.item_not_fail", "An exception can only be raised on an item with a Fail response.");
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

        // Denormalise the universe entity (via the plan link) for recurrence keying; null → recurrence skipped.
        Guid? auditableEntityId = null;
        if (auditEntity.PlanItemId is { } planItemId && await plans.GetByPlanItemIdAsync(planItemId, cancellationToken) is { } plan)
        {
            auditableEntityId = plan.Items.FirstOrDefault(i => i.Id == planItemId)?.EntityId;
        }

        var isRecurrence = false;
        Guid? recurrenceOf = null;
        if (auditableEntityId is { } entityId)
        {
            var sinceUtc = clock.UtcNow.AddMonths(-defaults.RecurrenceWindowMonths);
            var match = await exceptions.FindClosedForRecurrenceAsync(entityId, command.Category, sinceUtc, cancellationToken);
            if (match is not null)
            {
                isRecurrence = true;
                recurrenceOf = match.Id;
            }
        }

        var exception = AuditException.Raise(
            command.AuditId, command.ChecklistItemId, auditableEntityId, command.Title, severity, command.RootCause,
            command.Recommendation, command.Category, command.OwnerUserId, userId, targetDate, overridden,
            command.OverrideRationale, isRecurrence, recurrenceOf, "{}", clock.UtcNow);

        exceptions.Add(exception);
        auditEntity.SetItemException(command.ChecklistItemId, true); // M5 write-back (cleared only on cancel)
        audit.Record(AuditEventTypes.ExceptionRaised, AuditTargetTypes.Exception, exception.Id,
            after: new { exception.Title, severity = severity.ToString(), exception.OwnerUserId, exception.IsRecurrence });

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
