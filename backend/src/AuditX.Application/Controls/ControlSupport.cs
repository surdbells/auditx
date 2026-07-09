using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Common;
using AuditX.Domain.Controls;
using AuditX.Domain.Enums;

namespace AuditX.Application.Controls;

internal static class ControlConcurrency
{
    public static void EnsureVersion(this Control control, string expectedVersion)
    {
        if (!string.Equals(Convert.ToBase64String(control.Version ?? []), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("control.concurrency_conflict", "The control was modified by someone else; reload and retry.");
        }
    }
}

internal static class ControlParsing
{
    public static ControlType ParseType(string? value)
        => EnumExtensions.TryParseSnake<ControlType>(value, out var v)
            ? v
            : throw new DomainException("control.invalid_type", $"Unknown control type '{value}'.");

    public static ControlFrequency ParseFrequency(string? value)
        => EnumExtensions.TryParseSnake<ControlFrequency>(value, out var v)
            ? v
            : throw new DomainException("control.invalid_frequency", $"Unknown control frequency '{value}'.");

    public static ControlEffectiveness ParseEffectiveness(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? ControlEffectiveness.NotTested
            : EnumExtensions.TryParseSnake<ControlEffectiveness>(value, out var v)
                ? v
                : throw new DomainException("control.invalid_effectiveness", $"Unknown effectiveness '{value}'.");

    public static ControlType? ParseTypeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ParseType(value);

    public static ControlEffectiveness? ParseEffectivenessFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null
            : EnumExtensions.TryParseSnake<ControlEffectiveness>(value, out var v) ? v
            : throw new DomainException("control.invalid_effectiveness", $"Unknown effectiveness '{value}'.");
}
