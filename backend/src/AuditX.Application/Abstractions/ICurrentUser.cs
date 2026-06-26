namespace AuditX.Application.Abstractions;

/// <summary>The principal making the current request, derived from the validated session JWT.</summary>
public interface ICurrentUser
{
    /// <summary>The authenticated user's id, or <c>null</c> for anonymous/system contexts.</summary>
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    /// <summary>JWT id (jti) of the current session token, used for denylisting on logout.</summary>
    string? SessionTokenId { get; }

    /// <summary>Sliding expiry of the current session token, from its claims.</summary>
    DateTimeOffset? SessionExpiresAt { get; }

    /// <summary>Absolute (non-extendable) expiry of the current session token, from its claims.</summary>
    DateTimeOffset? SessionAbsoluteExpiresAt { get; }
}
