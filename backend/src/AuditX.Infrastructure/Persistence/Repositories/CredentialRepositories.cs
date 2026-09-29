using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class UserCredentialRepository(AppDbContext db) : IUserCredentialRepository
{
    public Task<UserCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => db.UserCredentials.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public void Add(UserCredential credential) => db.UserCredentials.Add(credential);

    public void Remove(UserCredential credential) => db.UserCredentials.Remove(credential);
}

public sealed class PasswordHistoryRepository(AppDbContext db) : IPasswordHistoryRepository
{
    public async Task<IReadOnlyList<UserPasswordHistory>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        return await db.UserPasswordHistory
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.SetAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public void Add(UserPasswordHistory entry) => db.UserPasswordHistory.Add(entry);

    public void RemoveRange(IEnumerable<UserPasswordHistory> entries) => db.UserPasswordHistory.RemoveRange(entries);
}

public sealed class PasswordResetTokenRepository(AppDbContext db) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<PasswordResetToken>> GetUnconsumedForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.ConsumedAt == null)
            .ToListAsync(cancellationToken);

    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);
}
