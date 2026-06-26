using AuditX.Application.Abstractions;

namespace AuditX.Infrastructure.Time;

/// <summary>Production clock backed by the operating system. Always returns UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
