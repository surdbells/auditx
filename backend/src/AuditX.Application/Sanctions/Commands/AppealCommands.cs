using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Sanctions.Dtos;
using AuditX.Application.Sanctions.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Sanctions;
using FluentValidation;

namespace AuditX.Application.Sanctions.Commands;

// ---- File appeal (subject only) ----

public sealed record FileAppealCommand(Guid CaseId, string Basis, Guid? EvidenceFileId, string Version) : ICommand<SanctionsAppealDto>;

public sealed class FileAppealCommandValidator : AbstractValidator<FileAppealCommand>
{
    public FileAppealCommandValidator()
    {
        RuleFor(x => x.Basis).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class FileAppealCommandHandler(
    ISanctionsCaseRepository cases, ISanctionsAppealRepository appeals, IUserRepository users,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<FileAppealCommand, SanctionsAppealDto>
{
    public async Task<SanctionsAppealDto> Handle(FileAppealCommand command, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(command.CaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", command.CaseId);
        sanctionsCase.EnsureVersion(command.Version);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        // US-M7-012: only the subject may file (designated-representative delegation is deferred — see blueprint A4b).
        if (sanctionsCase.SubjectUserId != actorId)
        {
            throw new ForbiddenAccessException("Only the subject of the sanction may file an appeal.");
        }

        var routedTo = await ResolveAppealsAuthorityAsync(users, cancellationToken);
        sanctionsCase.MarkAppealed();
        var appeal = SanctionsAppeal.File(sanctionsCase.Id, actorId, routedTo, command.Basis, clock.UtcNow);
        appeals.Add(appeal);

        // Pin RoutedToUserId as a top-level string Guid so the M10 payload_derived recipient resolves (D2).
        audit.Record(AuditEventTypes.AppealFiled, AuditTargetTypes.SanctionsAppeal, appeal.Id,
            payload: new { SanctionsCaseId = appeal.SanctionsCaseId, RoutedToUserId = appeal.RoutedToUserId, appeal.AppellantUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return appeal.ToDto();
    }

    /// <summary>Resolve the appeals authority (default CIA): the first active holder of the Appeals Authority role, else a CIA-permission holder.</summary>
    private static async Task<Guid> ResolveAppealsAuthorityAsync(IUserRepository users, CancellationToken cancellationToken)
    {
        var authorities = await users.GetActiveByRoleNameAsync(SanctionsRoles.AppealsAuthority, cancellationToken);
        if (authorities.Count > 0)
        {
            return authorities[0].Id;
        }

        var admins = await users.GetActiveByRoleNameAsync(BuiltInRoles.AdministratorName, cancellationToken);
        if (admins.Count > 0)
        {
            return admins[0].Id;
        }

        throw new ConflictException("sanctions.no_appeals_authority", "No appeals authority is configured to route the appeal to.");
    }
}

// ---- Decide appeal (routed authority only) ----

public sealed record DecideAppealCommand(Guid AppealId, string Outcome, string Rationale, string Version) : ICommand<SanctionsAppealDto>;

public sealed class DecideAppealCommandValidator : AbstractValidator<DecideAppealCommand>
{
    public DecideAppealCommandValidator()
    {
        RuleFor(x => x.Outcome).NotEmpty();
        RuleFor(x => x.Rationale).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class DecideAppealCommandHandler(
    ISanctionsAppealRepository appeals, ISanctionsCaseRepository cases, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DecideAppealCommand, SanctionsAppealDto>
{
    public async Task<SanctionsAppealDto> Handle(DecideAppealCommand command, CancellationToken cancellationToken)
    {
        var appeal = await appeals.GetByIdAsync(command.AppealId, cancellationToken) ?? throw new NotFoundException("Appeal", command.AppealId);
        appeal.EnsureVersion(command.Version);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        // In-handler scope: only the routed authority may decide.
        if (appeal.RoutedToUserId != actorId)
        {
            throw new ForbiddenAccessException("Only the routed appeals authority may decide this appeal.");
        }

        var outcome = SanctionsParsing.ParseAppealOutcome(command.Outcome);
        var detailJson = AppJson.Serialize(new
        {
            outcome = outcome.ToString(),
            detail = command.Rationale,
            decided_by = actorId,
            decided_at = clock.UtcNow,
        });
        appeal.RecordDecision(outcome, detailJson, actorId, clock.UtcNow);

        // Propagate to the case trail (FR-M7-009): appealed → appeal_decision_recorded.
        var sanctionsCase = await cases.GetByIdAsync(appeal.SanctionsCaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", appeal.SanctionsCaseId);
        sanctionsCase.RecordAppealOutcome(outcome, appeal.Id, actorId);

        audit.Record(AuditEventTypes.AppealOutcomeRecorded, AuditTargetTypes.SanctionsAppeal, appeal.Id,
            payload: new { SanctionsCaseId = appeal.SanctionsCaseId, outcome = outcome.ToString(), AppellantUserId = appeal.AppellantUserId });
        // The returned appeal carries the appellant (= the case subject) identity to the routed authority, who is
        // not a stored case-team member — record the deliberate exposure for the audit trail (US-M7-016).
        audit.Record(AuditEventTypes.SubjectIdentityExposed, AuditTargetTypes.SanctionsCase, appeal.SanctionsCaseId,
            payload: new { viewer = actorId, via = "appeal_decision" });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return appeal.ToDto();
    }
}
