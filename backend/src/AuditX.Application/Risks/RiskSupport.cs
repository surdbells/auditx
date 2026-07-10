using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Risks;

namespace AuditX.Application.Risks;

/// <summary>Derives the severity band from a likelihood×impact score (1–25) on a standard 5×5 matrix.</summary>
public static class RiskBands
{
    public static RiskBand Band(int score) => score switch
    {
        <= 4 => RiskBand.Low,
        <= 9 => RiskBand.Medium,
        <= 15 => RiskBand.High,
        _ => RiskBand.Critical,
    };

    /// <summary>Inclusive score bounds for a band (used to translate a band filter into a score range in SQL).</summary>
    public static (int Min, int Max) ScoreRange(RiskBand band) => band switch
    {
        RiskBand.Low => (1, 4),
        RiskBand.Medium => (5, 9),
        RiskBand.High => (10, 15),
        _ => (16, 25),
    };
}

internal static class RiskConcurrency
{
    public static void EnsureVersion(this Risk risk, string expectedVersion)
    {
        if (!string.Equals(RowVersionToken.Encode(risk.Version), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("risk.concurrency_conflict", "The risk was modified by someone else; reload and retry.");
        }
    }
}

internal static class RiskParsing
{
    public static RiskTreatmentStrategy? ParseStrategy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<RiskTreatmentStrategy>(value.Replace("_", string.Empty), ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw new DomainException("risk.invalid_strategy", $"Unknown treatment strategy '{value}'.");
    }

    public static RiskStatus ParseStatus(string? value)
        => Enum.TryParse<RiskStatus>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw new DomainException("risk.invalid_status", $"Unknown risk status '{value}'.");

    public static RiskStatus? ParseStatusFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ParseStatus(value);

    public static RiskBand? ParseBandFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<RiskBand>(value, ignoreCase: true, out var b) && Enum.IsDefined(b)
            ? b
            : throw new DomainException("risk.invalid_band", $"Unknown risk band '{value}'.");
    }
}
