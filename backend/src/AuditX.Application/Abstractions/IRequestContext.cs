namespace AuditX.Application.Abstractions;

/// <summary>Per-request metadata captured for the audit trail and structured logging.</summary>
public interface IRequestContext
{
    /// <summary>Correlation id (W3C TraceContext) for this request.</summary>
    string RequestId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }
}
