namespace AuditX.Application.Identity.Passwords;

/// <summary>The complexity requirements a candidate password must satisfy, drawn from the institution policy.</summary>
public sealed record PasswordComplexitySpec(
    int MinLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireDigit,
    bool RequireSymbol);

/// <summary>
/// Stateless evaluator of a candidate password against the institution's complexity policy. Returns a list
/// of human-readable violations (empty when the password satisfies the policy). History and expiry are
/// enforced separately by the credential command handlers, which hold the stored hashes and clock.
/// </summary>
public static class PasswordPolicyEvaluator
{
    public static IReadOnlyList<string> Validate(PasswordComplexitySpec policy, string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("A password is required.");
            return errors;
        }

        if (password.Length < policy.MinLength)
        {
            errors.Add($"Password must be at least {policy.MinLength} characters long.");
        }

        if (policy.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter.");
        }

        if (policy.RequireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("Password must contain at least one lowercase letter.");
        }

        if (policy.RequireDigit && !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one digit.");
        }

        if (policy.RequireSymbol && !password.Any(c => !char.IsLetterOrDigit(c)))
        {
            errors.Add("Password must contain at least one symbol.");
        }

        return errors;
    }

    /// <summary>True when the candidate satisfies every complexity requirement.</summary>
    public static bool IsSatisfiedBy(PasswordComplexitySpec policy, string? password)
        => Validate(policy, password).Count == 0;
}
