namespace AuditX.Application.Reports.Generation;

/// <summary>
/// Evaluates a report section's conditional flag against a FIXED, named flag bag computed from the composition
/// (US-M8-010). There is no code execution: a condition is just a flag name resolved against the bag, and an
/// unknown flag resolves to <c>false</c> (the section is omitted). The fixed flags are
/// <c>has_critical_exceptions</c>, <c>has_evidence_files</c>, <c>has_recurrence_flags</c>.
/// </summary>
public static class ConditionalSectionEvaluator
{
    public const string HasCriticalExceptions = "has_critical_exceptions";
    public const string HasEvidenceFiles = "has_evidence_files";
    public const string HasRecurrenceFlags = "has_recurrence_flags";

    public static readonly IReadOnlyList<string> KnownFlags = [HasCriticalExceptions, HasEvidenceFiles, HasRecurrenceFlags];

    /// <summary>Compute the fixed flag bag from the assembled composition.</summary>
    public static IReadOnlyDictionary<string, bool> BuildFlags(ReportComposition composition) => new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
    {
        [HasCriticalExceptions] = composition.CriticalExceptionCount > 0,
        [HasEvidenceFiles] = composition.EvidenceReferences.Count > 0,
        [HasRecurrenceFlags] = composition.RecurrenceCount > 0,
    };

    /// <summary>
    /// True if a section with the given <paramref name="conditionFlag"/> should render. A null/blank condition is
    /// unconditional (always renders). An unknown flag resolves to <c>false</c>.
    /// </summary>
    public static bool ShouldRender(string? conditionFlag, IReadOnlyDictionary<string, bool> flags)
    {
        if (string.IsNullOrWhiteSpace(conditionFlag))
        {
            return true;
        }

        return flags.TryGetValue(conditionFlag.Trim(), out var value) && value;
    }
}
