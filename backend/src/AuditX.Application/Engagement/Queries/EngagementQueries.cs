using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Engagement.Dtos;
using AuditX.Application.Execution;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Engagement.Queries;

/// <summary>
/// The caller's permissions, resolved once so the resolver stays a pure function: what they hold anywhere, and where
/// they manage audits (everywhere, the audits a grant names, or inside some org units).
/// </summary>
internal sealed record UserEngagementContext(
    Func<string, bool> Can,
    bool GlobalManage,
    IReadOnlySet<Guid> ManagedAuditIds,
    bool LimitedManage)
{
    public static async Task<UserEngagementContext> LoadAsync(Guid userId, IPermissionResolver permissions, CancellationToken ct)
    {
        var effective = await permissions.GetEffectivePermissionsAsync(userId, ct);
        var held = effective.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var manage = effective.Where(e => e.Key == PermissionKeys.ManageAudit).ToList();
        var globalManage = manage.Any(e => e.ScopeType == PermissionScopeType.Global || e.ScopeValue is null);
        var limitedManage = manage.Any(e => e.ScopeType == PermissionScopeType.OrgUnit && e.ScopeValue is not null);
        var auditIds = manage
            .Where(e => e.ScopeType != PermissionScopeType.OrgUnit && e.ScopeValue is not null)
            .Select(e => Guid.TryParse(e.ScopeValue, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        return new UserEngagementContext(held.Contains, globalManage, auditIds, limitedManage);
    }

    /// <summary>Whose engagements the board lists for this caller.</summary>
    public async Task<EngagementBoardReach> BoardReachAsync(Guid userId, IPermissionResolver permissions, CancellationToken ct)
    {
        IReadOnlyCollection<Guid> orgUnits = !GlobalManage && LimitedManage
            ? await permissions.GetLimitedOrgUnitsAsync(userId, PermissionKeys.ManageAudit, ct)
            : [];
        return new EngagementBoardReach(userId, GlobalManage, ManagedAuditIds, orgUnits);
    }
}

/// <summary>What the journey reads about an audit beyond the aggregate.</summary>
internal sealed record AuditJourneyFacts(
    IReadOnlyList<AuditException> OpenExceptions, AuditReportState Report, EngagementRecordFacts Records);

internal static class EngagementFacts
{
    /// <summary>
    /// The facts the journey needs beyond each aggregate: the open findings, where the report stands, and the
    /// engagement records (the letter's state, the minuted meetings). The number of queries does not grow with the
    /// number of audits. <paramref name="managesAudit"/> says whether the caller manages an audit.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, AuditJourneyFacts>> LoadAsync(
        IReadOnlyCollection<Audit> audits,
        bool requireIndependence,
        bool requireProgrammeApproval,
        Func<Audit, bool> managesAudit,
        IExceptionRepository exceptions,
        IReportRepository reports,
        IEngagementLetterRepository letters,
        IAuditMeetingRepository meetings,
        CancellationToken ct)
    {
        if (audits.Count == 0)
        {
            return new Dictionary<Guid, AuditJourneyFacts>();
        }

        var ids = audits.Select(a => a.Id).ToArray();
        var open = (await exceptions.ListByAuditsAsync(ids, EngagementJourneyResolver.OpenExceptionStatuses, ct))
            .ToLookup(e => e.AuditId);
        var reportStates = (await reports.GetAuditReportStatesAsync(ids, ct)).ToDictionary(s => s.AuditId);

        // A self-assessment keeps no engagement records.
        var withRecords = audits.Where(a => !a.IsSelfAssessment).Select(a => a.Id).ToArray();
        var letterStates = await letters.GetStatusesByAuditAsync(withRecords, ct);
        var minuted = (await meetings.ListRecordedAsync(withRecords, ct)).ToLookup(m => m.AuditId, m => m.Kind);

        return audits.ToDictionary(a => a.Id, a =>
        {
            var report = reportStates.GetValueOrDefault(a.Id) ?? AuditReportState.None(a.Id);
            var records = a.IsSelfAssessment
                ? EngagementRecordFacts.None
                : new EngagementRecordFacts(
                    requireIndependence,
                    letterStates.TryGetValue(a.Id, out var letter) ? letter : (EngagementLetterStatus?)null,
                    minuted[a.Id].Contains(MeetingKind.Kickoff),
                    minuted[a.Id].Contains(MeetingKind.Exit),
                    report.DraftCompleted,
                    managesAudit(a),
                    RequireProgrammeApproval: requireProgrammeApproval);
            return new AuditJourneyFacts([.. open[a.Id]], report, records);
        });
    }
}

// ---- Per-engagement journey ------------------------------------------------

public sealed record GetEngagementJourneyQuery(Guid AuditId) : IQuery<EngagementJourneyDto>;

public sealed class GetEngagementJourneyQueryHandler(
    IAuditRepository audits,
    IExceptionRepository exceptions,
    IReportRepository reports,
    IEngagementLetterRepository letters,
    IAuditMeetingRepository meetings,
    IInstitutionSettingsRepository settings,
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
        var manages = await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken);
        var rules = await settings.GetAsync(cancellationToken);
        var facts = (await EngagementFacts.LoadAsync(
            [audit], rules.RequireIndependenceDeclaration, rules.RequireProgrammeApproval, _ => manages, exceptions, reports, letters, meetings, cancellationToken))[audit.Id];

        var onTeam = audit.TeamMembers.Any(m => m.IsActive && m.UserId == uid);
        return EngagementJourneyResolver.Resolve(
            audit, facts.OpenExceptions, facts.Report, uid, ctx.Can, onTeam || manages, facts.Records);
    }
}

