using AuditX.Domain.Organization;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence for the organisational hierarchy (<see cref="OrgUnit"/>).</summary>
public interface IOrgUnitRepository
{
    Task<OrgUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrgUnit>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default);

    /// <summary>True if the (case-insensitive) code is already used by another org unit.</summary>
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);

    void Add(OrgUnit orgUnit);
}
