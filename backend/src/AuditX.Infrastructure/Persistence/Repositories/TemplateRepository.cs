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

    public async Task<CursorPage<Template>> SearchAsync(
        string? auditType,
        TemplateStatus? status,
        string? search,
        PageRequest page,
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
            query = query.Where(t => EF.Functions.Like(t.Name, $"%{term}%") || EF.Functions.Like(t.Description, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(t => t.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(t => t.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var nextCursor = hasMore ? items[^1].Id.ToString() : null;
        return new CursorPage<Template>(items, nextCursor, hasMore);
    }

    public void Add(Template template) => db.Templates.Add(template);
}
