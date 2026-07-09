using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Application.Risks;
using AuditX.Domain.Enums;
using AuditX.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class RiskRepository(AppDbContext db) : IRiskRepository
{
    public Task<Risk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Risks.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<CursorPage<Risk>> SearchAsync(RiskSearchFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Risks.AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        if (!filter.IncludeClosed)
        {
            query = query.Where(r => r.Status != RiskStatus.Closed);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(r => r.Category == filter.Category);
        }

        if (filter.OwnerUserId is { } owner)
        {
            query = query.Where(r => r.OwnerUserId == owner);
        }

        if (filter.Band is { } band)
        {
            var (min, max) = RiskBands.ScoreRange(band);
            // Current score = residual (when assessed) else inherent, as a single translatable expression.
            query = query.Where(r =>
                (r.ResidualLikelihood ?? r.InherentLikelihood) * (r.ResidualImpact ?? r.InherentImpact) >= min &&
                (r.ResidualLikelihood ?? r.InherentLikelihood) * (r.ResidualImpact ?? r.InherentImpact) <= max);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Contains() translates to a LIKE with a proper ESCAPE clause + auto-escaped wildcards, so a term
            // containing % / _ / [ is matched literally rather than as a pattern operator.
            var term = filter.Search.Trim();
            query = query.Where(r => r.Title.Contains(term) || r.Category.Contains(term));
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

        return new CursorPage<Risk>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public void Add(Risk risk) => db.Risks.Add(risk);
}
