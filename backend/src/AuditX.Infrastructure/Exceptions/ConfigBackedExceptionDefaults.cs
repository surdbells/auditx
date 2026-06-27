using AuditX.Application.Abstractions;
using AuditX.Domain.Configuration;
using AuditX.Domain.Enums;

namespace AuditX.Infrastructure.Exceptions;

/// <summary>
/// Exception defaults (M6) backed by the active <c>exception_defaults</c> bank-configuration version (M12). Reads the
/// active definition via <see cref="IActiveConfigurationProvider"/> on every call (cheaply — it is memory-cached and
/// invalidated on activation), falling back to <see cref="ExceptionDefaultsDefinition.HardcodedFallback"/>
/// (Critical 14 / High 30 / Medium 45 / Low 60, window 24) when no active version exists. The interface is unchanged,
/// so the M6 raise call sites are untouched. Never throws.
/// </summary>
public sealed class ConfigBackedExceptionDefaults(IActiveConfigurationProvider activeProvider) : IExceptionDefaults
{
    private ExceptionDefaultsDefinition Current =>
        activeProvider.GetActive<ExceptionDefaultsDefinition>(ConfigurationDomains.ExceptionDefaults)
        ?? ExceptionDefaultsDefinition.HardcodedFallback;

    public int RecurrenceWindowMonths => Current.RecurrenceWindowMonths;

    public int TargetDays(ExceptionSeverity severity) => Current.TargetDays(severity);
}
