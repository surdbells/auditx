using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Sanctions.Dtos;
using AuditX.Application.Sanctions.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;

namespace AuditX.Application.Sanctions.Queries;

internal static class SanctionsStatusParsing
{
    public static SanctionsCaseStatus? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<SanctionsCaseStatus>(value.Replace("_", string.Empty), ignoreCase: true, out var s)
            ? s
            : throw new ConflictException("sanctions.invalid_status", $"Unknown status '{value}'.");
    }
}

// ---- List (always masks subject — A1) ----

public sealed record ListSanctionsCasesQuery(string? Status, string? Search, string? Cursor, int? Limit) : IQuery<CursorPage<SanctionsCaseListDto>>;

public sealed class ListSanctionsCasesQueryHandler(ISanctionsCaseRepository cases)
    : IQueryHandler<ListSanctionsCasesQuery, CursorPage<SanctionsCaseListDto>>
{
    public async Task<CursorPage<SanctionsCaseListDto>> Handle(ListSanctionsCasesQuery query, CancellationToken cancellationToken)
    {
        var status = SanctionsStatusParsing.Parse(query.Status);
        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await cases.ListPagedAsync(status, query.Search, page, cancellationToken);
        return new CursorPage<SanctionsCaseListDto>(result.Items.Select(c => c.ToListDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}

// ---- Detail (unmasks for team + records subject_identity_exposed) ----

public sealed record GetSanctionsCaseByIdQuery(Guid Id) : IQuery<SanctionsCaseDto>;

public sealed class GetSanctionsCaseByIdQueryHandler(
    ISanctionsCaseRepository cases, ISanctionsAppealRepository appeals, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : IQueryHandler<GetSanctionsCaseByIdQuery, SanctionsCaseDto>
{
    public async Task<SanctionsCaseDto> Handle(GetSanctionsCaseByIdQuery query, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Sanctions case", query.Id);
        var unmask = await SanctionsAccess.IsCaseTeamMemberAsync(sanctionsCase, currentUser.UserId, permissions, cancellationToken);

        if (unmask && sanctionsCase.SubjectUserId is not null)
        {
            // Every deliberate detail view that exposes the subject is auditable (US-M7-016).
            audit.Record(AuditEventTypes.SubjectIdentityExposed, AuditTargetTypes.SanctionsCase, sanctionsCase.Id,
                payload: new { viewerUserId = currentUser.UserId });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var dto = sanctionsCase.ToDto(unmask);

        // Surface the latest appeal id/status so the case detail can route a decision to the correct appeal.
        var latestAppeal = await appeals.GetLatestByCaseAsync(sanctionsCase.Id, cancellationToken);
        if (latestAppeal is not null)
        {
            dto = dto with
            {
                LatestAppealId = latestAppeal.Id,
                LatestAppealStatus = latestAppeal.Status.ToSnake(),
                LatestAppealVersion = Convert.ToBase64String(latestAppeal.Version ?? []),
            };
        }

        return dto;
    }
}

// ---- DC queue (referred cases only) ----

public sealed record ListDcQueueQuery(string? Cursor, int? Limit) : IQuery<CursorPage<SanctionsCaseListDto>>;

public sealed class ListDcQueueQueryHandler(ISanctionsCaseRepository cases)
    : IQueryHandler<ListDcQueueQuery, CursorPage<SanctionsCaseListDto>>
{
    public async Task<CursorPage<SanctionsCaseListDto>> Handle(ListDcQueueQuery query, CancellationToken cancellationToken)
    {
        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await cases.ListReferredAsync(page, cancellationToken);
        return new CursorPage<SanctionsCaseListDto>(result.Items.Select(c => c.ToListDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}

// ---- Active grid ----

public sealed record GetActiveGridQuery : IQuery<SanctionsGridVersionDto?>;

public sealed class GetActiveGridQueryHandler(ISanctionsGridRepository grids)
    : IQueryHandler<GetActiveGridQuery, SanctionsGridVersionDto?>
{
    public async Task<SanctionsGridVersionDto?> Handle(GetActiveGridQuery query, CancellationToken cancellationToken)
    {
        var grid = await grids.GetActiveAsync(cancellationToken);
        return grid?.ToDto();
    }
}
