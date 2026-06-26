using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits.Dtos;
using AuditX.Application.Audits.Mapping;
using AuditX.Application.Audits.Services;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Audits.Commands;

public sealed record CreateAuditCommand(
    string Name,
    string AuditType,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    string? ScopeDescription,
    Guid? TemplateId,
    Guid? PlanItemId,
    Guid LeadUserId,
    Guid AuditeeUserId,
    IReadOnlyList<Guid>? TeamMemberUserIds,
    bool BackdatingOverride,
    string? BackdatingReason) : ICommand<AuditDto>;

public sealed class CreateAuditCommandValidator : AbstractValidator<CreateAuditCommand>
{
    public CreateAuditCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.AuditType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LeadUserId).NotEmpty();
        RuleFor(x => x.AuditeeUserId).NotEmpty();
    }
}

public sealed class CreateAuditCommandHandler(
    IAuditRepository audits,
    IAnnualPlanRepository plans,
    IUserRepository users,
    AuditCreationService creationService,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateAuditCommand, AuditDto>
{
    public async Task<AuditDto> Handle(CreateAuditCommand command, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        // Backdating control (US-M4-002).
        if (command.StartDate < today)
        {
            if (!command.BackdatingOverride)
            {
                throw new DomainException("audit.start_in_past", "Start date is in the past; set backdating override with a reason.");
            }

            _ = Domain.Common.Guard.MinLength(command.BackdatingReason, 20, "audit.backdating_reason_too_short", "A backdating reason of at least 20 characters is required.");
        }

        await EnsureUsersAssignableAsync(command, cancellationToken);

        // Resolve the linked plan (if any) once: it provides the effort default and is mutated on link.
        var plan = command.PlanItemId is { } pid ? await plans.GetByPlanItemIdAsync(pid, cancellationToken) : null;
        if (command.PlanItemId is { } planItemId && plan is null)
        {
            throw new NotFoundException("Plan item", planItemId);
        }

        var targetEnd = command.TargetEndDate ?? ComputeDefaultTargetEnd(command, plan);
        var data = new CreateAuditData(
            command.Name, command.AuditType, command.StartDate, targetEnd, command.ScopeDescription,
            command.TemplateId, null, command.PlanItemId, command.LeadUserId, command.AuditeeUserId,
            command.TeamMemberUserIds ?? []);

        var auditEntity = await creationService.BuildAsync(data, currentUser.UserId, clock.UtcNow, cancellationToken);

        if (command.PlanItemId is { } linkItemId)
        {
            plan!.LinkAuditToItem(linkItemId, auditEntity.Id); // throws plan.item_not_linkable (409) if not Approved
        }

        audits.Add(auditEntity);
        audit.Record(AuditEventTypes.AuditCreated, AuditTargetTypes.Audit, auditEntity.Id,
            after: new { auditEntity.Name, auditEntity.AuditType, auditEntity.TemplateId, auditEntity.PlanItemId });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return auditEntity.ToDto();
    }

    private DateOnly ComputeDefaultTargetEnd(CreateAuditCommand command, Domain.Planning.AnnualPlan? plan)
    {
        if (command.PlanItemId is { } itemId && plan is not null)
        {
            var item = plan.Items.FirstOrDefault(i => i.Id == itemId);
            if (item?.EstimatedEffortDays is { } effort && effort > 0)
            {
                return command.StartDate.AddDays((int)Math.Ceiling(effort));
            }
        }

        return command.StartDate.AddDays(30);
    }

    private async Task EnsureUsersAssignableAsync(CreateAuditCommand command, CancellationToken cancellationToken)
    {
        var ids = new List<Guid> { command.LeadUserId, command.AuditeeUserId };
        ids.AddRange(command.TeamMemberUserIds ?? []);
        var distinct = ids.Distinct().ToArray();

        var found = await users.GetByIdsAsync(distinct, cancellationToken);
        var assignable = found.Where(u => u.Status != UserStatus.Deactivated).Select(u => u.Id).ToHashSet();

        if (distinct.Any(id => !assignable.Contains(id)))
        {
            throw new ConflictException("audit.user_not_assignable", "One or more referenced users do not exist or are deactivated.");
        }
    }
}
