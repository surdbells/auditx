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

    Task<CursorPage<Template>> SearchAsync(
        string? auditType,
        TemplateStatus? status,
        string? search,
        PageRequest page,
        CancellationToken cancellationToken = default);

    void Add(Template template);
}
