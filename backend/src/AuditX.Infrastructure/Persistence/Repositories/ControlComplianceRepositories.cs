using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Compliance;
using AuditX.Domain.Controls;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class ControlRepository(AppDbContext db) : IControlRepository
{
    public Task<Control?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Controls.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<CursorPage<Control>> SearchAsync(ControlSearchFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Controls.AsNoTracking();

        if (filter.Type is { } type)
        {
            query = query.Where(c => c.ControlType == type);
        }

        if (filter.Effectiveness is { } eff)
        {
            query = query.Where(c => c.Effectiveness == eff);
        }

        if (filter.OwnerUserId is { } owner)
        {
            query = query.Where(c => c.OwnerUserId == owner);
        }

        if (!filter.IncludeRetired)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(c => c.Code.Contains(term) || c.Title.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(c => c.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(c => c.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<Control>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken = default)
        => db.Controls.AnyAsync(c => c.Code == code && (excludingId == null || c.Id != excludingId), cancellationToken);

    public void Add(Control control) => db.Controls.Add(control);
}

public sealed class RegulationRepository(AppDbContext db) : IRegulationRepository
{
    public Task<Regulation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Regulations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<CursorPage<Regulation>> SearchAsync(RegulationSearchFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Regulations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(r => r.Category == filter.Category);
        }

        if (!filter.IncludeRetired)
        {
            query = query.Where(r => r.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(r => r.Code.Contains(term) || r.Name.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(r => r.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(r => r.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<Regulation>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken = default)
        => db.Regulations.AnyAsync(r => r.Code == code && (excludingId == null || r.Id != excludingId), cancellationToken);

    public void Add(Regulation regulation) => db.Regulations.Add(regulation);
}

public sealed class FindingLinkRepository(AppDbContext db) : IFindingLinkRepository
{
    public async Task<IReadOnlyList<FindingControlLinkRow>> ListControlLinksAsync(Guid exceptionId, CancellationToken cancellationToken = default)
        => await db.ExceptionControlLinks.AsNoTracking()
            .Where(l => l.ExceptionId == exceptionId)
            .Join(db.Controls, l => l.ControlId, c => c.Id, (l, c) => new { l, c })
            .OrderBy(x => x.c.Code)
            .Select(x => new FindingControlLinkRow(x.l.Id, x.c.Id, x.c.Code, x.c.Title, x.l.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FindingRegulationLinkRow>> ListRegulationLinksAsync(Guid exceptionId, CancellationToken cancellationToken = default)
        => await db.ExceptionRegulationLinks.AsNoTracking()
            .Where(l => l.ExceptionId == exceptionId)
            .Join(db.Regulations, l => l.RegulationId, r => r.Id, (l, r) => new { l, r })
            .OrderBy(x => x.r.Code)
            .Select(x => new FindingRegulationLinkRow(x.l.Id, x.r.Id, x.r.Code, x.r.Name, x.l.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<ExceptionControlLink?> GetControlLinkAsync(Guid exceptionId, Guid controlId, CancellationToken cancellationToken = default)
        => db.ExceptionControlLinks.FirstOrDefaultAsync(l => l.ExceptionId == exceptionId && l.ControlId == controlId, cancellationToken);

    public Task<ExceptionRegulationLink?> GetRegulationLinkAsync(Guid exceptionId, Guid regulationId, CancellationToken cancellationToken = default)
        => db.ExceptionRegulationLinks.FirstOrDefaultAsync(l => l.ExceptionId == exceptionId && l.RegulationId == regulationId, cancellationToken);

    public void AddControlLink(ExceptionControlLink link) => db.ExceptionControlLinks.Add(link);

    public void AddRegulationLink(ExceptionRegulationLink link) => db.ExceptionRegulationLinks.Add(link);

    public void RemoveControlLink(ExceptionControlLink link) => db.ExceptionControlLinks.Remove(link);

    public void RemoveRegulationLink(ExceptionRegulationLink link) => db.ExceptionRegulationLinks.Remove(link);
}
