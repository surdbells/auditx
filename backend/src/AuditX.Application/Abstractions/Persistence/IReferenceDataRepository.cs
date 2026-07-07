using AuditX.Domain.ReferenceData;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence for the generic managed reference-data store (audit types, exception categories, …).</summary>
public interface IReferenceDataRepository
{
    /// <summary>Items in a category ordered by (sort order, label). Active-only unless <paramref name="includeInactive"/>.</summary>
    Task<IReadOnlyList<ReferenceDataItem>> ListByCategoryAsync(string category, bool includeInactive, CancellationToken cancellationToken = default);

    Task<ReferenceDataItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Whether a live (non-deleted) item with this (category, code) already exists — the uniqueness pre-check.</summary>
    Task<bool> ExistsAsync(string category, string code, CancellationToken cancellationToken = default);

    void Add(ReferenceDataItem item);
}
