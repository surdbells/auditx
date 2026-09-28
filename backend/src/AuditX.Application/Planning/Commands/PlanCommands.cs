using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Planning.Dtos;
using AuditX.Application.Planning.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Planning;
using FluentValidation;

namespace AuditX.Application.Planning.Commands;

public sealed record CreatePlanCommand(string PeriodLabel, DateOnly PeriodStart, DateOnly PeriodEnd) : ICommand<PlanDto>;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
        RuleFor(x => x.PeriodLabel).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PeriodEnd).GreaterThan(x => x.PeriodStart);
    }
}

public sealed class CreatePlanCommandHandler(
    IAnnualPlanRepository plans, IInstitutionSettingsRepository settings, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePlanCommand, PlanDto>
{
    public async Task<PlanDto> Handle(CreatePlanCommand command, CancellationToken cancellationToken)
    {
        var bank = await settings.GetAsync(cancellationToken);
        if (!bank.AllowOverlappingPlanPeriods
            && await plans.AnyOverlappingAsync(command.PeriodStart, command.PeriodEnd, null, cancellationToken))
        {
            throw new ConflictException("plan.period_overlap", "The plan period overlaps an existing plan.");
        }

        var plan = AnnualPlan.Create(command.PeriodLabel.Trim(), command.PeriodStart, command.PeriodEnd);
        plans.Add(plan);
        audit.Record(AuditEventTypes.PlanCreated, AuditTargetTypes.AnnualPlan, plan.Id, after: new { plan.PeriodLabel, plan.PeriodStart, plan.PeriodEnd });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return plan.ToDto(PlanLaunchPolicy.CanLaunchAudits(plan.Status, bank.AllowAuditLaunchBeforeApproval), PlanRevisionPolicy.CanApplyMinorRevision(plan.Status, bank.AllowMinorPlanRevisionAfterApproval));
    }
}

public sealed record UpdatePlanCommand(Guid Id, string PeriodLabel, DateOnly PeriodStart, DateOnly PeriodEnd) : ICommand<PlanDto>;

public sealed class UpdatePlanCommandHandler(
    IAnnualPlanRepository plans, IInstitutionSettingsRepository settings, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePlanCommand, PlanDto>
{
    public async Task<PlanDto> Handle(UpdatePlanCommand command, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Plan", command.Id);
        var bank = await settings.GetAsync(cancellationToken);
        if (!bank.AllowOverlappingPlanPeriods
            && await plans.AnyOverlappingAsync(command.PeriodStart, command.PeriodEnd, plan.Id, cancellationToken))
        {
            throw new ConflictException("plan.period_overlap", "The plan period overlaps an existing plan.");
        }

        plan.UpdatePeriod(command.PeriodLabel.Trim(), command.PeriodStart, command.PeriodEnd);
        audit.Record(AuditEventTypes.PlanUpdated, AuditTargetTypes.AnnualPlan, plan.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return plan.ToDto(PlanLaunchPolicy.CanLaunchAudits(plan.Status, bank.AllowAuditLaunchBeforeApproval), PlanRevisionPolicy.CanApplyMinorRevision(plan.Status, bank.AllowMinorPlanRevisionAfterApproval));
    }
}

public sealed record AddPlanItemCommand(
    Guid PlanId, IReadOnlyList<Guid> EntityIds, string AuditType, DateOnly PlannedStartDate, DateOnly PlannedEndDate, decimal? EstimatedEffortDays, Guid? AssignedLeadUserId)
    : ICommand<PlanItemDto>;

public sealed class AddPlanItemCommandValidator : AbstractValidator<AddPlanItemCommand>
{
    public AddPlanItemCommandValidator()
    {
        RuleFor(x => x.EntityIds).NotEmpty();
        RuleFor(x => x.AuditType).NotEmpty();
    }
}

public sealed class AddPlanItemCommandHandler(
    IAnnualPlanRepository plans, IAuditUniverseRepository entities, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<AddPlanItemCommand, PlanItemDto>
{
    public async Task<PlanItemDto> Handle(AddPlanItemCommand command, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(command.PlanId, cancellationToken) ?? throw new NotFoundException("Plan", command.PlanId);

        var distinctEntityIds = command.EntityIds.Distinct().ToArray();
        foreach (var entityId in distinctEntityIds)
        {
            _ = await entities.GetByIdAsync(entityId, cancellationToken)
                ?? throw new ConflictException("plan.entity_archived", "One or more referenced universe entities do not exist or are archived.");
        }

        var item = plan.AddItem(distinctEntityIds, command.AuditType.Trim(), command.PlannedStartDate, command.PlannedEndDate, command.EstimatedEffortDays, command.AssignedLeadUserId);
        audit.Record(AuditEventTypes.PlanItemAdded, AuditTargetTypes.AnnualPlan, plan.Id, payload: new { itemId = item.Id, entityIds = distinctEntityIds, command.AuditType });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToDto();
    }
}

public sealed record RemovePlanItemCommand(Guid PlanId, Guid ItemId) : ICommand<Unit>;

public sealed class RemovePlanItemCommandHandler(IAnnualPlanRepository plans, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RemovePlanItemCommand, Unit>
{
    public async Task<Unit> Handle(RemovePlanItemCommand command, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(command.PlanId, cancellationToken) ?? throw new NotFoundException("Plan", command.PlanId);
        plan.RemoveItem(command.ItemId);
        audit.Record(AuditEventTypes.PlanItemRemoved, AuditTargetTypes.AnnualPlan, plan.Id, payload: new { itemId = command.ItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ReorderPlanItemsCommand(Guid PlanId, IReadOnlyList<Guid> OrderedItemIds) : ICommand<Unit>;

public sealed class ReorderPlanItemsCommandHandler(IAnnualPlanRepository plans, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ReorderPlanItemsCommand, Unit>
{
    public async Task<Unit> Handle(ReorderPlanItemsCommand command, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(command.PlanId, cancellationToken) ?? throw new NotFoundException("Plan", command.PlanId);
        plan.ReorderItems(command.OrderedItemIds);
        audit.Record(AuditEventTypes.PlanItemsReordered, AuditTargetTypes.AnnualPlan, plan.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record SubmitPlanCommand(Guid PlanId) : ICommand<PlanDto>;

public sealed class SubmitPlanCommandHandler(IAnnualPlanRepository plans, IInstitutionSettingsRepository settings, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SubmitPlanCommand, PlanDto>
{
    public async Task<PlanDto> Handle(SubmitPlanCommand command, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(command.PlanId, cancellationToken) ?? throw new NotFoundException("Plan", command.PlanId);
        plan.Submit(clock.UtcNow);
        audit.Record(AuditEventTypes.PlanSubmitted, AuditTargetTypes.AnnualPlan, plan.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var bank = await settings.GetAsync(cancellationToken);
        return plan.ToDto(PlanLaunchPolicy.CanLaunchAudits(plan.Status, bank.AllowAuditLaunchBeforeApproval), PlanRevisionPolicy.CanApplyMinorRevision(plan.Status, bank.AllowMinorPlanRevisionAfterApproval));
    }
}

/// <summary>AC chair records a decision. Synchronous — not maker-checker gated by default (blueprint A0-11).</summary>
public sealed record RecordPlanDecisionCommand(Guid PlanId, string Decision, string? Detail, IReadOnlyList<string>? Comments) : ICommand<PlanDto>;

public sealed class RecordPlanDecisionCommandHandler(
    IAnnualPlanRepository plans, IInstitutionSettingsRepository settings, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordPlanDecisionCommand, PlanDto>
{
    public async Task<PlanDto> Handle(RecordPlanDecisionCommand command, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(command.PlanId, cancellationToken) ?? throw new NotFoundException("Plan", command.PlanId);
        if (!Enum.TryParse<AcDecisionOutcome>(command.Decision, ignoreCase: true, out var outcome))
        {
            throw new ConflictException("plan.invalid_decision", $"Unknown decision '{command.Decision}'.");
        }

        var decidedBy = currentUser.UserId ?? throw new UnauthorizedException();
        plan.RecordDecision(outcome, command.Detail, command.Comments ?? [], decidedBy, clock.UtcNow);
        audit.Record(AuditEventTypes.PlanDecisionRecorded, AuditTargetTypes.AnnualPlan, plan.Id, after: new { decision = outcome.ToString(), decidedBy });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var bank = await settings.GetAsync(cancellationToken);
        return plan.ToDto(PlanLaunchPolicy.CanLaunchAudits(plan.Status, bank.AllowAuditLaunchBeforeApproval), PlanRevisionPolicy.CanApplyMinorRevision(plan.Status, bank.AllowMinorPlanRevisionAfterApproval));
    }
}

public sealed record SubmitPlanRevisionCommand(Guid PlanId, string Kind, Guid? ItemId, DateOnly? NewStartDate, DateOnly? NewEndDate, string? Reason) : ICommand<PlanDto>;

public sealed class SubmitPlanRevisionCommandHandler(IAnnualPlanRepository plans, IInstitutionSettingsRepository settings, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SubmitPlanRevisionCommand, PlanDto>
{
    public async Task<PlanDto> Handle(SubmitPlanRevisionCommand command, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(command.PlanId, cancellationToken) ?? throw new NotFoundException("Plan", command.PlanId);
        var bank = await settings.GetAsync(cancellationToken);

        if (string.Equals(command.Kind, "minor", StringComparison.OrdinalIgnoreCase))
        {
            // Once Approved, a plan is fully locked by default — "an audit must happen as planned". A bank may opt
            // into this lighter-weight, no-re-approval-needed shortcut for flexibility.
            if (plan.Status == PlanStatus.Approved && !bank.AllowMinorPlanRevisionAfterApproval)
            {
                throw new InvalidStateTransitionException(
                    "plan.minor_revision_disabled",
                    "This deployment does not allow direct edits to an approved plan. Submit a material revision instead, which re-opens the plan for Audit-Committee re-approval.");
            }

            if (command.ItemId is not { } itemId || command.NewStartDate is not { } start || command.NewEndDate is not { } end)
            {
                throw new ConflictException("plan.minor_revision_invalid", "A minor revision requires an item id and new dates.");
            }

            plan.ApplyMinorItemDateChange(itemId, start, end);
        }
        else if (string.Equals(command.Kind, "material", StringComparison.OrdinalIgnoreCase))
        {
            plan.BeginMaterialRevision(command.Reason, clock.UtcNow);
        }
        else
        {
            throw new ConflictException("plan.invalid_revision_kind", $"Unknown revision kind '{command.Kind}'.");
        }

        audit.Record(AuditEventTypes.PlanRevisionSubmitted, AuditTargetTypes.AnnualPlan, plan.Id, payload: new { command.Kind, command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return plan.ToDto(PlanLaunchPolicy.CanLaunchAudits(plan.Status, bank.AllowAuditLaunchBeforeApproval), PlanRevisionPolicy.CanApplyMinorRevision(plan.Status, bank.AllowMinorPlanRevisionAfterApproval));
    }
}

public sealed record ClosePlanCommand(Guid PlanId) : ICommand<Unit>;

public sealed class ClosePlanCommandHandler(IAnnualPlanRepository plans, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ClosePlanCommand, Unit>
{
    public async Task<Unit> Handle(ClosePlanCommand command, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(command.PlanId, cancellationToken) ?? throw new NotFoundException("Plan", command.PlanId);
        plan.Close();
        audit.Record(AuditEventTypes.PlanClosed, AuditTargetTypes.AnnualPlan, plan.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
