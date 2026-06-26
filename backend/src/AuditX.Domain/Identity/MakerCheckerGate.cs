using AuditX.Domain.Common;

namespace AuditX.Domain.Identity;

/// <summary>
/// Configuration of a maker-checker gate for one action type (US-M1-020). Determines whether the
/// action requires dual control, which role may act as checker, and whether the maker may also be a
/// checker (default: no — and BR-M1-008 forbids self-approval on the same action instance regardless).
/// </summary>
public sealed class MakerCheckerGate : Entity
{
    private MakerCheckerGate()
    {
    }

    public string ActionType { get; private set; } = null!;

    public bool IsEnabled { get; private set; }

    /// <summary>Optional role name whose holders may approve; null means any user with the relevant permission.</summary>
    public string? CheckerRoleName { get; private set; }

    public bool AllowMakerAsChecker { get; private set; }

    public static MakerCheckerGate Create(string actionType, bool isEnabled, string? checkerRoleName = null)
    {
        return new MakerCheckerGate
        {
            ActionType = Guard.NotNullOrWhiteSpace(actionType, "gate.action_type_required", "Action type is required."),
            IsEnabled = isEnabled,
            CheckerRoleName = checkerRoleName,
            AllowMakerAsChecker = false,
        };
    }

    public void Configure(bool isEnabled, string? checkerRoleName, bool allowMakerAsChecker)
    {
        IsEnabled = isEnabled;
        CheckerRoleName = string.IsNullOrWhiteSpace(checkerRoleName) ? null : checkerRoleName.Trim();
        AllowMakerAsChecker = allowMakerAsChecker;
    }
}
