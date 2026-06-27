using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions;
using AuditX.Application.Sanctions.Dtos;
using AuditX.Application.Sanctions.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Sanctions;
using FluentValidation;

namespace AuditX.Application.Sanctions.Commands;

internal static class SanctionsParsing
{
    public static HrOutcomeType ParseHrOutcome(string? value)
        => Enum.TryParse<HrOutcomeType>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var v)
            ? v
            : throw new DomainException("sanctions.invalid_hr_outcome", $"Unknown HR outcome '{value}'.");

    public static DcDecisionType ParseDcDecision(string? value)
        => Enum.TryParse<DcDecisionType>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var v)
            ? v
            : throw new DomainException("sanctions.invalid_dc_decision", $"Unknown DC decision '{value}'.");

    public static AppealOutcome ParseAppealOutcome(string? value)
        => Enum.TryParse<AppealOutcome>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var v)
            ? v
            : throw new DomainException("sanctions.invalid_appeal_outcome", $"Unknown appeal outcome '{value}'.");
}

// ---- Trigger (from an exception) ----

public sealed record TriggerSanctionsCommand(Guid ExceptionId, Guid? SubjectUserId) : ICommand<SanctionsCaseDto>;

public sealed class TriggerSanctionsCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, ISanctionsCaseRepository cases,
    IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<TriggerSanctionsCommand, SanctionsCaseDto>
{
    public async Task<SanctionsCaseDto> Handle(TriggerSanctionsCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        // Decoupling (FR-M7-011): copy category/severity/recurrence from the exception — the case never re-reads it.
        var sanctionsCase = SanctionsCase.Trigger(
            exception.Id, command.SubjectUserId, exception.Category, exception.Severity, exception.IsRecurrence, actorId, clock.UtcNow);
        cases.Add(sanctionsCase);
        audit.Record(AuditEventTypes.SanctionsTriggered, AuditTargetTypes.SanctionsCase, sanctionsCase.Id,
            after: new { sanctionsCase.ExceptionId, severity = exception.Severity.ToString(), sanctionsCase.IsRecurrence });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sanctionsCase.ToDto(unmask: true);
    }
}

// ---- Recommendation ----

public sealed record RecordRecommendationCommand(Guid CaseId, string Recommendation, string? DeviationReason, string Version) : ICommand<SanctionsCaseDto>;

