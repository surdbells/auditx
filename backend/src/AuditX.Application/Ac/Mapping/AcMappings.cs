using System.Text.Json;
using AuditX.Application.Ac.Dtos;
using AuditX.Application.Ac.Generation;
using AuditX.Application.Common.Enums;
using AuditX.Domain.Ac;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;

namespace AuditX.Application.Ac.Mapping;

public static class AcMappings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---- AC pack metadata ----

    public static AcPackDto ToDto(this AcPack p) => new(
        p.Id,
        p.VersionNumber,
        p.Status.ToSnake(),
        p.PeriodStart,
        p.PeriodEnd,
        p.AcMeetingLabel,
        p.Sha256Hash,
        p.CiaSupplementaryText,
        ParseFormats(p.RequestedFormatsJson),
        ParseArtefacts(p.ProducedArtefactsJson).Select(a => new AcProducedArtefactDto(a.Format, a.ContentType, a.SizeBytes, a.Sha256)).ToArray(),
        p.FailureReason,
        p.GeneratedBy,
        p.RequestedAt,
        p.CompletedAt,
        p.ApprovedBy,
        p.ApprovedAt,
        RowVersionToken.Encode(p.Version));

    public static AcPackListItemDto ToListDto(this AcPack p) => new(
        p.Id,
        p.VersionNumber,
        p.Status.ToSnake(),
        p.PeriodStart,
        p.PeriodEnd,
        p.AcMeetingLabel,
        p.Sha256Hash,
        ParseArtefacts(p.ProducedArtefactsJson).Select(a => a.Format).ToArray(),
        p.GeneratedBy,
        p.RequestedAt,
        p.CompletedAt);

    public static AcPackDistributionDto ToDto(this AcPackDistribution d) => new(
        d.Id, d.AcPackId, d.AcPackVersionNumber, d.RecipientUserId, d.DispatchedAt, d.DispatchedBy, d.Outcome.ToSnake());

    // ---- AC action items / comments / restrictions ----

    public static AcActionItemDto ToDto(this AcActionItem i) => new(
        i.Id, i.Title, i.Description, i.Status.ToSnake(), i.AssignedToUserId, i.DueDate, i.ClosureResponse,
        i.CreatedByUserId, i.ClosedAt, i.ClosedByUserId, i.AcknowledgedAt, i.AcknowledgedByUserId,
        RowVersionToken.Encode(i.Version));

    public static AcCommentDto ToDto(this AcComment c) => new(
        c.Id, c.TargetType.ToSnake(), c.TargetId, c.CommentText, c.AuthorUserId, c.CommentedAt);

    public static FindingVisibilityRestrictionDto ToDto(this FindingVisibilityRestriction r) => new(
        r.Id, r.FindingType.ToSnake(), r.FindingId, ParseUserIds(r.AllowedUserIdsJson), r.Reason, r.RestrictedByUserId, r.RestrictedAt);

    // ---- Pack analytics (from the immutable snapshot, restricted-filter applied per requester) ----

    /// <summary>
    /// Project the immutable snapshot into the analytics DTO, applying the restricted-visibility allow-list
    /// PER-REQUESTER: a non-allow-listed finding keeps its severity/dates/counts (aggregates stay coherent) but its
    /// title is nulled and <c>restricted=true</c> so the SPA shows a "Restricted finding pending chair review"
    /// placeholder. The allow-lists are passed in (loaded live), never baked into the snapshot.
    /// </summary>
    public static AcPackAnalyticsDto ToAnalyticsDto(
        this AcPackComposition c, Guid requesterId, IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> exceptionAllowLists) => new(
        c.VersionNumber,
        c.PeriodStart,
        c.PeriodEnd,
        c.TotalPlans,
        c.PlanItemsTotal,
        c.PlanItemsCompleted,
        c.PlanCompletionPercent,
        c.OpenExceptionTotal,
        c.AverageClosureDays,
        c.ExceptionsBySeverity.Select(s => new AcSeverityCountDto(s.Severity, s.Count)).ToArray(),
        c.MaterialFindings.Select(f => MapFinding(f, requesterId, exceptionAllowLists)).ToArray(),
        c.SanctionsTotalCases,
        c.SanctionsGridAdherencePercent,
        c.SanctionsAppealRatePercent,
        c.SanctionsByBusinessUnit.Select(r => new AcSanctionsConsistencyRowDto(
            r.BusinessUnit, r.CaseCount, r.WithinGridCount, r.GridAdherencePercent, r.DeviationCount, r.AppealCount, r.AppealRatePercent)).ToArray(),
        c.RecurrenceClusters.Select(rc => new AcRecurrenceClusterDto(
            rc.Id, rc.AuditableEntityId, rc.Category, rc.ClosedExceptionCount, rc.WindowMonths, rc.FirstOccurredAt, rc.LastOccurredAt)).ToArray(),
        c.GeneratedAtUtc);

    /// <summary>
    /// Apply the per-requester restricted-visibility filter to one material finding. Returns a placeholder
    /// (title nulled, <c>restricted=true</c>) when the finding is restricted and the requester is not allow-listed.
    /// </summary>
    public static AcMaterialFindingDto MapFinding(
        AcMaterialFindingLine f, Guid requesterId, IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> exceptionAllowLists)
    {
        var restricted = exceptionAllowLists.TryGetValue(f.ExceptionId, out var allowed) && !allowed.Contains(requesterId);
        return new AcMaterialFindingDto(
            f.ExceptionId,
            f.AuditId,
            restricted ? null : f.Title,
            f.Severity,
            f.Status,
            f.AuditableEntityId,
            f.RaisedAt,
            f.TargetDate,
            restricted);
    }

    // ---- Parsing helpers ----

    public static IReadOnlyList<string> ParseFormats(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<ProducedArtefact> ParseArtefacts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProducedArtefact>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<Guid> ParseUserIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static AcPackComposition? ParseComposition(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AcPackComposition>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
