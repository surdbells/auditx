using AuditX.Domain.Enums;

namespace AuditX.Application.Reports.Generation;

/// <summary>
/// A single metric line in a standalone report section — a label and its already-formatted value (M8).
/// </summary>
public sealed record StandaloneMetric(string Label, string Value);

/// <summary>
/// A simple tabular block in a standalone report section: a header row + zero or more data rows, each row a list of
/// already-formatted cell strings aligned to <see cref="Columns"/> (M8).
/// </summary>
public sealed record StandaloneTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// One section of a standalone report: a heading, an optional key/value metric list, an optional table, and an
/// optional free-text note (rendered when the section has no data). Renderers walk sections generically, so a new
/// standalone report kind is a matter of composing different sections — no renderer change (M8).
/// </summary>
public sealed record StandaloneSection(
    string Heading,
    IReadOnlyList<StandaloneMetric> Metrics,
    StandaloneTable? Table = null,
    string? Note = null);

/// <summary>
/// The pure, port-free data a standalone (cross-audit) report renders from (M8). Assembled from the M9 analytics /
/// coverage read ports — NOT from a single audit. Discriminated by <see cref="Kind"/> (never
/// <see cref="ReportKind.AuditEngagement"/>); the engagement shape uses <see cref="ReportComposition"/> instead.
/// </summary>
public sealed record StandaloneReportModel(
    ReportKind Kind,
    string Title,
    string Subtitle,
    int VersionNumber,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<StandaloneSection> Sections)
{
    /// <summary>The kebab-case stem used for artefact filenames, e.g. <c>executive-summary</c>.</summary>
    public string FileStem => FileStemFor(Kind);

    /// <summary>The kebab-case artefact-filename stem for a standalone report kind.</summary>
    public static string FileStemFor(ReportKind kind) => kind switch
    {
        ReportKind.ExecutiveSummary => "executive-summary",
        ReportKind.AnnualPlanStatus => "annual-plan-status",
        ReportKind.KpiPack => "kpi-pack",
        ReportKind.AuditCoverage => "audit-coverage",
        ReportKind.FindingsRegister => "findings-register",
        ReportKind.SanctionsConsistency => "sanctions-consistency",
        ReportKind.PerformanceScorecards => "performance-scorecards",
        _ => "report",
    };

    /// <summary>The human title for a standalone report kind (also used as the default template title).</summary>
    public static string TitleFor(ReportKind kind) => kind switch
    {
        ReportKind.ExecutiveSummary => "Executive Summary",
        ReportKind.AnnualPlanStatus => "Annual Plan Status",
        ReportKind.KpiPack => "KPI Pack",
        ReportKind.AuditCoverage => "Audit Coverage",
        ReportKind.FindingsRegister => "Findings Register",
        ReportKind.SanctionsConsistency => "Sanctions Consistency",
        ReportKind.PerformanceScorecards => "Auditor Performance Scorecards",
        _ => "Report",
    };
}
