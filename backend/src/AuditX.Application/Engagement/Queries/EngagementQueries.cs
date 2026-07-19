using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Engagement.Dtos;
using AuditX.Application.Execution;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Engagement.Queries;

/// <summary>Resolves the caller's role/permission context once, so the resolver stays a pure function.</summary>
internal sealed record UserEngagementContext(
    Func<string, bool> Can,
    bool GlobalManage,
    IReadOnlySet<string> ManageAuditScopes)
{
    public bool ManagesOrIsMemberOf(Audit audit, Guid userId) =>
        audit.TeamMembers.Any(m => m.IsActive && m.UserId == userId)
        || GlobalManage
        || ManageAuditScopes.Contains(audit.Id.ToString());

    public static async Task<UserEngagementContext> LoadAsync(Guid userId, IPermissionResolver permissions, CancellationToken ct)
    {
        var effective = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var held = effective.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var globalManage = effective.Any(e => e.Key == PermissionKeys.ManageAudit
            && (e.ScopeType == Domain.Enums.PermissionScopeType.Global || e.ScopeValue is null));
        var scopes = effective
            .Where(e => e.Key == PermissionKeys.ManageAudit && e.ScopeValue is not null)
            .Select(e => e.ScopeValue!)
            .ToHashSet(StringComparer.Ordinal);
        return new UserEngagementContext(held.Contains, globalManage, scopes);
    }
}

internal static class EngagementFacts
{
    /// <summary>Loads the per-audit facts the resolver needs beyond the aggregate: open exceptions + report state.</summary>
    public static async Task<(IReadOnlyList<AuditException> Exceptions, bool HasCompletedReport, bool HasDistributedReport)>
        LoadAsync(Guid auditId, IExceptionRepository exceptions, IReportRepository reports, CancellationToken ct)
    {
        var ex = await exceptions.ListByAuditAsync(auditId, null, ct);
        var reportPage = await reports.ListByAuditAsync(auditId, PageSpec.Of(1, 100), ct);
        var hasCompleted = reportPage.Items.Any(r => r.Status == ReportStatus.Completed);
        var hasDistributed = reportPage.Items.Any(r => r.Distributions.Count > 0);
        return (ex, hasCompleted, hasDistributed);
    }
}

// ---- Per-engagement journey ------------------------------------------------

public sealed record GetEngagementJourneyQuery(Guid AuditId) : IQuery<EngagementJourneyDto>;

public sealed class GetEngagementJourneyQueryHandler(
    IAuditRepository audits,
    IExceptionRepository exceptions,
    IReportRepository reports,
    IPermissionResolver permissions,
    ICurrentUser currentUser)
    : IQueryHandler<GetEngagementJourneyQuery, EngagementJourneyDto>
{
    public async Task<EngagementJourneyDto> Handle(GetEngagementJourneyQuery query, CancellationToken cancellationToken)
    {
        var audit = await audits.GetByIdAsync(query.AuditId, cancellationToken)
            ?? throw new NotFoundException("Audit", query.AuditId);
        await AuditAccess.EnsureCanAccessAsync(audit, currentUser.UserId, permissions, cancellationToken);
        var uid = currentUser.UserId!.Value;

        var ctx = await UserEngagementContext.LoadAsync(uid, permissions, cancellationToken);
        var (ex, hasCompleted, hasDistributed) = await EngagementFacts.LoadAsync(audit.Id, exceptions, reports, cancellationToken);

        return EngagementJourneyResolver.Resolve(
            audit, ex, hasCompleted, hasDistributed, uid, ctx.Can, ctx.ManagesOrIsMemberOf(audit, uid));
    }
}

// ---- Portfolio board -------------------------------------------------------

public sealed record ListMyEngagementsQuery(string? Status) : IQuery<IReadOnlyList<EngagementBoardItemDto>>;

public sealed class ListMyEngagementsQueryHandler(
    IAuditRepository audits,
    IExceptionRepository exceptions,
    IReportRepository reports,
    IPermissionResolver permissions,
    ICurrentUser currentUser)
    : IQueryHandler<ListMyEngagementsQuery, IReadOnlyList<EngagementBoardItemDto>>
{
    // The board is scoped to a user's own engagements (a small set); we cap the underlying scan defensively.
    private const int Scan = 500;

    public async Task<IReadOnlyList<EngagementBoardItemDto>> Handle(ListMyEngagementsQuery query, CancellationToken cancellationToken)
    {
        var uid = currentUser.UserId ?? throw new UnauthorizedException();

        AuditStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = Enum.TryParse<AuditStatus>(query.Status.Replace("_", string.Empty), ignoreCase: true, out var parsed)
                ? parsed
                : throw new ConflictException("invalid_status", $"Unknown audit status '{query.Status}'.");
        }

        var ctx = await UserEngagementContext.LoadAsync(uid, permissions, cancellationToken);
        var page = await audits.SearchAsync(status, null, null, null, null, PageSpec.Of(1, Scan), cancellationToken);

        var items = new List<EngagementBoardItemDto>();
        foreach (var audit in page.Items)
        {
            if (!ctx.ManagesOrIsMemberOf(audit, uid))
            {
                continue; // not the user's engagement
            }

            var (ex, hasCompleted, hasDistributed) = await EngagementFacts.LoadAsync(audit.Id, exceptions, reports, cancellationToken);
            var journey = EngagementJourneyResolver.Resolve(audit, ex, hasCompleted, hasDistributed, uid, ctx.Can, onTeamOrManager: true);

            items.Add(new EngagementBoardItemDto(
                journey.AuditId, journey.Name, journey.AuditType, journey.Status, journey.Stage,
                journey.ProgressPercent, journey.OpenExceptionCount, journey.Version,
                WaitingOnMe: journey.NextActions.Count > 0, journey.NextActions));
        }

        // Surface the engagements that need the user first, then by name.
        return [.. items.OrderByDescending(i => i.WaitingOnMe).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
