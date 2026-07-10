namespace AuditX.Application.Common.Models;

/// <summary>
/// A page of results using offset/page pagination (1-based <see cref="Page"/> + fixed <see cref="PageSize"/> + a
/// <see cref="Total"/> row count), so a table can offer first / prev / next / last navigation and a page-size picker.
/// Supersedes the opaque-cursor <c>CursorPage</c>. A "load all" request is served as a single large page capped at
/// <see cref="PageSpec.LoadAllCap"/> rows.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    /// <summary>Total pages for this page size (always ≥ 1).</summary>
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int pageSize) =>
        new([], 0, 1, pageSize <= 0 ? PageSpec.DefaultPageSize : pageSize);

    /// <summary>Projects the items to another shape, preserving the page metadata (Total/Page/PageSize).</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) =>
        new(Items.Select(selector).ToArray(), Total, Page, PageSize);
}

/// <summary>
/// A normalised offset-pagination request. <c>pageSize ≤ 0</c> means "load all" — served as a single page capped at
/// <see cref="LoadAllCap"/> rows so a huge table (audit trail, exceptions) can never stall the server or browser.
/// </summary>
public sealed record PageSpec(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>The hard ceiling for a "load all" request. Rows beyond it are not returned (the UI shows a notice).</summary>
    public const int LoadAllCap = 5000;

    /// <summary>Rows to skip before this page.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>True when this spec is a capped "load all" request (page size at the cap).</summary>
    public bool IsLoadAll => PageSize >= LoadAllCap;

    /// <summary>
    /// Normalises raw query parameters: page defaults to 1; a null size → <see cref="DefaultPageSize"/>; a size ≤ 0 →
    /// a single "load all" page capped at <see cref="LoadAllCap"/>; any other size is clamped to <see cref="MaxPageSize"/>.
    /// </summary>
    public static PageSpec Of(int? page, int? pageSize)
    {
        var normalisedPage = page is null || page.Value < 1 ? 1 : page.Value;
        if (pageSize is null)
        {
            return new PageSpec(normalisedPage, DefaultPageSize);
        }

        if (pageSize.Value <= 0)
        {
            return new PageSpec(1, LoadAllCap);
        }

        return new PageSpec(normalisedPage, Math.Min(pageSize.Value, MaxPageSize));
    }
}
