using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Application.Exceptions.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Exceptions.Commands;

internal static class FollowUpParsing
{
    public static ManagementResponseDecision ParseDecision(string? value)
        => EnumExtensions.TryParseSnake<ManagementResponseDecision>(value, out var d)
            ? d
            : throw new DomainException("exception.invalid_response_decision", $"Unknown management-response decision '{value}'.");

    public static VerificationResult ParseVerificationResult(string? value)
        => EnumExtensions.TryParseSnake<VerificationResult>(value, out var r)
            ? r
            : throw new DomainException("exception.invalid_verification_result", $"Unknown verification result '{value}'.");
}

// ---- Management response (P2-B) ----

public sealed record RecordManagementResponseCommand(Guid ExceptionId, string Decision, string Comment, string Version) : ICommand<ExceptionDto>;

public sealed class RecordManagementResponseCommandValidator : AbstractValidator<RecordManagementResponseCommand>
{
    public RecordManagementResponseCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty();
        RuleFor(x => x.Decision).NotEmpty();
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class RecordManagementResponseCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordManagementResponseCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(RecordManagementResponseCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        exception.EnsureVersion(command.Version);

        var decision = FollowUpParsing.ParseDecision(command.Decision);
        exception.RecordManagementResponse(decision, command.Comment, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.ManagementResponseRecorded, AuditTargetTypes.Exception, exception.Id,
            after: new { decision = decision.ToSnake() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

// ---- Follow-up verification (P2-B) ----

public sealed record AddFindingVerificationCommand(Guid ExceptionId, string Result, string? Notes, string Version) : ICommand<ExceptionDto>;

public sealed class AddFindingVerificationCommandValidator : AbstractValidator<AddFindingVerificationCommand>
{
    public AddFindingVerificationCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty();
        RuleFor(x => x.Result).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class AddFindingVerificationCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AddFindingVerificationCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(AddFindingVerificationCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        exception.EnsureVersion(command.Version);

        var result = FollowUpParsing.ParseVerificationResult(command.Result);
        var verification = exception.AddVerification(result, command.Notes, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.FindingVerified, AuditTargetTypes.FindingVerification, verification.Id,
            after: new { command.ExceptionId, result = result.ToSnake() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

// ---- Reopen (P2-B) ----

public sealed record ReopenExceptionCommand(Guid ExceptionId, string Reason, string Version) : ICommand<ExceptionDto>;

public sealed class ReopenExceptionCommandValidator : AbstractValidator<ReopenExceptionCommand>
{
    public ReopenExceptionCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class ReopenExceptionCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ReopenExceptionCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(ReopenExceptionCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        exception.EnsureVersion(command.Version);

        exception.Reopen(command.Reason, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        // The originating checklist item is once again outstanding.
        auditEntity.SetItemException(exception.ChecklistItemId, true);
        audit.Record(AuditEventTypes.ExceptionReopened, AuditTargetTypes.Exception, exception.Id,
            after: new { command.Reason, exception.ReopenCount });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}
