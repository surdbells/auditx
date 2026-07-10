using AuditX.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence;

/// <summary>Materialises an ORDERED, filtered query into an offset <see cref="PagedResult{T}"/> (count + skip/take).</summary>
public static class PagedQueryExtensions
{
    /// <summary>
    /// Counts the query then returns the requested page. The query MUST already be ordered (offset paging over an
    /// unordered source is non-deterministic). Runs the count and the page as two round-trips against the same filter.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageSpec page, CancellationToken cancellationToken = default)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, total, page.Page, page.PageSize);
    }
}
