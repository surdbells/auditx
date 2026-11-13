using System.Net;
using System.Security.Cryptography;
using System.Text;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.Reports;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// Renders the CANONICAL, hashed, browser-printable report as structured HTML (M8). Mirrors
/// <c>HtmlDossierGenerator</c>: <see cref="WebUtility.HtmlEncode"/> on all dynamic text, sections gated by the
/// fixed conditional-flag evaluator, an evidence REFERENCE table (filename + SHA-256 + size — never bytes,
/// FR-M8-011). No PDF library (license/audit risk — a vetted PDF engine is a later swap behind
/// <see cref="IReportRenderer"/>).
///
/// CANONICALIZATION: the artefact's SHA-256 is computed over the EXACT bytes that are stored (no post-hash
/// substitution), so verify-on-read recomputes identically over the stored bytes. The self-hash is therefore not
/// embedded in the hashed body — it is surfaced via the report metadata / verify-hash endpoint / download header.
///
/// BRANDING: the institution's logo, name and colours are written INTO the document (the logo as an inline data
/// URI, the colours as literal CSS values) — nothing is fetched or resolved when the report is opened. The branding
/// is therefore part of the hashed bytes: an issued report keeps the look it was issued with, and any alteration of
/// it fails verify-on-read like any other tampering.
/// </summary>
public sealed class HtmlReportRenderer : IReportRenderer
{
    public IReadOnlyCollection<string> SupportedFormats { get; } = ["html"];

    public bool CanRender(string format) => string.Equals(format, "html", StringComparison.OrdinalIgnoreCase);

    public RenderedArtefact Render(string format, ReportRenderContext context)
    {
        if (context.Kind != Domain.Enums.ReportKind.AuditEngagement && context.Standalone is { } standalone)
        {
            return RenderStandalone(standalone, context.Branding);
        }

        var c = context.Composition!;
        var flags = ConditionalSectionEvaluator.BuildFlags(c);
        var sections = ReportTemplateDefinition.ReadSections(context.TemplateDefinitionJson);
        var title = ReportTemplateDefinition.ReadTitle(context.TemplateDefinitionJson) ?? "Audit Report";

        var branding = context.Branding;
        var sb = new StringBuilder();
        AppendHead(sb, $"{title} — {c.AuditName}", branding);
        AppendBrandHeader(sb, branding, title);

        // Cover
        sb.Append("<h1>").Append(Html(title)).Append("</h1>");
        sb.Append("<table class=\"meta\">");
        Row(sb, "Audit", c.AuditName);
        Row(sb, "Audit type", c.AuditType);
        Row(sb, "Status", c.AuditStatus);
        Row(sb, "Report version", c.VersionNumber.ToString());
        Row(sb, "Conducted", $"{c.StartDate:yyyy-MM-dd} to {(c.ActualEndDate ?? c.TargetEndDate):yyyy-MM-dd}");
        Row(sb, "Generated (UTC)", c.GeneratedAtUtc.ToString("u"));
        Row(sb, "Integrity (SHA-256)", "Computed at issue — verify via the report's verify-hash endpoint.");
        sb.Append("</table>");

        if (sections.Count == 0)
        {
            // No template sections configured: render the standard section set unconditionally.
            AppendExecutiveSummary(sb, c);
            AppendScope(sb, c);
            AppendFindings(sb, c);
            AppendEvidence(sb, c);
        }
        else
        {
            foreach (var section in sections)
            {
                if (!ConditionalSectionEvaluator.ShouldRender(section.Condition, flags))
                {
                    continue;
                }

                AppendSection(sb, section, c);
            }
        }

        AppendFooter(sb, branding, "This report assembles live audit, response, exception and evidence data.");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new RenderedArtefact(bytes, "text/html", $"audit-report-{c.AuditId}-v{c.VersionNumber}.html", "html", sha256);
    }

