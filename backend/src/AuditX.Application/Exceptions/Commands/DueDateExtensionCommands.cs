using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Application.Exceptions.Mapping;
using AuditX.Domain.AuditTrail;
using FluentValidation;

namespace AuditX.Application.Exceptions.Commands;

// ---- Request more time ----

/// <summary>
/// Asks for a later due date: the finding's own target date when <c>ActionId</c> is null, otherwise that action's. Asked
/// by the people remediating it: the finding's owner, the action's owner, or a manager of the audit on their behalf.
/// </summary>
public sealed record RequestDueDateExtensionCommand(Guid ExceptionId, Guid? ActionId, DateOnly RequestedDate, string Reason, string Version)
    : ICommand<ExceptionDto>;

public sealed class RequestDueDateExtensionCommandValidator : AbstractValidator<RequestDueDateExtensionCommand>
{
    public RequestDueDateExtensionCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty();
        RuleFor(x => x.ActionId).Must(id => id is null || id != Guid.Empty);
        RuleFor(x => x.RequestedDate).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class RequestDueDateExtensionCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RequestDueDateExtensionCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(RequestDueDateExtensionCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadForOwnerAsync(
            exceptions, audits, permissions, currentUser, command.ExceptionId, command.ActionId, cancellationToken);
        exception.EnsureVersion(command.Version);

        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var request = exception.RequestExtension(command.ActionId, command.RequestedDate, command.Reason, userId, today, clock.UtcNow);
        audit.Record(AuditEventTypes.DueDateExtensionRequested, AuditTargetTypes.Exception, exception.Id,
            after: new { requestId = request.Id, request.ActionId, request.CurrentDate, request.RequestedDate, request.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(today);
    }
}

// ---- Decide ----

/// <summary>
/// Approves or rejects a request for more time. Audit-side, like approving the plan (never the audit's auditee), and
/// never by the person who asked. A rejection needs a reason; an approval may carry a note.
/// </summary>
public sealed record DecideDueDateExtensionCommand(Guid ExceptionId, Guid RequestId, bool Approve, string? Note, string Version)
    : ICommand<ExceptionDto>;

public sealed class DecideDueDateExtensionCommandValidator : AbstractValidator<DecideDueDateExtensionCommand>
{
    public DecideDueDateExtensionCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Approve);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class DecideDueDateExtensionCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DecideDueDateExtensionCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(DecideDueDateExtensionCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadForAuditorAsync(
            exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var request = exception.ExtensionRequests.FirstOrDefault(r => r.Id == command.RequestId)
            ?? throw new NotFoundException("Extension request", command.RequestId);

        // Segregation of duties: nobody grants themselves more time.
        if (request.RequestedBy == userId)
        {
            throw new ForbiddenAccessException("You cannot decide your own request for more time.");
        }

        var beforeTarget = exception.TargetDate;
        if (command.Approve)
        {
            exception.ApproveExtension(command.RequestId, userId, command.Note, clock.UtcNow);
        }
        else
        {
            exception.RejectExtension(command.RequestId, userId, command.Note ?? string.Empty, clock.UtcNow);
        }

        audit.Record(
            command.Approve ? AuditEventTypes.DueDateExtensionApproved : AuditEventTypes.DueDateExtensionRejected,
            AuditTargetTypes.Exception, exception.Id,
            before: new { targetDate = beforeTarget },
            after: new
            {
                requestId = request.Id, request.ActionId, request.RequestedDate, note = request.DecisionNote,
                targetDate = exception.TargetDate,
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

// ---- Withdraw ----

/// <summary>The person who asked takes their request back before it is decided.</summary>
public sealed record WithdrawDueDateExtensionCommand(Guid ExceptionId, Guid RequestId, string Version) : ICommand<ExceptionDto>;

public sealed class WithdrawDueDateExtensionCommandValidator : AbstractValidator<WithdrawDueDateExtensionCommand>
{
    public WithdrawDueDateExtensionCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class WithdrawDueDateExtensionCommandHandler(
    IExceptionRepository exceptions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<WithdrawDueDateExtensionCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(WithdrawDueDateExtensionCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken)
            ?? throw new NotFoundException("Exception", command.ExceptionId);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var request = exception.ExtensionRequests.FirstOrDefault(r => r.Id == command.RequestId)
            ?? throw new NotFoundException("Extension request", command.RequestId);

        // Only the person who asked; anyone else learns nothing more than that it is not theirs.
        if (request.RequestedBy != userId)
        {
            throw new ForbiddenAccessException("Only the person who asked for more time can withdraw the request.");
        }

        exception.EnsureVersion(command.Version);
        exception.WithdrawExtension(command.RequestId, userId, clock.UtcNow);
        audit.Record(AuditEventTypes.DueDateExtensionWithdrawn, AuditTargetTypes.Exception, exception.Id,
            after: new { requestId = request.Id, request.ActionId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}