// ---- Portfolio board -------------------------------------------------------

public sealed record ListMyEngagementsQuery(string? Status) : IQuery<IReadOnlyList<EngagementBoardItemDto>>;

/// <summary>
/// The caller's engagements: the audits they are on the team of (the auditee included) and those they manage, each
/// with its journey. The newest <see cref="Max"/> are listed, those waiting on the caller first.
/// </summary>
public sealed class ListMyEngagementsQueryHandler(
    IAuditRepository audits,
    IExceptionRepository exceptions,
    IReportRepository reports,
    IEngagementLetterRepository letters,
    IAuditMeetingRepository meetings,
    IInstitutionSettingsRepository settings,
    IPermissionResolver permissions,
    ICurrentUser currentUser)
    : IQueryHandler<ListMyEngagementsQuery, IReadOnlyList<EngagementBoardItemDto>>
{
    /// <summary>The most audits one board lists; someone who manages every audit sees the newest.</summary>
    public const int Max = 500;

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
        var reach = await ctx.BoardReachAsync(uid, permissions, cancellationToken);
        var rows = await audits.ListForEngagementBoardAsync(reach, status, Max, cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var unitOf = rows.ToDictionary(r => r.Audit.Id, r => r.OrgUnitId);
        var rules = await settings.GetAsync(cancellationToken);
        var facts = await EngagementFacts.LoadAsync(
            [.. rows.Select(r => r.Audit)], rules.RequireIndependenceDeclaration, rules.RequireProgrammeApproval, a => reach.Manages(a.Id, unitOf[a.Id]),
            exceptions, reports, letters, meetings, cancellationToken);

        var items = new List<EngagementBoardItemDto>(rows.Count);
        foreach (var audit in rows.Select(r => r.Audit))
        {
            var f = facts[audit.Id];
            var journey = EngagementJourneyResolver.Resolve(
                audit, f.OpenExceptions, f.Report, uid, ctx.Can, onTeamOrManager: true, f.Records);

            items.Add(new EngagementBoardItemDto(
                journey.AuditId, journey.Name, journey.AuditType, journey.Status, journey.Stage,
                journey.ProgressPercent, journey.OpenExceptionCount, journey.Version,
                WaitingOnMe: journey.NextActions.Count > 0, journey.NextActions, CreatedAt: journey.CreatedAt));
        }

        // Surface the engagements that need the user first, then by name.
        return [.. items.OrderByDescending(i => i.WaitingOnMe).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
