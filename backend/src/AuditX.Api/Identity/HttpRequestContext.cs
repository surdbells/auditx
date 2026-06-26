using System.Diagnostics;
using AuditX.Application.Abstractions;

namespace AuditX.Api.Identity;

/// <summary>Captures per-request metadata (correlation id, client IP, user agent) for the audit trail.</summary>
public sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string RequestId => Activity.Current?.Id ?? accessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
