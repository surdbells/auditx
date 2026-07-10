using AuditX.Application.Common.Enums;
using AuditX.Application.Sanctions.Dtos;
using AuditX.Domain.Sanctions;

namespace AuditX.Application.Sanctions.Mapping;

public static class SanctionsMappings
{
    /// <summary>Full case DTO. Pass <paramref name="unmask"/>=false to redact the subject id (non-team viewers).</summary>
    public static SanctionsCaseDto ToDto(this SanctionsCase c, bool unmask) => new(
        c.Id,
        c.ExceptionId,
        unmask ? c.SubjectUserId : null,
        !unmask,
        c.Status.ToSnake(),
        c.Category,
        c.Severity.ToSnake(),
        c.IsRecurrence,
        c.Recommendation,
        c.GridConsultedVersion,
        c.GridRecommendedRange,
        c.WithinGridRange,
        c.DeviationReason,
        c.HrOutcomeJson,
        c.DcDecisionJson,
        c.TriggeredBy,
        c.TriggeredAt,
        c.RecommendedBy,
        c.RecommendedAt,
        c.SubmittedAt,
        c.HrOutcomeAt,
        c.DcDecisionAt,
        c.ClosedBy,
        c.ClosedAt,
        RowVersionToken.Encode(c.Version),
        unmask ? c.TeamMembers.Select(m => m.UserId).ToArray() : []);

    /// <summary>List item DTO — the subject is ALWAYS masked in lists (A1).</summary>
    public static SanctionsCaseListDto ToListDto(this SanctionsCase c) => new(
        c.Id, c.ExceptionId, null, true, c.Status.ToSnake(), c.Category, c.Severity.ToSnake(), c.IsRecurrence, c.TriggeredAt);

    public static SanctionsGridVersionDto ToDto(this SanctionsGridVersion g) => new(
        g.Id, g.VersionNumber, g.GridDefinitionJson, g.IsActive, g.ActivationReason,
        g.CreatedByUserId, g.CreatedAtUtc, g.ActivatedBy, g.ActivatedAt, RowVersionToken.Encode(g.Version));

    public static SanctionsAppealDto ToDto(this SanctionsAppeal a) => new(
        a.Id, a.SanctionsCaseId, a.AppellantUserId, a.RoutedToUserId, a.Basis,
        a.Status.ToSnake(), a.DecisionJson, a.FiledAt, a.DecidedAt, RowVersionToken.Encode(a.Version));
}
