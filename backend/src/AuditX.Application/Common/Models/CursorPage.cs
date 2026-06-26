namespace AuditX.Application.Common.Models;

/// <summary>A page of results using opaque-cursor pagination (default limit 20, max 100).</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore)
{
    public static CursorPage<T> Empty { get; } = new([], null, false);
}

/// <summary>Normalised pagination request parameters.</summary>
public sealed record PageRequest(string? Cursor, int Limit)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public static PageRequest Of(string? cursor, int? limit)
    {
        var effective = limit is null or <= 0 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);
        return new PageRequest(cursor, effective);
    }
}
