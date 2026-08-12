using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence operations for <see cref="Template"/> aggregates (items, sections, versions).</summary>
public interface ITemplateRepository
{
    /// <summary>Load a template with its items, sections and versions.</summary>
    Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Template?> GetByNameAndTypeAsync(string name, string auditType, CancellationToken cancellationToken = default);

    Task<PagedResult<Template>> SearchAsync(
        string? auditType,
        TemplateStatus? status,
        string? search,
        PageSpec page,
        CancellationToken cancellationToken = default);

    void Add(Template template);
}

/// <summary>Persistence operations for bank-configurable <see cref="RatingScale"/>s.</summary>
public interface IRatingScaleRepository
{
    Task<RatingScale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RatingScale?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RatingScale>> GetAllAsync(bool? activeOnly, CancellationToken cancellationToken = default);

    void Add(RatingScale scale);
}

/// <summary>Persistence operations for organisation-defined <see cref="ResponseOptionSet"/>s.</summary>
public interface IResponseOptionSetRepository
{
    Task<ResponseOptionSet?> GetByResponseTypeAsync(ResponseType responseType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResponseOptionSet>> GetAllAsync(CancellationToken cancellationToken = default);

    void Add(ResponseOptionSet set);
}
