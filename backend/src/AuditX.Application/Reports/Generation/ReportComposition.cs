namespace AuditX.Application.Reports.Generation;

/// <summary>An evidence reference embedded in a report — filename + SHA-256 + size only, never bytes (FR-M8-011).</summary>
public sealed record ReportEvidenceReference(string Filename, string Sha256Hash, long SizeBytes);

/// <summary>An exception finding projected for the report (M6 data — severity/category/recurrence/MAP-action count).</summary>
public sealed record ReportExceptionLine(
    Guid ExceptionId,
    string Title,
    string Severity,
    string? Category,
    string Status,
    bool IsRecurrence,
    int MapActionCount,
    int CompletedMapActionCount);

/// <summary>A checklist item + its (final) response projected for the report (M4/M5 data).</summary>
public sealed record ReportChecklistLine(
    Guid ItemId,
    string Prompt,
    string? SectionName,
    string? Verdict,
    string? Comment);

/// <summary>
/// The pure, port-free data the renderer composes a report from (M8). Assembled from M4 audit + M5 responses + M6
/// exceptions/MAP + evidence REFERENCES only. NEVER includes M7 sanctions (scope guard) and never evidence bytes.
/// </summary>
public sealed record ReportComposition(
    Guid AuditId,
    string AuditName,
    string AuditType,
    string AuditStatus,
    string? ScopeDescription,
    DateOnly StartDate,
    DateOnly TargetEndDate,
    DateOnly? ActualEndDate,
    int VersionNumber,
    string Sha256Placeholder,
    int TotalItems,
    int PassCount,
    int FailCount,
    int NaCount,
    int UnansweredCount,
    int ExceptionCount,
    int CriticalExceptionCount,
    int RecurrenceCount,
    IReadOnlyList<ReportChecklistLine> ChecklistLines,
    IReadOnlyList<ReportExceptionLine> Exceptions,
    IReadOnlyList<ReportEvidenceReference> EvidenceReferences,
    DateTimeOffset GeneratedAtUtc)
{
    /// <summary>Human-readable severity rollup for the distribution email body (E1), e.g. "1 critical, 3 high".</summary>
    public string SeveritySummary { get; init; } = string.Empty;
}
