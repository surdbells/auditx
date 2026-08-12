using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class TemplateRepository(AppDbContext db) : ITemplateRepository
{
    public Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Templates
            .Include(t => t.Items)
            .Include(t => t.Sections)
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Template?> GetByNameAndTypeAsync(string name, string auditType, CancellationToken cancellationToken = default)
        => db.Templates.FirstOrDefaultAsync(t => t.Name == name && t.AuditType == auditType, cancellationToken);

    public async Task<PagedResult<Template>> SearchAsync(
        string? auditType,
        TemplateStatus? status,
        string? search,
        PageSpec page,
        CancellationToken cancellationToken = default)
    {
        var query = db.Templates.AsNoTracking().Include(t => t.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(auditType))
        {
            query = query.Where(t => t.AuditType == auditType);
        }

        if (status is { } s)
        {
            query = query.Where(t => t.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => t.Name.Contains(term) || t.Description.Contains(term));
        }

        return await query.OrderBy(t => t.Id).ToPagedResultAsync(page, cancellationToken);
    }

    public void Add(Template template) => db.Templates.Add(template);
}

public sealed class RatingScaleRepository(AppDbContext db) : IRatingScaleRepository
{
    public Task<RatingScale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.RatingScales.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<RatingScale?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => db.RatingScales.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);

    public async Task<IReadOnlyList<RatingScale>> GetAllAsync(bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var query = db.RatingScales.AsNoTracking().AsQueryable();
        if (activeOnly is { } active)
        {
            query = query.Where(s => s.IsActive == active);
        }

        return await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
    }

    public void Add(RatingScale scale) => db.RatingScales.Add(scale);
}

public sealed class ResponseOptionSetRepository(AppDbContext db) : IResponseOptionSetRepository
{
    public Task<ResponseOptionSet?> GetByResponseTypeAsync(ResponseType responseType, CancellationToken cancellationToken = default)
        => db.ResponseOptionSets.FirstOrDefaultAsync(s => s.ResponseType == responseType, cancellationToken);

    public async Task<IReadOnlyList<ResponseOptionSet>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.ResponseOptionSets.AsNoTracking().OrderBy(s => s.ResponseType).ToListAsync(cancellationToken);

    public void Add(ResponseOptionSet set) => db.ResponseOptionSets.Add(set);
}
