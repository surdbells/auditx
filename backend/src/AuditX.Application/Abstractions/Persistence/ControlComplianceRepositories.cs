using AuditX.Application.Common.Models;
using AuditX.Domain.Compliance;
using AuditX.Domain.Controls;
using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Filter dimensions for the controls register (P1-B).</summary>
public sealed record ControlSearchFilter(
    ControlType? Type = null,
    ControlEffectiveness? Effectiveness = null,
    Guid? OwnerUserId = null,
    bool IncludeRetired = true,
    string? Search = null);

public interface IControlRepository
{
    Task<Control?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CursorPage<Control>> SearchAsync(ControlSearchFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>True if another live control already uses this code (unique business key).</summary>
    Task<bool> CodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken = default);

    void Add(Control control);
}

/// <summary>Filter dimensions for the regulation / compliance register (P1-B).</summary>
public sealed record RegulationSearchFilter(
    string? Category = null,
    bool IncludeRetired = true,
    string? Search = null);

public interface IRegulationRepository
{
    Task<Regulation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CursorPage<Regulation>> SearchAsync(RegulationSearchFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken = default);

    void Add(Regulation regulation);
}

/// <summary>Enriched finding↔control link row (joined to the control for display).</summary>
public sealed record FindingControlLinkRow(Guid LinkId, Guid ControlId, string Code, string Title, DateTimeOffset LinkedAt);

/// <summary>Enriched finding↔regulation link row (joined to the regulation for display).</summary>
public sealed record FindingRegulationLinkRow(Guid LinkId, Guid RegulationId, string Code, string Name, DateTimeOffset LinkedAt);

/// <summary>Persistence port for the finding↔control and finding↔regulation link rows (P1-B).</summary>
public interface IFindingLinkRepository
{
    Task<IReadOnlyList<FindingControlLinkRow>> ListControlLinksAsync(Guid exceptionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FindingRegulationLinkRow>> ListRegulationLinksAsync(Guid exceptionId, CancellationToken cancellationToken = default);

    Task<ExceptionControlLink?> GetControlLinkAsync(Guid exceptionId, Guid controlId, CancellationToken cancellationToken = default);

    Task<ExceptionRegulationLink?> GetRegulationLinkAsync(Guid exceptionId, Guid regulationId, CancellationToken cancellationToken = default);

    void AddControlLink(ExceptionControlLink link);

    void AddRegulationLink(ExceptionRegulationLink link);

    void RemoveControlLink(ExceptionControlLink link);

    void RemoveRegulationLink(ExceptionRegulationLink link);
}
