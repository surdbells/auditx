using AuditX.Domain.Enums;

namespace AuditX.Domain.Configuration;

/// <summary>
/// The typed shape of the <c>exception_defaults</c> configuration domain (M12): per-severity remediation target days,
/// the recurrence lookback window, and the recurrence detection threshold. <see cref="HardcodedFallback"/> reproduces
/// the values that were hardcoded before M12 (Critical 14 / High 30 / Medium 45 / Low 60, window 24 months, threshold
/// 3) — it is the seam's safety net so the config-backed defaults never throw when no active version exists.
/// </summary>
public sealed record ExceptionDefaultsDefinition(
    int CriticalTargetDays,
    int HighTargetDays,
    int MediumTargetDays,
    int LowTargetDays,
    int RecurrenceWindowMonths,
    int RecurrenceThreshold)
{
    /// <summary>The pre-M12 hardcoded values. The critical regression guard: the seeded v1 must reproduce these exactly.</summary>
    public static ExceptionDefaultsDefinition HardcodedFallback { get; } =
        new(CriticalTargetDays: 14, HighTargetDays: 30, MediumTargetDays: 45, LowTargetDays: 60, RecurrenceWindowMonths: 24, RecurrenceThreshold: 3);

    /// <summary>Remediation target days for a severity (mirrors the pre-M12 switch in ConfigBackedExceptionDefaults).</summary>
    public int TargetDays(ExceptionSeverity severity) => severity switch
    {
        ExceptionSeverity.Critical => CriticalTargetDays,
        ExceptionSeverity.High => HighTargetDays,
        ExceptionSeverity.Medium => MediumTargetDays,
        _ => LowTargetDays,
    };
}
