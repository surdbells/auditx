using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Identity.Passwords;
using AuditX.Domain.Identity;
using FluentValidation.Results;

namespace AuditX.Application.Identity.Services;

/// <summary>
/// Shared local-password mechanics used by self-service and admin flows: build the complexity spec from the
/// institution policy, validate a candidate (complexity + no reuse of the current or recent hashes), and apply
/// a new password (create-or-replace the credential, push the outgoing hash to history, trim history to depth).
/// </summary>
public sealed class PasswordService(
    IUserCredentialRepository credentials,
    IPasswordHistoryRepository history,
    IPasswordHasher hasher,
    IClock clock)
{
    public static PasswordComplexitySpec SpecFrom(InstitutionSettings s)
        => new(s.PasswordMinLength, s.PasswordRequireUppercase, s.PasswordRequireLowercase, s.PasswordRequireDigit, s.PasswordRequireSymbol);

    /// <summary>Validate complexity and reuse; throws <see cref="FluentValidation.ValidationException"/> (→ 422) on failure.</summary>
    public async Task ValidateAsync(Guid userId, string newPassword, InstitutionSettings settings, CancellationToken cancellationToken)
    {
        var errors = PasswordPolicyEvaluator.Validate(SpecFrom(settings), newPassword);

        if (errors.Count == 0 && settings.PasswordHistoryDepth > 0 && await IsReuseAsync(userId, newPassword, settings.PasswordHistoryDepth, cancellationToken))
        {
            errors = [.. errors, $"Password must not repeat any of your last {settings.PasswordHistoryDepth} passwords."];
        }

        if (errors.Count > 0)
        {
            throw new FluentValidation.ValidationException(errors.Select(e => new ValidationFailure("newPassword", e)));
        }
    }

    private async Task<bool> IsReuseAsync(Guid userId, string newPassword, int depth, CancellationToken cancellationToken)
    {
        var current = await credentials.GetByUserIdAsync(userId, cancellationToken);
        if (current is not null && hasher.Verify(current.PasswordHash, newPassword).Succeeded)
        {
            return true;
        }

        var recent = await history.GetRecentAsync(userId, depth, cancellationToken);
        return recent.Any(h => hasher.Verify(h.PasswordHash, newPassword).Succeeded);
    }

    /// <summary>
    /// Create-or-replace the user's credential with a hash of <paramref name="newPassword"/>. The outgoing hash is
    /// pushed to history and history trimmed to the policy depth. Callers commit via the unit of work.
    /// </summary>
    public async Task<UserCredential> ApplyAsync(Guid userId, string newPassword, bool mustChange, InstitutionSettings settings, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var newHash = hasher.Hash(newPassword);
        var credential = await credentials.GetByUserIdAsync(userId, cancellationToken);

        if (credential is null)
        {
            credential = UserCredential.Create(userId, newHash, mustChange, now);
            credentials.Add(credential);
        }
        else
        {
            history.Add(UserPasswordHistory.Record(userId, credential.PasswordHash, now));
            credential.SetPassword(newHash, mustChange, now);
        }

        await TrimHistoryAsync(userId, settings.PasswordHistoryDepth, cancellationToken);
        return credential;
    }

    private async Task TrimHistoryAsync(Guid userId, int depth, CancellationToken cancellationToken)
    {
        var all = await history.GetRecentAsync(userId, int.MaxValue, cancellationToken); // newest first
        if (all.Count > depth)
        {
            history.RemoveRange(all.Skip(depth));
        }
    }
}
