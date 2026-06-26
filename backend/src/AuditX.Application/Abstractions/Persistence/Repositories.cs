using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence operations for <see cref="User"/>. Implementations add to the unit of work; callers commit.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByObjectSidAsync(string objectSid, CancellationToken cancellationToken = default);

    Task<User?> GetBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken = default);

    Task<bool> HasAnyRoleAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Keyset-paginated search over users (US-M1/US-M15-004).</summary>
    Task<CursorPage<User>> SearchAsync(
        string? search,
        string? roleName,
        UserStatus? status,
        PageRequest page,
        CancellationToken cancellationToken = default);

    void Add(User user);
}

/// <summary>Persistence operations for <see cref="Role"/> and the inheritance graph.</summary>
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>The full role graph as (roleId → parentRoleIds), for cycle detection and inheritance resolution.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetInheritanceGraphAsync(CancellationToken cancellationToken = default);

    void Add(Role role);
}

/// <summary>Persistence operations for role assignments and delegations.</summary>
public interface IUserRoleRepository
{
    Task<UserRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRole>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid userId, Guid roleId, string? scopeValue, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRole>> GetExpiredDelegationsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);

    void Add(UserRole userRole);

    void Remove(UserRole userRole);
}

/// <summary>Persistence operations for maker-checker pending actions.</summary>
public interface IMakerCheckerRepository
{
    Task<MakerCheckerAction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MakerCheckerAction>> GetPendingAsync(string? actionType, CancellationToken cancellationToken = default);

    void Add(MakerCheckerAction action);
}

/// <summary>Access to the single-row deployment settings.</summary>
public interface IBankSettingsRepository
{
    Task<BankSettings> GetAsync(CancellationToken cancellationToken = default);
}
