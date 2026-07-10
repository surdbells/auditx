using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Risks;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Filter dimensions for the risk register list (P1-A).</summary>
public sealed record RiskSearchFilter(
    RiskStatus? Status = null,
    string? Category = null,
    Guid? OwnerUserId = null,
    RiskBand? Band = null,
    bool IncludeClosed = true,
    string? Search = null);

/// <summary>Persistence port for the enterprise <see cref="Risk"/> register.</summary>
public interface IRiskRepository
{
    Task<Risk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Risk>> SearchAsync(RiskSearchFilter filter, PageSpec page, CancellationToken cancellationToken = default);

    void Add(Risk risk);
}