    /// <summary>
    /// Renders a standalone (cross-audit) report from its generic section model (M8). Same canonicalisation as the
    /// engagement path: the artefact's SHA-256 is computed over the exact stored bytes (no post-hash substitution).
    /// </summary>
    private static RenderedArtefact RenderStandalone(StandaloneReportModel model, ReportBranding branding)
    {
        var sb = new StringBuilder();
        AppendHead(sb, model.Title, branding);
        AppendBrandHeader(sb, branding, model.Title);

        sb.Append("<h1>").Append(Html(model.Title)).Append("</h1>");
        sb.Append("<p class=\"sub\">").Append(Html(model.Subtitle)).Append("</p>");
        sb.Append("<table class=\"meta\">");
        Row(sb, "Report version", model.VersionNumber.ToString());
        Row(sb, "Generated (UTC)", model.GeneratedAtUtc.ToString("u"));
        Row(sb, "Integrity (SHA-256)", "Computed at issue — verify via the report's verify-hash endpoint.");
        sb.Append("</table>");

        foreach (var section in model.Sections)
        {
            sb.Append("<h2>").Append(Html(section.Heading)).Append("</h2>");
            if (section.Metrics.Count > 0)
            {
                sb.Append("<table>");
                foreach (var metric in section.Metrics)
                {
                    Row(sb, metric.Label, metric.Value);
                }

                sb.Append("</table>");
            }

            if (section.Table is { } table && table.Columns.Count > 0)
            {
                // Wide tables (coverage matrix, scorecards) scroll within their own box, never the page body.
                sb.Append("<div class=\"tablewrap\"><table><tr>");
                foreach (var column in table.Columns)
                {
                    sb.Append("<th>").Append(Html(column)).Append("</th>");
                }

                sb.Append("</tr>");
                foreach (var dataRow in table.Rows)
                {
                    sb.Append("<tr>");
                    foreach (var cell in dataRow)
                    {
                        sb.Append("<td>").Append(Html(cell ?? string.Empty)).Append("</td>");
                    }

                    sb.Append("</tr>");
                }

                sb.Append("</table></div>");
            }

            if (!string.IsNullOrWhiteSpace(section.Note))
            {
                sb.Append("<p>").Append(Html(section.Note)).Append("</p>");
            }
        }

        AppendFooter(sb, branding, "This report assembles live cross-audit analytics data.");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new RenderedArtefact(bytes, "text/html", $"{model.FileStem}-v{model.VersionNumber}.html", "html", sha256);
    }

    /// <summary>
    /// The document head: one stylesheet for both report shapes, with the brand colours written in as literal values
    /// (they come from <see cref="ReportBranding"/>, which only ever holds validated <c>#rrggbb</c>).
    /// </summary>
    private static void AppendHead(StringBuilder sb, string pageTitle, ReportBranding branding)
    {
        var palette = new BrandPalette(branding);
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>").Append(Html(pageTitle)).Append("</title>");
        sb.Append("<style>")
            .Append("body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;color:#1a1a1a}")
            .Append(".brand{display:flex;align-items:center;gap:1rem;padding-bottom:.85rem;margin-bottom:1.5rem;border-bottom:3px solid ").Append(palette.Primary.Hex).Append('}')
            .Append(".brand__logo{max-height:56px;max-width:220px;object-fit:contain}")
            .Append(".brand__org{font-size:1.1rem;font-weight:700;line-height:1.25;color:").Append(palette.HeadingText.Hex).Append('}')
            .Append(".brand__doc{font-size:.8rem;color:#555;letter-spacing:.04em;text-transform:uppercase}")
            .Append("h1{font-size:1.5rem;color:").Append(palette.HeadingText.Hex).Append('}')
            .Append("h2{font-size:1.15rem;color:").Append(palette.HeadingText.Hex)
            .Append(";border-bottom:2px solid ").Append(palette.Accent.Hex).Append(";padding-bottom:.25rem;margin-top:1.5rem}")
            .Append("table{border-collapse:collapse;width:100%;margin-top:.5rem}")
            .Append("td,th{border:1px solid #ccc;padding:.4rem .6rem;text-align:left;vertical-align:top;word-break:break-word;overflow-wrap:anywhere}")
            .Append("th{background:").Append(palette.TableHeaderFill.Hex).Append(";color:").Append(palette.TableHeaderText.Hex).Append('}')
            .Append("code{font-family:Consolas,monospace}.meta th{width:16rem}.sub{color:#555}")
            .Append(".tablewrap{overflow-x:auto;margin-top:.5rem}.tablewrap table{margin-top:0}")
            .Append(".foot{margin-top:2rem;padding-top:.75rem;border-top:1px solid #ccc;color:#555;font-size:.8rem}")
            .Append("@media print{body{margin:0}.brand{break-inside:avoid}h2{break-after:avoid}}")
            .Append("</style>");
        sb.Append("</head><body>");
    }

    /// <summary>The letterhead: the institution's logo (when it has one) beside its name and the document title.</summary>
    private static void AppendBrandHeader(StringBuilder sb, ReportBranding branding, string documentTitle)
    {
        sb.Append("<header class=\"brand\">");
        if (branding.LogoDataUri is { } logo)
        {
            // Inline data URI (validated base64 image) — nothing is fetched when the report is opened.
            sb.Append("<img class=\"brand__logo\" src=\"").Append(Html(logo)).Append("\" alt=\"")
                .Append(Html(branding.InstitutionName)).Append(" logo\">");
        }

        sb.Append("<div><div class=\"brand__org\">").Append(Html(branding.InstitutionName)).Append("</div>")
            .Append("<div class=\"brand__doc\">").Append(Html(documentTitle)).Append("</div></div>");
        sb.Append("</header>");
    }

