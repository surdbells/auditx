using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using System.Globalization;
using System.Text.Json;
using AuditX.Application.Common.Enums;
using AuditX.Application.Templates;
using AuditX.Domain.Audits;
using AuditX.Domain.Enums;

namespace AuditX.Application.Reports.Generation;

/// <summary>
/// Composes a <see cref="ReportComposition"/> from LIVE data at generation time (M8). Sources are M4 (audit
/// metadata + checklist), M5 (responses + evidence references) and M6 (exceptions + MAP actions). Evidence is
/// embedded as REFERENCES ONLY — filename + SHA-256 + size, never bytes (FR-M8-011, mirrors the M7 dossier).
///
/// SCOPE GUARD: the assembler must NEVER query M7 sanctions — it takes no sanctions repository and references no
/// sanctions type (FR-M7-010 confidentiality; M8-XC-A2). It is a pure composition service (no port).
/// </summary>
public sealed class ReportContentAssembler(
    IExceptionRepository exceptions, IEvidenceRepository evidence, IClock clock)
{
    /// <summary>The placeholder token the HTML renderer substitutes with the canonical SHA-256 after hashing.</summary>
    public const string Sha256Placeholder = "{{REPORT_SHA256}}";

    public async Task<ReportComposition> AssembleAsync(Audit audit, int versionNumber, CancellationToken cancellationToken)
    {
        // M4/M5: checklist items + their (final) responses.
        var responsesByItem = audit.Responses
            .Where(r => !r.IsDraft)
            .GroupBy(r => r.ChecklistItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RespondedAt).First());

        var checklistLines = new List<ReportChecklistLine>();
        var pass = 0;
        var fail = 0;
        var na = 0;
        var recorded = 0;
        var unanswered = 0;
        foreach (var item in audit.ChecklistItems.OrderBy(i => i.OrderIndex))
        {
            responsesByItem.TryGetValue(item.Id, out var response);
            var verdict = response?.Verdict;
            switch (verdict)
            {
                case ResponseVerdict.Pass:
                    pass++;
                    break;
                case ResponseVerdict.Fail:
                    fail++;
                    break;
                case ResponseVerdict.Na:
                    na++;
                    break;
                default:
                    // No conclusion: either nothing was answered, or a value was captured without one.
                    if (response is null)
                    {
                        unanswered++;
                    }
                    else
                    {
                        recorded++;
                    }

                    break;
            }

            checklistLines.Add(new ReportChecklistLine(
                item.Id, item.Prompt, item.SectionName, verdict?.ToSnake(), response?.Comment, FormatAnswer(item, response)));
        }

        // M6: exceptions with their MAP actions.
        var exceptionEntities = await exceptions.ListByAuditWithMapActionsAsync(audit.Id, cancellationToken);
        var exceptionLines = exceptionEntities.Select(e => new ReportExceptionLine(
            e.Id, e.Title, e.Severity.ToSnake(), e.Category, e.Status.ToSnake(), e.IsRecurrence,
            e.MapActions.Count, e.MapActions.Count(a => a.Status == MapActionStatus.Complete))).ToArray();

        var criticalCount = exceptionEntities.Count(e => e.Severity == ExceptionSeverity.Critical);
        var recurrenceCount = exceptionEntities.Count(e => e.IsRecurrence);
        var severitySummary = BuildSeveritySummary(exceptionEntities.GroupBy(e => e.Severity).ToDictionary(g => g.Key, g => g.Count()));

        // M5/M6 evidence: REFERENCES ONLY, gathered from each response and each MAP action context.
        var references = new List<ReportEvidenceReference>();
        foreach (var response in responsesByItem.Values)
        {
            var files = await evidence.ListForContextAsync(audit.Id, EvidenceContextType.Response, response.Id, cancellationToken);
            references.AddRange(files.Select(f => new ReportEvidenceReference(f.OriginalFilename, f.Sha256Hash, f.SizeBytes)));
        }

        foreach (var exception in exceptionEntities)
        {
            foreach (var action in exception.MapActions)
            {
                var files = await evidence.ListForContextAsync(audit.Id, EvidenceContextType.MapAction, action.Id, cancellationToken);
                references.AddRange(files.Select(f => new ReportEvidenceReference(f.OriginalFilename, f.Sha256Hash, f.SizeBytes)));
            }
        }

        return new ReportComposition(
            audit.Id,
            audit.Name,
            audit.AuditType,
            audit.Status.ToSnake(),
            audit.ScopeDescription,
            audit.StartDate,
            audit.TargetEndDate,
            audit.ActualEndDate,
            versionNumber,
            Sha256Placeholder,
            checklistLines.Count,
            pass,
            fail,
            na,
            unanswered,
            exceptionEntities.Count,
            criticalCount,
            recurrenceCount,
            checklistLines,
            exceptionLines,
            references,
            clock.UtcNow)
        {
            SeveritySummary = severitySummary,
            RecordedCount = recorded,
        };
    }

    /// <summary>
    /// What the responder answered, as they saw it. Verdict items read with the conclusion label that was chosen
    /// (the organisation's own wording, snapshotted on the response); value items read with the captured value —
    /// a number with its unit, a rating with the label of the point picked.
    /// </summary>
    public static string? FormatAnswer(AuditChecklistItem item, ChecklistResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        if (!item.ResponseType.IsValueType())
        {
            if (!string.IsNullOrWhiteSpace(response.SelectedOptionLabel))
            {
                return response.SelectedOptionLabel;
            }

            return (item.ResponseType, response.Verdict) switch
            {
                (ResponseType.YesNo, ResponseVerdict.Pass) => "Yes",
                (ResponseType.YesNo, ResponseVerdict.Fail) => "No",
                (_, ResponseVerdict.Pass) => "Pass",
                (_, ResponseVerdict.Fail) => "Fail",
                (_, ResponseVerdict.Na) => "N/A",
                _ => null,
            };
        }

        if (string.IsNullOrWhiteSpace(response.ValueJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(response.ValueJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            switch (item.ResponseType)
            {
                case ResponseType.Text:
                    return ReadString(root, "text");
                case ResponseType.Date:
                    return ReadString(root, "date");
                case ResponseType.MultipleChoice:
                    return ReadString(root, "choice");
                case ResponseType.Numeric when root.TryGetProperty("number", out var number) && number.ValueKind == JsonValueKind.Number:
                {
                    var text = number.TryGetDecimal(out var value) ? value.ToString(CultureInfo.InvariantCulture) : number.GetRawText();
                    var unit = ResponseItemConfig.Parse(item.ResponseConfigJson).Unit?.Trim();
                    return string.IsNullOrEmpty(unit) ? text : $"{text} {unit}";
                }

                case ResponseType.Rating when root.TryGetProperty("rating", out var rating) && rating.ValueKind == JsonValueKind.Number:
                {
                    var label = ReadString(root, "label");
                    return label is null ? rating.GetRawText() : $"{rating.GetRawText()} – {label}";
                }

                default:
                    return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(element.GetString())
            ? element.GetString()!.Trim()
            : null;

    private static string BuildSeveritySummary(IReadOnlyDictionary<ExceptionSeverity, int> counts)
    {
        if (counts.Count == 0)
        {
            return "no exceptions";
        }

        var parts = new[] { ExceptionSeverity.Critical, ExceptionSeverity.High, ExceptionSeverity.Medium, ExceptionSeverity.Low }
            .Where(s => counts.TryGetValue(s, out var c) && c > 0)
            .Select(s => $"{counts[s]} {s.ToSnake()}");
        return string.Join(", ", parts);
    }
}
