using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions;

/// <summary>
/// Bank-configurable defaults for exceptions (M6). v1 ships hardcoded sane values behind this seam; the
/// configurable surface (per-bank/type/severity) lands with M12.
/// </summary>
public interface IExceptionDefaults
{
    /// <summary>Days from raise to the default remediation target date, by severity.</summary>
    int TargetDays(ExceptionSeverity severity);

    /// <summary>Lookback window for recurrence detection.</summary>
    int RecurrenceWindowMonths { get; }
}
