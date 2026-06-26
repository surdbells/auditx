namespace AuditX.Application.Abstractions;

/// <summary>Abstraction over the system clock so time-dependent logic is deterministically testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
