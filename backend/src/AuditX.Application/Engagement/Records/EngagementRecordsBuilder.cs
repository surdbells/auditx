using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits;
using AuditX.Application.Common.Concurrency;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Models;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Engagement;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;

namespace AuditX.Application.Engagement.Records;

/// <summary>Builds an audit's <see cref="EngagementRecordsDto"/> for one caller, applying what each may see.</summary>
internal static class EngagementRecordsBuilder
{
    /// <summary>Audit states in which the team declares independence and managers decide on conflicts.</summary>
    internal static readonly AuditStatus[] IndependenceOpen = [AuditStatus.Draft, AuditStatus.Planned, AuditStatus.InProgress, AuditStatus.UnderReview];

    /// <summary>Audit states in which the engagement letter is written and issued (before and during fieldwork).</summary>
    internal static readonly AuditStatus[] LetterOpen = [AuditStatus.Draft, AuditStatus.Planned, AuditStatus.InProgress];

    public static async Task<EngagementRecordsDto> BuildAsync(
        Audit audit,
        Guid userId,
        IEngagementLetterRepository letters,
        IAuditMeetingRepository meetings,
        IReportRepository reports,
        IInstitutionSettingsRepository settings,
        IPermissionResolver permissions,
        CancellationToken cancellationToken)
    {
        var isAuditee = audit.IsAuditee(userId);
        var canManage = await CanManageAsync(audit, userId, permissions, cancellationToken);
        var letter = await letters.GetByAuditAsync(audit.Id, cancellationToken);
        var meetingRecords = await meetings.ListByAuditAsync(audit.Id, cancellationToken);

        // The auditee sees a letter once it has been issued to them, never a draft.
        var visibleLetter = letter is null || (isAuditee && letter.Status == EngagementLetterStatus.Draft) ? null : letter;

        IReadOnlyList<Report> auditReports = [];
        if (canManage || meetingRecords.Any(m => m.ReportId is not null))
        {
            auditReports = (await reports.ListByAuditAsync(audit.Id, PageSpec.Of(1, 100), cancellationToken: cancellationToken)).Items;
        }

        IndependenceDto? independence = null;
        if (!audit.IsSelfAssessment && !isAuditee)
        {
            var required = (await settings.GetAsync(cancellationToken)).RequireIndependenceDeclaration;
            var canDecide = IndependenceOpen.Contains(audit.Status)
                && await permissions.HasPermissionAsync(userId, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken);
            independence = ToIndependence(audit, userId, required, canDecide);
        }

        return new EngagementRecordsDto(
            audit.Id,
            audit.IsSelfAssessment,
            CanManage: canManage && !audit.IsSelfAssessment,
            IsAuditee: isAuditee,
            CanAcknowledge: isAuditee && letter is { Status: EngagementLetterStatus.Issued } && audit.Status != AuditStatus.Cancelled,
            visibleLetter is null ? null : ToDto(visibleLetter),
            independence,
            [.. meetingRecords.Select(m => ToDto(m, auditReports))],
            canManage
                ? [.. auditReports
                    .Where(r => r.Stage == ReportStage.Draft && r.Status == ReportStatus.Completed)
                    .OrderByDescending(r => r.VersionNumber)
                    .Select(r => new DraftReportOptionDto(r.Id, r.VersionNumber, r.CompletedAt))]
                : [],
            RowVersionToken.Encode(audit.Version));
    }

    /// <summary>The same rule as <see cref="AuditManageAuthorization.EnsureCanManageAsync"/>, as a yes or no.</summary>
    public static async Task<bool> CanManageAsync(Audit audit, Guid userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        try
        {
            await AuditManageAuthorization.EnsureCanManageAsync(audit, userId, permissions, cancellationToken);
            return true;
        }
        catch (ForbiddenAccessException)
        {
            return false;
        }
    }

    private static IndependenceDto ToIndependence(Audit audit, Guid userId, bool required, bool canDecide)
    {
        var members = audit.TeamMembers
            .Where(m => m.IsActive && !audit.IsAuditee(m.UserId))
            .GroupBy(m => m.UserId)
            .Select(g => g.First())
            .Select(m =>
            {
                var declaration = audit.DeclarationOf(m.UserId);
                var seesDetails = canDecide || m.UserId == userId;
                return new IndependenceMemberDto(
                    m.UserId,
                    m.TeamRole.ToSnake(),
                    declaration is null ? "pending" : declaration.Status.ToSnake(),
                    declaration?.Id,
                    declaration?.DeclaredAt,
                    seesDetails ? declaration?.ConflictDetails : null,
                    declaration?.ReviewedBy,
                    declaration?.ReviewedAt,
                    seesDetails ? declaration?.ReviewNote : null);
            })
            .ToList();

        var outstanding = audit.IndependenceOutstanding().Count;
        return new IndependenceDto(
            Required: required,
            Complete: outstanding == 0,
            Outstanding: outstanding,
            CanDeclare: audit.IsOnAuditTeam(userId) && IndependenceOpen.Contains(audit.Status),
            CanDecide: canDecide,
            members);
    }

    private static EngagementLetterDto ToDto(EngagementLetter letter) => new(
        letter.Id,
        letter.Status.ToSnake(),
        letter.Revision,
        letter.Body,
        letter.DraftedBy,
        letter.DraftedAt,
        letter.IssuedBy,
        letter.IssuedAt,
        letter.AcknowledgedBy,
        letter.AcknowledgedAt,
        letter.AcknowledgementComment,
        [.. letter.PreviousRevisions.OrderBy(r => r.Revision).Select(r => new EngagementLetterRevisionDto(
            r.Revision, r.Body, r.IssuedBy, r.IssuedAt, r.AcknowledgedBy, r.AcknowledgedAt, r.AcknowledgementComment))],
        RowVersionToken.Encode(letter.Version));

    private static AuditMeetingDto ToDto(AuditMeeting meeting, IReadOnlyList<Report> reports) => new(
        meeting.Id,
        meeting.Kind.ToSnake(),
        meeting.HeldAt,
        meeting.AttendeeUserIds,
        meeting.ExternalAttendees,
        meeting.Minutes,
        meeting.AgreedPoints,
        meeting.ReportId,
        meeting.ReportId is { } reportId ? reports.FirstOrDefault(r => r.Id == reportId)?.VersionNumber : null,
        meeting.RecordedBy,
        meeting.RecordedAt,
        meeting.LastEditedBy,
        meeting.LastEditedAt,
        RowVersionToken.Encode(meeting.Version));
}
