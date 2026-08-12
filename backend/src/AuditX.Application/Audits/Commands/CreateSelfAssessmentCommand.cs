using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits.Dtos;
using AuditX.Application.Audits.Mapping;
using AuditX.Application.Audits.Services;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Audits.Commands;

/// <summary>
/// Start a self-assessment: an audit the area owner runs on their own area. The current user is both the lead
/// (assessor) and the auditee (subject); it reuses the full checklist/response/exception engine. Requires the
/// RunSelfAssessment permission — no independent auditor is involved.
/// </summary>
public sealed record CreateSelfAssessmentCommand(
    string Name, string AuditType, DateOnly StartDate, DateOnly? TargetEndDate, string? ScopeDescription,
    Guid? TemplateId, Guid? AuditableEntityId) : ICommand<AuditDto>;

public sealed class CreateSelfAssessmentCommandValidator : AbstractValidator<CreateSelfAssessmentCommand>
{
    public CreateSelfAssessmentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.AuditType).NotEmpty().MaximumLength(100);
        // A self-assessor holds RunSelfAssessment but not ManageAudit, so they cannot hand-add checklist items:
        // the assessment must be seeded from a published template, otherwise it would have nothing to respond to.
        RuleFor(x => x.TemplateId).NotNull().WithMessage("A self-assessment must be based on a template.");
    }
}

public sealed class CreateSelfAssessmentCommandHandler(
    IAuditRepository audits,
    IUserRepository users,
    AuditCreationService creationService,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateSelfAssessmentCommand, AuditDto>
{
    public async Task<AuditDto> Handle(CreateSelfAssessmentCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        // The assessor is the current user, and must be an assignable (active) account.
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("audit.user_not_assignable", "Your account cannot run a self-assessment.");
        }

        var targetEnd = command.TargetEndDate ?? command.StartDate.AddDays(30);
        var data = new CreateAuditData(
            command.Name, command.AuditType, command.StartDate, targetEnd, command.ScopeDescription,
            command.TemplateId, null, PlanItemId: null, command.AuditableEntityId,
            LeadUserId: userId, AuditeeUserId: userId, TeamMemberUserIds: [], IsSelfAssessment: true);

        var auditEntity = await creationService.BuildAsync(data, userId, clock.UtcNow, cancellationToken);
        audits.Add(auditEntity);
        audit.Record(AuditEventTypes.AuditCreated, AuditTargetTypes.Audit, auditEntity.Id,
            after: new { auditEntity.Name, auditEntity.AuditType, selfAssessment = true });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return auditEntity.ToDto();
    }
}
