using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Mapping;
using AuditX.Domain.AuditTrail;
using FluentValidation;

namespace AuditX.Application.Identity.MakerChecker;

/// <summary>
/// List pending actions the current user is eligible to check — i.e. excluding actions they made
/// themselves, since a maker may never approve their own action (US-M1-025, BR-M1-008).
/// </summary>
public sealed record ListPendingActionsQuery(string? ActionType) : IQuery<IReadOnlyList<MakerCheckerActionDto>>;

public sealed class ListPendingActionsQueryHandler(IMakerCheckerRepository pending, ICurrentUser currentUser)
    : IQueryHandler<ListPendingActionsQuery, IReadOnlyList<MakerCheckerActionDto>>
{
    public async Task<IReadOnlyList<MakerCheckerActionDto>> Handle(ListPendingActionsQuery query, CancellationToken cancellationToken)
    {
        var actions = await pending.GetPendingAsync(query.ActionType, cancellationToken);
        return actions
            .Where(a => a.MakerUserId != currentUser.UserId)
            .Select(a => a.ToDto())
            .ToArray();
    }
}

/// <summary>
/// Approve a pending action and execute the captured operation atomically: both the status change and
/// the replayed action commit in one transaction, or neither does (US-M1-022). Self-approval is
/// blocked (US-M1-023).
/// </summary>
public sealed record ApproveActionCommand(Guid ActionId) : ICommand<Unit>;

public sealed class ApproveActionCommandHandler(
    IMakerCheckerRepository pending,
    IEnumerable<IPendingActionExecutor> executors,
    ICurrentUser currentUser,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveActionCommand, Unit>
{
    public async Task<Unit> Handle(ApproveActionCommand command, CancellationToken cancellationToken)
    {
        var checkerId = currentUser.UserId ?? throw new UnauthorizedException();

        var action = await pending.GetByIdAsync(command.ActionId, cancellationToken)
            ?? throw new NotFoundException("Pending action", command.ActionId);

        if (action.MakerUserId == checkerId)
        {
            throw new ForbiddenAccessException("A maker may not approve their own action.");
        }

        var executor = executors.FirstOrDefault(e => e.ActionType == action.ActionType)
            ?? throw new ConflictException("mc.no_executor", $"No executor is registered for action type '{action.ActionType}'.");

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Domain guard also enforces self-approval and pending-state invariants.
            action.Approve(checkerId, clock.UtcNow);
            await executor.ExecuteAsync(action.PendingPayloadJson, action.MakerUserId, ct);
            audit.Record(AuditEventTypes.MakerCheckerApproved, AuditTargetTypes.MakerCheckerAction, action.Id,
                payload: new { action.ActionType, action.MakerUserId, checkerId });
            await unitOfWork.SaveChangesAsync(ct);
            return Unit.Value;
        }, cancellationToken);

        await permissions.InvalidateAllAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Reject a pending action with a reason of at least 20 characters (US-M1-024).</summary>
public sealed record RejectActionCommand(Guid ActionId, string Reason) : ICommand<Unit>;

public sealed class RejectActionCommandValidator : AbstractValidator<RejectActionCommand>
{
    public RejectActionCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(20)
            .WithMessage("A rejection reason of at least 20 characters is required.");
    }
}

public sealed class RejectActionCommandHandler(
    IMakerCheckerRepository pending,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RejectActionCommand, Unit>
{
    public async Task<Unit> Handle(RejectActionCommand command, CancellationToken cancellationToken)
    {
        var checkerId = currentUser.UserId ?? throw new UnauthorizedException();

        var action = await pending.GetByIdAsync(command.ActionId, cancellationToken)
            ?? throw new NotFoundException("Pending action", command.ActionId);

        if (action.MakerUserId == checkerId)
        {
            throw new ForbiddenAccessException("A maker may not reject their own action.");
        }

        action.Reject(checkerId, command.Reason, clock.UtcNow);
        audit.Record(AuditEventTypes.MakerCheckerRejected, AuditTargetTypes.MakerCheckerAction, action.Id,
            payload: new { action.ActionType, action.MakerUserId, checkerId, reason = command.Reason });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
