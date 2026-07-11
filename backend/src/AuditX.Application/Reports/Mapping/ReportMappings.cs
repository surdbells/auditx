using System.Text.Json;
using AuditX.Application.Common.Enums;
using AuditX.Application.Reports.Dtos;
using AuditX.Domain.Reports;

namespace AuditX.Application.Reports.Mapping;

public static class ReportMappings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ReportDto ToDto(this Report r) => new(
        r.Id,
        r.AuditId,
        r.Kind.ToSnake(),
        r.VersionNumber,
        r.Status.ToSnake(),
        r.Sha256Hash,
        r.TemplateId,
        r.TemplateVersionSnapshot,
        ParseFormats(r.RequestedFormatsJson),
        ParseArtefacts(r.ProducedArtefactsJson).Select(a => new ProducedArtefactDto(a.Format, a.ContentType, a.SizeBytes, a.Sha256)).ToArray(),
        r.FailureReason,
        r.GeneratedBy,
        r.RequestedAt,
        r.CompletedAt,
        RowVersionToken.Encode(r.Version));

    public static ReportListItemDto ToListDto(this Report r) => new(
        r.Id,
        r.AuditId,
        r.Kind.ToSnake(),
        r.VersionNumber,
        r.Status.ToSnake(),
        r.Sha256Hash,
        ParseArtefacts(r.ProducedArtefactsJson).Select(a => a.Format).ToArray(),
        r.GeneratedBy,
        r.RequestedAt,
        r.CompletedAt);

    public static ReportDistributionDto ToDto(this ReportDistribution d) => new(
        d.Id, d.ReportId, d.ReportVersionNumber, d.RecipientUserId, d.RecipientEmail,
        d.DispatchedAt, d.DispatchedBy, d.Outcome.ToSnake(), d.OutcomeRecordedAt, d.OutcomeRecordedBy);

    public static ReportTemplateDto ToDto(this ReportTemplate t) => new(
        t.Id, t.Name, t.VersionNumber, t.TemplateDefinitionJson, t.IsActive, t.ActivationReason,
        t.CreatedByUserId, t.CreatedAtUtc, t.ActivatedBy, t.ActivatedAt, RowVersionToken.Encode(t.Version));

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
}
