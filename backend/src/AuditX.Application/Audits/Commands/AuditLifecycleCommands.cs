using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits.Dtos;
using AuditX.Application.Audits.Mapping;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;

namespace AuditX.Application.Audits.Commands;

public sealed record UpdateAuditMetadataCommand(Guid Id, string Name, string? ScopeDescription, DateOnly StartDate, DateOnly TargetEndDate, string Version) : ICommand<AuditDto>;

public sealed class UpdateAuditMetadataCommandHandler(IAuditRepository audits, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateAuditMetadataCommand, AuditDto>
{
    public async Task<AuditDto> Handle(UpdateAuditMetadataCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Audit", command.Id);
        entity.EnsureVersion(command.Version);
        entity.UpdateMetadata(command.Name.Trim(), command.ScopeDescription, command.StartDate, command.TargetEndDate);
        audit.Record(AuditEventTypes.AuditMetadataUpdated, AuditTargetTypes.Audit, entity.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

/// <summary>Sets (or clears) the audit's planned-effort budget in hours (P0-B budget-vs-actual baseline).</summary>
public sealed record SetAuditBudgetCommand(Guid Id, decimal? BudgetedHours, string Version) : ICommand<AuditDto>;

public sealed class SetAuditBudgetCommandHandler(
    IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<SetAuditBudgetCommand, AuditDto>
{
    public async Task<AuditDto> Handle(SetAuditBudgetCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Audit", command.Id);

        // Resource-scope check: [RequirePermission(ManageAudit)] only gates globally, so a per-audit-scoped grant
        // could otherwise set another audit's budget. Re-check ManageAudit scoped to THIS audit (mirrors the
        // time-entry write path's EnsureCanModifyAsync).
        var uid = currentUser.UserId ?? throw new ForbiddenAccessException();
        if (!await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, entity.Id.ToString(), cancellationToken))
        {
            throw new ForbiddenAccessException("You do not manage this audit.");
        }

        entity.EnsureVersion(command.Version);
        entity.SetBudgetedHours(command.BudgetedHours);
        audit.Record(AuditEventTypes.AuditBudgetSet, AuditTargetTypes.Audit, entity.Id, after: new { command.BudgetedHours });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

/// <summary>
/// Schedule (or reschedule) the pre-audit kickoff meeting. Gated the same way as a lifecycle transition
/// (ManageAudit on this audit, or the self-assessor on their own assessment). Raising the domain event gives
/// the auditee advance notice through the M10 notification pipeline.
/// </summary>
public sealed record ScheduleAuditKickoffCommand(Guid Id, DateTimeOffset ScheduledAtUtc, string? Location, string? Agenda, string Version) : ICommand<AuditDto>;

public sealed class ScheduleAuditKickoffCommandHandler(
    IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ScheduleAuditKickoffCommand, AuditDto>
{
    public async Task<AuditDto> Handle(ScheduleAuditKickoffCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Audit", command.Id);
        await AuditManageAuthorization.EnsureCanManageAsync(entity, currentUser.UserId, permissions, cancellationToken);
        entity.EnsureVersion(command.Version);
        entity.ScheduleKickoff(command.ScheduledAtUtc, command.Location, command.Agenda, clock.UtcNow);
        audit.Record(AuditEventTypes.AuditKickoffScheduled, AuditTargetTypes.Audit, entity.Id,
            after: new { scheduledAtUtc = command.ScheduledAtUtc, entity.KickoffLocation });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

/// <summary>Transition an audit through its lifecycle (US-M4-010/011/012/013/016/017).</summary>
/// <summary>
/// Links an AD-HOC audit to the audit-universe entity it examines, or clears the link (null EntityId). Audits created
/// before the entity could be chosen — or created without one — would otherwise never count toward coverage. Allowed
/// at any stage except cancelled; when the audit is already completed the affected entities' "last audited" stamps
/// are brought back in line with their completed audits.
/// </summary>
public sealed record SetAuditEntityCommand(Guid Id, Guid? EntityId, string Version) : ICommand<AuditDto>;

public sealed class SetAuditEntityCommandHandler(
    IAuditRepository audits, IAuditUniverseRepository universe, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<SetAuditEntityCommand, AuditDto>
{
    public async Task<AuditDto> Handle(SetAuditEntityCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Audit", command.Id);

        // Resource-scope check: the controller's ManageAudit gate is global; re-check it for THIS audit.
        var uid = currentUser.UserId ?? throw new ForbiddenAccessException();
        if (!await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, entity.Id.ToString(), cancellationToken))
        {
            throw new ForbiddenAccessException("You do not manage this audit.");
        }

        entity.EnsureVersion(command.Version);
        var previousEntityId = entity.AuditableEntityId;
        if (previousEntityId == command.EntityId)
        {
            return entity.ToDto();
        }

        // The new entity must be a live (not archived) universe entity.
        Domain.Universe.AuditableEntity? target = null;
        if (command.EntityId is { } newEntityId)
        {
            target = await universe.GetByIdAsync(newEntityId, cancellationToken) ?? throw new NotFoundException("Auditable entity", newEntityId);
        }

        entity.SetAuditableEntity(command.EntityId);

        // A COMPLETED audit already counts toward coverage, so moving it moves the credit: the entity it now points
        // at is audited as of its completion date (never moving that entity's stamp backwards), and the entity it
        // left is restated from whatever completed audits it still has (possibly none — "never audited").
        if (entity.Status == AuditStatus.Completed && entity.ActualEndDate is { } completedOn)
        {
            var completedAt = AtStartOfDayUtc(completedOn);
            if (target is not null && (target.LastAuditedAt is null || target.LastAuditedAt < completedAt))
            {
                target.MarkAudited(completedAt);
            }

            if (previousEntityId is { } previousId
                && await universe.GetByIdIncludingArchivedAsync(previousId, cancellationToken) is { } previous)
            {
                var latest = await audits.GetLatestCompletionDateForEntityAsync(previousId, entity.Id, cancellationToken);
                previous.RestateLastAudited(latest is { } date ? AtStartOfDayUtc(date) : null);
            }
        }

        audit.Record(AuditEventTypes.AuditEntityLinked, AuditTargetTypes.Audit, entity.Id,
            before: new { auditableEntityId = previousEntityId },
            after: new { auditableEntityId = command.EntityId, entityName = target?.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto() with { AuditableEntityName = target?.Name };
    }

    private static DateTimeOffset AtStartOfDayUtc(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}

public sealed record TransitionAuditCommand(Guid Id, string TargetState, string? Reason, string Version) : ICommand<AuditDto>;

public sealed class TransitionAuditCommandHandler(
    IAuditRepository audits,
    IAnnualPlanRepository plans,
    IAuditUniverseRepository universe,
    IPermissionResolver permissions,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<TransitionAuditCommand, AuditDto>
{
    public async Task<AuditDto> Handle(TransitionAuditCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Audit", command.Id);
        await AuditManageAuthorization.EnsureCanManageAsync(entity, currentUser.UserId, permissions, cancellationToken);
        entity.EnsureVersion(command.Version);
        var target = command.TargetState.Replace("-", "_").ToLowerInvariant();

        switch (target)
        {
            case "planned":
                entity.Plan();
                break;
            case "in_progress":
                // Planned → InProgress (start) and UnderReview → InProgress (return) share the same target
                // state; the reason-required return edge is selected by the current status (US-M4-016).
                if (entity.Status == AuditStatus.UnderReview)
                {
                    entity.ReturnToInProgress(command.Reason ?? throw new ConflictException("audit.reason_required", "A reason is required to return an audit to in progress."));
                }
                else
                {
                    entity.Start();
                }

                break;
            case "under_review":
                entity.SendToReview(command.Reason);
                break;
            case "completed":
                await CompleteAsync(entity, cancellationToken);
                break;
            case "draft":
                entity.Reopen(command.Reason ?? throw new ConflictException("audit.reason_required", "A reason is required to reopen."));
                break;
            default:
                throw new ConflictException("audit.invalid_target_state", $"Unknown target state '{command.TargetState}'.");
        }

        audit.Record(AuditEventTypes.AuditTransitioned, AuditTargetTypes.Audit, entity.Id,
            after: new { status = entity.Status.ToSnake(), command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    private async Task CompleteAsync(Domain.Audits.Audit entity, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        entity.Complete(today);

        // Cross-module on completion (US-M3-011): mark the linked plan item's entity as completed on the plan.
        if (entity.PlanItemId is { } planItemId && entity.AuditableEntityId is { } completedEntityId
            && await plans.GetByPlanItemIdAsync(planItemId, cancellationToken) is { } plan)
        {
            plan.MarkPlanItemCompleted(planItemId, completedEntityId);
        }

        // Coverage (US-M3-019): ANY completed independent audit of an entity — plan-launched or ad hoc — stamps it as
        // audited. A self-assessment is the area assessing itself, not an independent audit, so it does not count.
        if (!entity.IsSelfAssessment && entity.AuditableEntityId is { } auditedEntityId
            && await universe.GetByIdAsync(auditedEntityId, cancellationToken) is { } auditedEntity)
        {
            auditedEntity.MarkAudited(clock.UtcNow);
        }

        audit.Record(AuditEventTypes.AuditCompleted, AuditTargetTypes.Audit, entity.Id, after: new { actualEndDate = today });
    }
}

public sealed record CancelAuditCommand(Guid Id, string Reason, string Version) : ICommand<AuditDto>;

public sealed class CancelAuditCommandHandler(
    IAuditRepository audits, IAnnualPlanRepository plans, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CancelAuditCommand, AuditDto>
{
    public async Task<AuditDto> Handle(CancelAuditCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Audit", command.Id);
        entity.EnsureVersion(command.Version);
        entity.Cancel(command.Reason, clock.UtcNow);

        if (entity.PlanItemId is { } planItemId && entity.AuditableEntityId is { } cancelledEntityId
            && await plans.GetByPlanItemIdAsync(planItemId, cancellationToken) is { } plan)
        {
            plan.MarkPlanItemDeferred(planItemId, cancelledEntityId);
        }

        audit.Record(AuditEventTypes.AuditCancelled, AuditTargetTypes.Audit, entity.Id, payload: new { command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

/// <summary>Auto-start planned audits whose start date has arrived (US-M4-011). Invoked by the hourly job.</summary>
public sealed record AutoStartAuditsCommand : ICommand<int>;

public sealed class AutoStartAuditsCommandHandler(IAuditRepository audits, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AutoStartAuditsCommand, int>
{
    public async Task<int> Handle(AutoStartAuditsCommand command, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var due = await audits.GetPlannedDueToStartAsync(today, cancellationToken);

        foreach (var entity in due)
        {
            entity.Start();
            audit.RecordAs(ActorType.System, "audit-auto-start", null, AuditEventTypes.AuditTransitioned, AuditTargetTypes.Audit, entity.Id,
                after: new { status = entity.Status.ToSnake() });
        }

        if (due.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }
}
