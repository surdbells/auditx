using AuditX.Application.Abstractions;
using AuditX.Domain.Enums;

namespace AuditX.Infrastructure.Exceptions;

/// <summary>
/// v1 hardcoded exception defaults (M6). Severity → remediation target days and the recurrence lookback.
/// The bank-configurable surface (per-bank/type/severity) lands with M12 behind this same interface.
/// </summary>
public sealed class ConfigBackedExceptionDefaults : IExceptionDefaults
{
    public int RecurrenceWindowMonths => 24;

    public int TargetDays(ExceptionSeverity severity) => severity switch
    {
        ExceptionSeverity.Critical => 14,
        ExceptionSeverity.High => 30,
        ExceptionSeverity.Medium => 45,
        _ => 60,
    };
}
