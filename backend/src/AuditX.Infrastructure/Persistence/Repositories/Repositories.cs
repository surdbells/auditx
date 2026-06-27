using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByObjectSidAsync(string objectSid, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(u => u.AdObjectSid == objectSid, cancellationToken);

    public Task<User?> GetBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(u => u.AdSamAccountName == samAccountName, cancellationToken);

    public Task<bool> HasAnyRoleAsync(Guid userId, CancellationToken cancellationToken = default)
        => db.UserRoles.AnyAsync(ur => ur.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> GetActiveByRoleNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var roleIds = db.Roles.Where(r => r.Name == roleName).Select(r => r.Id);
        var userIds = db.UserRoles.Where(ur => ur.IsActive && roleIds.Contains(ur.RoleId)).Select(ur => ur.UserId);
        return await db.Users
            .Where(u => userIds.Contains(u.Id) && u.Status != UserStatus.Deactivated)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByObjectSidAsync(string objectSid, CancellationToken cancellationToken = default)
        => db.Users.IgnoreQueryFilters().AnyAsync(u => u.AdObjectSid == objectSid, cancellationToken);

    public async Task<IReadOnlyDictionary<string, Guid>> GetIdsByEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0)
        {
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        }

        var matches = await db.Users.Where(u => emails.Contains(u.Email)).Select(u => new { u.Email, u.Id }).ToListAsync(cancellationToken);
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in matches)
        {
            result[match.Email] = match.Id;
        }

        return result;
    }

    public async Task<CursorPage<User>> SearchAsync(
        string? search,
        string? roleName,
        UserStatus? status,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                EF.Functions.Like(u.FirstName, $"%{term}%") ||
                EF.Functions.Like(u.LastName, $"%{term}%") ||
                EF.Functions.Like(u.DisplayName, $"%{term}%") ||
                EF.Functions.Like(u.Email, $"%{term}%"));
        }

        if (status is { } s)
        {
            query = query.Where(u => u.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var name = roleName.Trim();
            query = query.Where(u => db.UserRoles.Any(ur =>
                ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == name)));
        }

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(u => u.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(u => u.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var nextCursor = hasMore ? items[^1].Id.ToString() : null;
        return new CursorPage<User>(items, nextCursor, hasMore);
    }

    public void Add(User user) => db.Users.Add(user);
}

public sealed class RoleRepository(AppDbContext db) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = db.Roles.Include(r => r.Permissions).AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(r => !r.IsArchived);
        }

        return await query.OrderBy(r => r.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => await db.Roles.Include(r => r.Permissions).Where(r => ids.Contains(r.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetInheritanceGraphAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Roles.AsNoTracking()
            .Select(r => new { r.Id, Parents = r.ParentRoleIds })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => (IReadOnlyList<Guid>)r.Parents.ToList());
    }

    public void Add(Role role) => db.Roles.Add(role);
}

public sealed class UserRoleRepository(AppDbContext db) : IUserRoleRepository
{
    public Task<UserRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.UserRoles.FirstOrDefaultAsync(ur => ur.Id == id, cancellationToken);

    public async Task<IReadOnlyList<UserRole>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid userId, Guid roleId, string? scopeValue, CancellationToken cancellationToken = default)
        => db.UserRoles.AnyAsync(
            ur => ur.UserId == userId && ur.RoleId == roleId && ur.ScopeValue == scopeValue && ur.DelegatedFromUserId == null,
            cancellationToken);

    public async Task<IReadOnlyList<UserRole>> GetExpiredDelegationsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
        => await db.UserRoles
            .Where(ur => ur.DelegatedFromUserId != null && ur.IsActive && ur.DelegationEnd != null && ur.DelegationEnd <= asOfUtc)
            .ToListAsync(cancellationToken);

    public void Add(UserRole userRole) => db.UserRoles.Add(userRole);

    public void Remove(UserRole userRole) => db.UserRoles.Remove(userRole);
}

public sealed class MakerCheckerRepository(AppDbContext db) : IMakerCheckerRepository
{
    public Task<MakerCheckerAction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.MakerCheckerActions.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MakerCheckerAction>> GetPendingAsync(string? actionType, CancellationToken cancellationToken = default)
    {
        var query = db.MakerCheckerActions.Where(a => a.Status == MakerCheckerStatus.Pending);
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(a => a.ActionType == actionType);
        }

        return await query.OrderBy(a => a.CreatedAt).ToListAsync(cancellationToken);
    }

    public void Add(MakerCheckerAction action) => db.MakerCheckerActions.Add(action);
}

public sealed class MakerCheckerGateRepository(AppDbContext db) : IMakerCheckerGateRepository
{
    public Task<MakerCheckerGate?> GetByActionTypeAsync(string actionType, CancellationToken cancellationToken = default)
        => db.MakerCheckerGates.FirstOrDefaultAsync(g => g.ActionType == actionType, cancellationToken);

    public async Task<IReadOnlyList<MakerCheckerGate>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.MakerCheckerGates.OrderBy(g => g.ActionType).ToListAsync(cancellationToken);

    public void Add(MakerCheckerGate gate) => db.MakerCheckerGates.Add(gate);
}

public sealed class BankSettingsRepository(AppDbContext db) : IBankSettingsRepository
{
    public async Task<BankSettings> GetAsync(CancellationToken cancellationToken = default)
        => await db.BankSettings.FirstOrDefaultAsync(cancellationToken)
           ?? throw new InvalidOperationException("Bank settings have not been seeded.");
}
