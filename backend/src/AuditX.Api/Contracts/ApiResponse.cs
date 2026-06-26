namespace AuditX.Api.Contracts;

/// <summary>Standard success envelope: <c>{ "data": ..., "metadata": { "request_id": ... } }</c> (per the PRD).</summary>
public sealed record ApiResponse<T>(T Data, ApiMetadata Metadata)
{
    public static ApiResponse<T> Create(T data, string requestId) => new(data, new ApiMetadata(requestId, DateTimeOffset.UtcNow));
}

public sealed record ApiMetadata(string RequestId, DateTimeOffset GeneratedAt);
