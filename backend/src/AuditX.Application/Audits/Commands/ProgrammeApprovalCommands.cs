using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits.Dtos;
using AuditX.Application.Audits.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using FluentValidation;

namespace AuditX.Application.Audits.Commands;

/// <summary>
/// Approves a planned audit's programme (scope, objectives and checklist) for fieldwork. The approver supervises the
/// engagement: they hold ManageAudit for this audit, and are neither its lead (who prepared the programme) nor its
/// auditee. They need not be on the team.
/// </summary>
public sealed record ApproveAuditProgrammeCommand(Guid AuditId, string? Note, string Version) : ICommand<AuditDto>;

public sealed class ApproveAuditProgrammeCommandValidator : AbstractValidator<ApproveAuditProgrammeCommand>
{
    public ApproveAuditProgrammeCommandValidator()
    {
        RuleFor(x => x.AuditId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class ApproveAuditProgrammeCommandHandler(
    IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveAuditProgrammeCommand, AuditDto>
{
    public async Task<AuditDto> Handle(ApproveAuditProgrammeCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        var uid = currentUser.UserId ?? throw new ForbiddenAccessException();

        Execution.AuditIndependence.EnsureNotAuditee(entity, uid);
        if (!await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, entity.Id.ToString(), cancellationToken))
        {
            throw new ForbiddenAccessException("You do not manage this audit.");
        }

        // Segregation of duties: whoever prepared the programme does not approve it.
        if (uid == entity.LeadUserId)
        {
            throw new ForbiddenAccessException("The audit's lead prepared the programme and cannot approve it. Another manager of the audit approves it.");
        }

        entity.EnsureVersion(command.Version);
        entity.ApproveProgramme(uid, command.Note, clock.UtcNow);
        audit.Record(AuditEventTypes.AuditProgrammeApproved, AuditTargetTypes.Audit, entity.Id,
            after: new { approvedBy = uid, note = entity.ProgrammeApprovalNote });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}
