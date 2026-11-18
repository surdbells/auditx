namespace AuditX.Application.Reports.Generation;

/// <summary>An evidence reference embedded in a report — filename + SHA-256 + size only, never bytes (FR-M8-011).</summary>
public sealed record ReportEvidenceReference(string Filename, string Sha256Hash, long SizeBytes);

/// <summary>One action management agreed to take on a finding, as it stood when the report was generated.</summary>
public sealed record ReportActionLine(string Description, string? OwnerName, DateOnly TargetDate, string Status);

/// <summary>An exception finding projected for the report (M6 data — severity/category/recurrence/MAP-action count).</summary>
public sealed record ReportExceptionLine(
    Guid ExceptionId,
    string Title,
    string Severity,
    string? Category,
    string Status,
    bool IsRecurrence,
    int MapActionCount,
    int CompletedMapActionCount)
{
    public string? RootCause { get; init; }

    public string? Recommendation { get; init; }

    /// <summary>Who in management owns the finding.</summary>
    public string? OwnerName { get; init; }

    public DateOnly? TargetDate { get; init; }

    /// <summary>Management's position, as a label: Accepted / Partially accepted / Disputed. Null while awaited.</summary>
    public string? ManagementResponse { get; init; }

    public string? ManagementResponseComment { get; init; }

    /// <summary>Who recorded management's response.</summary>
    public string? ManagementRespondedBy { get; init; }

    /// <summary>True when the audit team recorded the response on management's behalf.</summary>
    public bool ManagementResponseOnBehalf { get; init; }

    /// <summary>The actions management agreed to take (the approved or proposed action plan).</summary>
    public IReadOnlyList<ReportActionLine> Actions { get; init; } = [];
}

/// <summary>
/// A checklist item + its (final) response projected for the report (M4/M5 data). <paramref name="Answer"/> is what
/// the responder actually gave, as they saw it — the conclusion label for a verdict item ("Compliant", "No"), or the
/// captured value for a value item ("12.5 days", "4 – Effective", a date, a choice, free text). <paramref name="Verdict"/>
/// stays the canonical pass / fail / na conclusion (absent on a value item answered without one).
/// </summary>
public sealed record ReportChecklistLine(
    Guid ItemId,
    string Prompt,
    string? SectionName,
    string? Verdict,
    string? Comment,
    string? Answer = null);

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
    /// <summary>
    /// True for a DRAFT report — for discussion with management, not for reliance. Every artefact says so on its face.
    /// </summary>
    public bool IsDraft { get; init; }

    /// <summary>Human-readable severity rollup for the distribution email body (E1), e.g. "1 critical, 3 high".</summary>
    public string SeveritySummary { get; init; } = string.Empty;

    /// <summary>
    /// Items that WERE answered but carry no pass / fail / n-a conclusion — a number, date, rating, choice or text
    /// captured without one. Counted apart from <see cref="UnansweredCount"/> so an answered item is never reported
    /// as unanswered.
    /// </summary>
    public int RecordedCount { get; init; }
}
