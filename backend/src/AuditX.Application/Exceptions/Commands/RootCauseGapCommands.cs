using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Concurrency;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using FluentValidation;

namespace AuditX.Application.Exceptions.Commands;

internal static class RootCauseGapMapping
{
    public static RootCauseGapDto ToDto(this RootCauseGap g, IReadOnlyList<RootCauseGapLinkedExceptionRow> linked) => new(
        g.Id, g.Title, g.Description, g.Category, g.OwnerUserId, g.TargetDate, g.Status.ToSnake(),
        g.IdentifiedByUserId, g.IdentifiedAt, g.ClosureRationale, g.ClosedByUserId, g.ClosedAt,
        RowVersionToken.Encode(g.Version),
        linked.Select(l => new RootCauseGapLinkedExceptionDto(l.LinkId, l.ExceptionId, l.Title, l.Severity.ToSnake(), l.Status.ToSnake())).ToArray(),
        // Open actions first (by soonest due date, undated last), then completed — the plan reads as a worklist.
        g.Remediations
            .OrderBy(r => r.Status == RootCauseGapRemediationStatus.Completed)
            .ThenBy(r => r.DueDate ?? DateOnly.MaxValue)
            .ThenBy(r => r.CreatedAt)
            .Select(r => new RootCauseGapRemediationDto(
                r.Id, r.Description, r.OwnerUserId, r.DueDate, r.Status.ToSnake(),
                r.CompletionNote, r.CompletedByUserId, r.CompletedAt, r.CreatedAt))
            .ToArray());

    public static RootCauseGapListItemDto ToListItemDto(this RootCauseGap g) => new(
        g.Id, g.Title, g.Category, g.OwnerUserId, g.TargetDate, g.Status.ToSnake(), g.IdentifiedAt, g.Links.Count);

    public static void EnsureVersion(this RootCauseGap gap, string version)
    {
        if (RowVersionToken.Encode(gap.Version) != version)
        {
            throw new ConflictException("root_cause_gap.concurrency_conflict", "This root-cause gap was changed by someone else. Reload and try again.");
        }
    }
}

public sealed record CreateRootCauseGapCommand(string Title, string? Description, string? Category, Guid OwnerUserId, DateOnly? TargetDate) : ICommand<RootCauseGapDto>;

public sealed class CreateRootCauseGapCommandValidator : AbstractValidator<CreateRootCauseGapCommand>
{
    public CreateRootCauseGapCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.OwnerUserId).NotEmpty();
    }
}

