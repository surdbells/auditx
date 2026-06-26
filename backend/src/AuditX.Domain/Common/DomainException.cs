namespace AuditX.Domain.Common;

/// <summary>
/// Raised when a domain invariant or business rule is violated. The <see cref="Code"/> is a stable,
/// machine-readable identifier surfaced to API clients as the RFC 7807 <c>error_code</c>.
/// </summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Raised when an operation is attempted that the entity's current state machine does not permit
/// (e.g. publishing an already-published template). Maps to HTTP 409 Conflict.
/// </summary>
public sealed class InvalidStateTransitionException(string code, string message)
    : DomainException(code, message);
