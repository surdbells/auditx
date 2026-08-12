using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Exceptions;

/// <summary>
/// A bank-configurable rule for which finalised responses of a given <see cref="ResponseType"/> are eligible
/// to have an exception raised against them (M6). A Fail verdict is always eligible — that base rule is
/// non-negotiable and enforced unconditionally in the application layer, not here. This rule only EXTENDS
/// eligibility for a response type beyond that base case: an N/A verdict, and/or (for value types that carry a
/// <see cref="Audits.ChecklistResponse.Score"/>, i.e. Rating) a score at or below a configured threshold — a
/// poor rating can warrant a finding even with no explicit Fail verdict. One rule per <see cref="ResponseType"/>.
/// </summary>
public sealed class ExceptionRaisingRule : AggregateRoot
{
    private ExceptionRaisingRule()
    {
    }

    public ResponseType ResponseType { get; private set; }

    public bool AllowOnNa { get; private set; }

    /// <summary>A finalised response scoring at or below this (0-100) is exception-eligible regardless of verdict. Null = not score-gated.</summary>
    public decimal? ScoreThreshold { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static ExceptionRaisingRule Create(ResponseType responseType, bool allowOnNa, decimal? scoreThreshold)
    {
        ValidateThreshold(scoreThreshold);
        return new ExceptionRaisingRule
        {
            ResponseType = responseType,
            AllowOnNa = allowOnNa,
            ScoreThreshold = scoreThreshold,
            IsActive = true,
        };
    }

    public void Update(bool allowOnNa, decimal? scoreThreshold, bool isActive)
    {
        ValidateThreshold(scoreThreshold);
        AllowOnNa = allowOnNa;
        ScoreThreshold = scoreThreshold;
        IsActive = isActive;
    }

    /// <summary>True when a finalised response with this verdict/score meets this rule's extended eligibility (the caller still ORs this with the unconditional Fail rule).</summary>
    public bool IsEligible(ResponseVerdict? verdict, decimal? score)
    {
        if (!IsActive)
        {
            return false;
        }

        if (verdict == ResponseVerdict.Na && AllowOnNa)
        {
            return true;
        }

        return ScoreThreshold is { } threshold && score is { } s && s <= threshold;
    }

    private static void ValidateThreshold(decimal? threshold)
    {
        if (threshold is { } t && (t < 0 || t > 100))
        {
            throw new DomainException("exception_rule.threshold_range", "Score threshold must be between 0 and 100.");
        }
    }
}