public sealed class CreateRootCauseGapCommandHandler(IRootCauseGapRepository gaps, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRootCauseGapCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(CreateRootCauseGapCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var gap = RootCauseGap.Open(command.Title, command.Description, command.Category, command.OwnerUserId, command.TargetDate, userId, clock.UtcNow);
        gaps.Add(gap);
        audit.Record(AuditEventTypes.RootCauseGapOpened, AuditTargetTypes.RootCauseGap, gap.Id, after: new { gap.Title, gap.OwnerUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto([]);
    }
}

public sealed record UpdateRootCauseGapCommand(Guid Id, string Title, string? Description, string? Category, Guid OwnerUserId, DateOnly? TargetDate, string Version) : ICommand<RootCauseGapDto>;

public sealed class UpdateRootCauseGapCommandHandler(IRootCauseGapRepository gaps, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRootCauseGapCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(UpdateRootCauseGapCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.Id);
        gap.EnsureVersion(command.Version);
        gap.UpdateDetails(command.Title, command.Description, command.Category, command.OwnerUserId, command.TargetDate);
        audit.Record(AuditEventTypes.RootCauseGapUpdated, AuditTargetTypes.RootCauseGap, gap.Id, after: new { gap.Title });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record CloseRootCauseGapCommand(Guid Id, string Rationale, string Version) : ICommand<RootCauseGapDto>;

public sealed class CloseRootCauseGapCommandHandler(IRootCauseGapRepository gaps, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CloseRootCauseGapCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(CloseRootCauseGapCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.Id);
        gap.EnsureVersion(command.Version);
        gap.Close(command.Rationale, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.RootCauseGapClosed, AuditTargetTypes.RootCauseGap, gap.Id, after: new { command.Rationale });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record ReopenRootCauseGapCommand(Guid Id, string Version) : ICommand<RootCauseGapDto>;

public sealed class ReopenRootCauseGapCommandHandler(IRootCauseGapRepository gaps, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ReopenRootCauseGapCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(ReopenRootCauseGapCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.Id);
        gap.EnsureVersion(command.Version);
        gap.Reopen();
        audit.Record(AuditEventTypes.RootCauseGapReopened, AuditTargetTypes.RootCauseGap, gap.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record LinkExceptionToGapCommand(Guid GapId, Guid ExceptionId, string Version) : ICommand<RootCauseGapDto>;

public sealed class LinkExceptionToGapCommandHandler(
    IRootCauseGapRepository gaps, IExceptionRepository exceptions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<LinkExceptionToGapCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(LinkExceptionToGapCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.GapId, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.GapId);
        gap.EnsureVersion(command.Version);
        _ = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);

        gap.LinkException(command.ExceptionId, currentUser.UserId ?? Guid.Empty);
        audit.Record(AuditEventTypes.RootCauseGapExceptionLinked, AuditTargetTypes.RootCauseGap, gap.Id, payload: new { command.ExceptionId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record UnlinkExceptionFromGapCommand(Guid GapId, Guid ExceptionId, string Version) : ICommand<RootCauseGapDto>;

public sealed class UnlinkExceptionFromGapCommandHandler(IRootCauseGapRepository gaps, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UnlinkExceptionFromGapCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(UnlinkExceptionFromGapCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.GapId, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.GapId);
        gap.EnsureVersion(command.Version);
        gap.UnlinkException(command.ExceptionId);
        audit.Record(AuditEventTypes.RootCauseGapExceptionUnlinked, AuditTargetTypes.RootCauseGap, gap.Id, payload: new { command.ExceptionId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

// ---- Remediation plan actions ----

public sealed record AddGapRemediationCommand(Guid GapId, string Description, Guid OwnerUserId, DateOnly? DueDate, string Version) : ICommand<RootCauseGapDto>;

public sealed class AddGapRemediationCommandValidator : AbstractValidator<AddGapRemediationCommand>
{
    public AddGapRemediationCommandValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.OwnerUserId).NotEmpty();
    }
}

public sealed class AddGapRemediationCommandHandler(IRootCauseGapRepository gaps, IUserRepository users, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AddGapRemediationCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(AddGapRemediationCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.GapId, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.GapId);
        gap.EnsureVersion(command.Version);
        _ = await users.GetByIdAsync(command.OwnerUserId, cancellationToken) ?? throw new NotFoundException("User", command.OwnerUserId);

        var item = gap.AddRemediation(command.Description, command.OwnerUserId, command.DueDate, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.RootCauseGapRemediationAdded, AuditTargetTypes.RootCauseGap, gap.Id, payload: new { item.Id, command.OwnerUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record UpdateGapRemediationCommand(Guid GapId, Guid RemediationId, string Description, Guid OwnerUserId, DateOnly? DueDate, string Version) : ICommand<RootCauseGapDto>;

public sealed class UpdateGapRemediationCommandValidator : AbstractValidator<UpdateGapRemediationCommand>
{
    public UpdateGapRemediationCommandValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.OwnerUserId).NotEmpty();
    }
}

public sealed class UpdateGapRemediationCommandHandler(IRootCauseGapRepository gaps, IUserRepository users, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateGapRemediationCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(UpdateGapRemediationCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.GapId, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.GapId);
        gap.EnsureVersion(command.Version);
        _ = await users.GetByIdAsync(command.OwnerUserId, cancellationToken) ?? throw new NotFoundException("User", command.OwnerUserId);

        gap.UpdateRemediation(command.RemediationId, command.Description, command.OwnerUserId, command.DueDate);
        audit.Record(AuditEventTypes.RootCauseGapRemediationUpdated, AuditTargetTypes.RootCauseGap, gap.Id, payload: new { command.RemediationId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record CompleteGapRemediationCommand(Guid GapId, Guid RemediationId, string? Note, string Version) : ICommand<RootCauseGapDto>;

public sealed class CompleteGapRemediationCommandHandler(IRootCauseGapRepository gaps, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CompleteGapRemediationCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(CompleteGapRemediationCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.GapId, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.GapId);
        gap.EnsureVersion(command.Version);
        gap.CompleteRemediation(command.RemediationId, command.Note, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.RootCauseGapRemediationCompleted, AuditTargetTypes.RootCauseGap, gap.Id, payload: new { command.RemediationId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record ReopenGapRemediationCommand(Guid GapId, Guid RemediationId, string Version) : ICommand<RootCauseGapDto>;

public sealed class ReopenGapRemediationCommandHandler(IRootCauseGapRepository gaps, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ReopenGapRemediationCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(ReopenGapRemediationCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.GapId, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.GapId);
        gap.EnsureVersion(command.Version);
        gap.ReopenRemediation(command.RemediationId);
        audit.Record(AuditEventTypes.RootCauseGapRemediationReopened, AuditTargetTypes.RootCauseGap, gap.Id, payload: new { command.RemediationId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record RemoveGapRemediationCommand(Guid GapId, Guid RemediationId, string Version) : ICommand<RootCauseGapDto>;

public sealed class RemoveGapRemediationCommandHandler(IRootCauseGapRepository gaps, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveGapRemediationCommand, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(RemoveGapRemediationCommand command, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(command.GapId, cancellationToken) ?? throw new NotFoundException("Root-cause gap", command.GapId);
        gap.EnsureVersion(command.Version);
        gap.RemoveRemediation(command.RemediationId);
        audit.Record(AuditEventTypes.RootCauseGapRemediationRemoved, AuditTargetTypes.RootCauseGap, gap.Id, payload: new { command.RemediationId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, cancellationToken: cancellationToken));
    }
}

public sealed record ListRootCauseGapsQuery(string? Status, string? Search, int? Page, int? PageSize) : IQuery<PagedResult<RootCauseGapListItemDto>>;

/// <summary>
/// The root-cause gap register is a portfolio view of systemic weaknesses across audits. A caller with portfolio
/// visibility (ViewAudits — Auditor / Audit Manager / Administrator) sees all of it. Anyone else holding ViewExceptions
/// (an auditee) sees only the gaps they answer for — as the gap's owner or the owner of one of its remediation actions.
/// </summary>
public sealed class ListRootCauseGapsQueryHandler(IRootCauseGapRepository gaps, ICurrentUser currentUser, IPermissionResolver permissions)
    : IQueryHandler<ListRootCauseGapsQuery, PagedResult<RootCauseGapListItemDto>>
{
    public async Task<PagedResult<RootCauseGapListItemDto>> Handle(ListRootCauseGapsQuery query, CancellationToken cancellationToken)
    {
        var participant = await ExceptionAccess.ResolveParticipantScopeAsync(currentUser.UserId, permissions, cancellationToken);
        RootCauseGapStatus? status = query.Status?.ToLowerInvariant() switch
        {
            null or "" or "all" => null,
            "open" => RootCauseGapStatus.Open,
            "closed" => RootCauseGapStatus.Closed,
            _ => throw new ConflictException("root_cause_gap.invalid_status", $"Unknown status filter '{query.Status}'."),
        };
        var result = await gaps.SearchAsync(status, query.Search, PageSpec.Of(query.Page, query.PageSize), participant, cancellationToken);
        return result.Map(g => g.ToListItemDto());
    }
}

public sealed record GetRootCauseGapQuery(Guid Id) : IQuery<RootCauseGapDto>;

public sealed class GetRootCauseGapQueryHandler(IRootCauseGapRepository gaps, ICurrentUser currentUser, IPermissionResolver permissions)
    : IQueryHandler<GetRootCauseGapQuery, RootCauseGapDto>
{
    public async Task<RootCauseGapDto> Handle(GetRootCauseGapQuery query, CancellationToken cancellationToken)
    {
        var gap = await gaps.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Root-cause gap", query.Id);

        // Same scope as the register: without portfolio visibility, only a gap the caller answers for — and within
        // it, only the linked findings they could open themselves (never other areas' finding titles).
        var participant = await ExceptionAccess.ResolveParticipantScopeAsync(currentUser.UserId, permissions, cancellationToken);
        if (participant is { } uid && gap.OwnerUserId != uid && !gap.Remediations.Any(r => r.OwnerUserId == uid))
        {
            throw new ForbiddenAccessException("You can only open a root-cause gap you own or hold a remediation action on.");
        }

        return gap.ToDto(await gaps.ListLinkedExceptionsAsync(gap.Id, participant, cancellationToken));
    }
}