    private static void AppendFooter(StringBuilder sb, ReportBranding branding, string note)
    {
        sb.Append("<p class=\"foot\">").Append(Html(branding.InstitutionName))
            .Append(" · Generated by AuditX. ").Append(Html(note)).Append("</p>");
        sb.Append("</body></html>");
    }

    private static void AppendSection(StringBuilder sb, ReportTemplateSection section, ReportComposition c)
    {
        switch (section.Key)
        {
            case "executive_summary":
                AppendExecutiveSummary(sb, c, section.Title);
                break;
            case "scope":
            case "methodology":
                AppendScope(sb, c, section.Title);
                break;
            case "findings":
                AppendFindings(sb, c, section.Title);
                break;
            case "evidence":
            case "appendix":
                AppendEvidence(sb, c, section.Title);
                break;
            default:
                // A configured-but-unrecognised section renders its heading with a checklist roll-up fallback.
                sb.Append("<h2>").Append(Html(section.Title)).Append("</h2>");
                break;
        }
    }

    private static void AppendExecutiveSummary(StringBuilder sb, ReportComposition c, string heading = "Executive summary")
    {
        sb.Append("<h2>").Append(Html(heading)).Append("</h2><table>");
        Row(sb, "Checklist items", c.TotalItems.ToString());
        Row(sb, "Pass", c.PassCount.ToString());
        Row(sb, "Fail", c.FailCount.ToString());
        Row(sb, "N/A", c.NaCount.ToString());
        Row(sb, "Answered without a conclusion", c.RecordedCount.ToString());
        Row(sb, "Unanswered", c.UnansweredCount.ToString());
        Row(sb, "Exceptions raised", c.ExceptionCount.ToString());
        Row(sb, "Critical exceptions", c.CriticalExceptionCount.ToString());
        Row(sb, "Recurrence flags", c.RecurrenceCount.ToString());
        sb.Append("</table>");
    }

    private static void AppendScope(StringBuilder sb, ReportComposition c, string heading = "Scope")
    {
        sb.Append("<h2>").Append(Html(heading)).Append("</h2>");
        sb.Append("<p>").Append(string.IsNullOrWhiteSpace(c.ScopeDescription) ? "&mdash;" : Html(c.ScopeDescription)).Append("</p>");
    }

    private static void AppendFindings(StringBuilder sb, ReportComposition c, string heading = "Findings")
    {
        sb.Append("<h2>").Append(Html(heading)).Append("</h2>");
        if (c.Exceptions.Count == 0)
        {
            sb.Append("<p>No exceptions were raised.</p>");
            return;
        }

        sb.Append("<table><tr><th>Title</th><th>Severity</th><th>Category</th><th>Status</th><th>Recurrence</th><th>MAP actions</th></tr>");
        foreach (var e in c.Exceptions)
        {
            sb.Append("<tr><td>").Append(Html(e.Title)).Append("</td><td>").Append(Html(e.Severity))
                .Append("</td><td>").Append(Html(e.Category ?? "—")).Append("</td><td>").Append(Html(e.Status))
                .Append("</td><td>").Append(e.IsRecurrence ? "Yes" : "No")
                .Append("</td><td>").Append(e.CompletedMapActionCount).Append('/').Append(e.MapActionCount)
                .Append("</td></tr>");
        }

        sb.Append("</table>");
    }

    private static void AppendEvidence(StringBuilder sb, ReportComposition c, string heading = "Evidence references (SHA-256)")
    {
        sb.Append("<h2>").Append(Html(heading)).Append("</h2>");
        if (c.EvidenceReferences.Count == 0)
        {
            sb.Append("<p>No evidence files are linked to this audit.</p>");
            return;
        }

        sb.Append("<table><tr><th>File</th><th>SHA-256</th><th>Size (bytes)</th></tr>");
        foreach (var reference in c.EvidenceReferences)
        {
            sb.Append("<tr><td>").Append(Html(reference.Filename)).Append("</td><td><code>")
                .Append(Html(reference.Sha256Hash)).Append("</code></td><td>").Append(reference.SizeBytes).Append("</td></tr>");
        }

        sb.Append("</table>");
    }

    private static void Row(StringBuilder sb, string label, string? value)
        => sb.Append("<tr><th>").Append(Html(label)).Append("</th><td>")
            .Append(string.IsNullOrWhiteSpace(value) ? "&mdash;" : Html(value)).Append("</td></tr>");

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
