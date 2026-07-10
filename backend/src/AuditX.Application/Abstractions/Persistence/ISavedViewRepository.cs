using AuditX.Domain.SavedViews;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence port for <see cref="SavedView"/> (D3-A saved filter/parameter sets).</summary>
public interface ISavedViewRepository
{
    /// <summary>Loads a tracked view for mutation (soft-deleted rows are excluded by the global filter).</summary>
    Task<SavedView?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The views visible to <paramref name="userId"/> for a screen: their own views plus any shared views, ordered
    /// owned-first then by name.
    /// </summary>
    Task<IReadOnlyList<SavedView>> ListVisibleAsync(Guid userId, string viewKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// True if the owner already has a live view with this name on this screen (case-insensitive per the column
    /// collation), optionally excluding one id (for renames). Backs the friendly duplicate-name 409.
    /// </summary>
    Task<bool> OwnedNameExistsAsync(Guid ownerUserId, string viewKey, string name, Guid? excludingId, CancellationToken cancellationToken = default);

    void Add(SavedView view);
}