public sealed class RecordRecommendationCommandValidator : AbstractValidator<RecordRecommendationCommand>
{
    public RecordRecommendationCommandValidator()
    {
        RuleFor(x => x.Recommendation).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class RecordRecommendationCommandHandler(
    ISanctionsCaseRepository cases, ISanctionsGridRepository grids, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordRecommendationCommand, SanctionsCaseDto>
{
    public async Task<SanctionsCaseDto> Handle(RecordRecommendationCommand command, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(command.CaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", command.CaseId);
        sanctionsCase.EnsureVersion(command.Version);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        // Consult the active grid for the case's pinned (category, severity, recurrence) tuple. When there is no
        // active grid OR no matching cell (no recommended range), there is nothing to deviate FROM, so the
        // recommendation is treated as within range — a deviation reason is required only when a recommended
        // range exists and the recommendation falls outside it (FR-M7-004).
        var grid = await grids.GetActiveAsync(cancellationToken);
        int? gridVersion = grid?.VersionNumber;
        string? gridRange = null;
        var withinRange = true;
        if (grid is not null)
        {
            var consultation = GridConsultation.Consult(grid.GridDefinitionJson, sanctionsCase.Category, sanctionsCase.Severity, sanctionsCase.IsRecurrence);
            gridRange = consultation.RecommendedRange;
            withinRange = gridRange is null || GridConsultation.WithinRange(consultation, command.Recommendation);
        }

        sanctionsCase.RecordRecommendation(command.Recommendation, gridVersion, gridRange, withinRange, command.DeviationReason, actorId, clock.UtcNow);
        sanctionsCase.EnsureTeamMember(actorId, "recommender");
        audit.Record(AuditEventTypes.SanctionsRecommended, AuditTargetTypes.SanctionsCase, sanctionsCase.Id,
            after: new { sanctionsCase.GridConsultedVersion, sanctionsCase.WithinGridRange, hasDeviationReason = sanctionsCase.DeviationReason is not null });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sanctionsCase.ToDto(unmask: true);
    }
}

public sealed record SubmitRecommendationCommand(Guid CaseId, string Version) : ICommand<SanctionsCaseDto>;

public sealed class SubmitRecommendationCommandHandler(
    ISanctionsCaseRepository cases, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SubmitRecommendationCommand, SanctionsCaseDto>
{
    public async Task<SanctionsCaseDto> Handle(SubmitRecommendationCommand command, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(command.CaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", command.CaseId);
        sanctionsCase.EnsureVersion(command.Version);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        sanctionsCase.SubmitRecommendation(actorId, clock.UtcNow);
        audit.Record(AuditEventTypes.SanctionsRecommendationSubmitted, AuditTargetTypes.SanctionsCase, sanctionsCase.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sanctionsCase.ToDto(unmask: true);
    }
}

// ---- HR outcome / DC referral / DC decision ----

public sealed record RecordHrOutcomeCommand(Guid CaseId, string OutcomeType, string Detail, Guid? EvidenceFileId, string Version) : ICommand<SanctionsCaseDto>;

public sealed class RecordHrOutcomeCommandValidator : AbstractValidator<RecordHrOutcomeCommand>
{
    public RecordHrOutcomeCommandValidator()
    {
        RuleFor(x => x.OutcomeType).NotEmpty();
        RuleFor(x => x.Detail).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class RecordHrOutcomeCommandHandler(
    ISanctionsCaseRepository cases, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordHrOutcomeCommand, SanctionsCaseDto>
{
    public async Task<SanctionsCaseDto> Handle(RecordHrOutcomeCommand command, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(command.CaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", command.CaseId);
        sanctionsCase.EnsureVersion(command.Version);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var outcomeType = SanctionsParsing.ParseHrOutcome(command.OutcomeType);

        var detailJson = AppJson.Serialize(new
        {
            outcome_type = outcomeType.ToString(),
            detail = command.Detail,
            evidence_file_id = command.EvidenceFileId,
            recorded_by = actorId,
            recorded_at = clock.UtcNow,
        });
        sanctionsCase.RecordHrOutcome(outcomeType, detailJson, actorId, clock.UtcNow);
        sanctionsCase.EnsureTeamMember(actorId, "hr");
        var eventType = outcomeType == HrOutcomeType.DcReferral ? AuditEventTypes.DcReferral : AuditEventTypes.HrOutcomeRecorded;
        audit.Record(eventType, AuditTargetTypes.SanctionsCase, sanctionsCase.Id, after: new { outcomeType = outcomeType.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sanctionsCase.ToDto(unmask: true);
    }
}

public sealed record ReferToDcCommand(Guid CaseId, string ReferralReason, string Version) : ICommand<SanctionsCaseDto>;

public sealed class ReferToDcCommandValidator : AbstractValidator<ReferToDcCommand>
{
    public ReferToDcCommandValidator()
    {
        RuleFor(x => x.ReferralReason).NotEmpty().MinimumLength(20);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class ReferToDcCommandHandler(
    ISanctionsCaseRepository cases, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ReferToDcCommand, SanctionsCaseDto>
{
    public async Task<SanctionsCaseDto> Handle(ReferToDcCommand command, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(command.CaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", command.CaseId);
        sanctionsCase.EnsureVersion(command.Version);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        sanctionsCase.ReferToDc(command.ReferralReason, actorId, clock.UtcNow);
        // Keep the referrer on the case team so they retain visibility after the status moves to dc_referral
        // (mirrors the HR-outcome path, which persists the recorder as a team member).
        sanctionsCase.EnsureTeamMember(actorId, "referrer");
        audit.Record(AuditEventTypes.DcReferral, AuditTargetTypes.SanctionsCase, sanctionsCase.Id, payload: new { command.ReferralReason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sanctionsCase.ToDto(unmask: true);
    }
}

public sealed record RecordDcDecisionCommand(Guid CaseId, string Decision, string Rationale, string? VotingRecord, string Version) : ICommand<SanctionsCaseDto>;

public sealed class RecordDcDecisionCommandValidator : AbstractValidator<RecordDcDecisionCommand>
{
    public RecordDcDecisionCommandValidator()
    {
        RuleFor(x => x.Decision).NotEmpty();
        RuleFor(x => x.Rationale).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class RecordDcDecisionCommandHandler(
    ISanctionsCaseRepository cases, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordDcDecisionCommand, SanctionsCaseDto>
{
    public async Task<SanctionsCaseDto> Handle(RecordDcDecisionCommand command, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(command.CaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", command.CaseId);
        sanctionsCase.EnsureVersion(command.Version);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var decision = SanctionsParsing.ParseDcDecision(command.Decision);

        var detailJson = AppJson.Serialize(new
        {
            decision = decision.ToString(),
            detail = command.Rationale,
            voting_record = command.VotingRecord,
            decided_by = actorId,
            decided_at = clock.UtcNow,
        });
        sanctionsCase.RecordDcDecision(decision, detailJson, actorId, clock.UtcNow);
        sanctionsCase.EnsureTeamMember(actorId, "dc");
        audit.Record(AuditEventTypes.DcDecisionRecorded, AuditTargetTypes.SanctionsCase, sanctionsCase.Id, after: new { decision = decision.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sanctionsCase.ToDto(unmask: true);
    }
}

// ---- Close ----

public sealed record CloseCaseCommand(Guid CaseId, string Version) : ICommand<SanctionsCaseDto>;

public sealed class CloseCaseCommandValidator : AbstractValidator<CloseCaseCommand>
{
    public CloseCaseCommandValidator() => RuleFor(x => x.Version).NotEmpty();
}

public sealed class CloseCaseCommandHandler(
    ISanctionsCaseRepository cases, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CloseCaseCommand, SanctionsCaseDto>
{
    public async Task<SanctionsCaseDto> Handle(CloseCaseCommand command, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(command.CaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", command.CaseId);
        sanctionsCase.EnsureVersion(command.Version);
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        sanctionsCase.Close(actorId, clock.UtcNow);
        audit.Record(AuditEventTypes.SanctionsCaseClosed, AuditTargetTypes.SanctionsCase, sanctionsCase.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sanctionsCase.ToDto(unmask: true);
    }
}
