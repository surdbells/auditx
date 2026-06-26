namespace AuditX.Application.Common.Exceptions;

/// <summary>Requested resource does not exist (or is not visible). Maps to HTTP 404.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string ErrorCode => "not_found";
}

/// <summary>Operation conflicts with current state (e.g. duplicate, optimistic-concurrency). Maps to HTTP 409.</summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string ErrorCode { get; } = code;
}

/// <summary>Authenticated principal lacks permission for the operation. Maps to HTTP 403.</summary>
public sealed class ForbiddenAccessException(string message = "You do not have permission to perform this action.")
    : Exception(message)
{
    public string ErrorCode => "forbidden";
}

/// <summary>Caller is not authenticated. Maps to HTTP 401.</summary>
public sealed class UnauthorizedException(string code = "unauthorized", string message = "Authentication is required.")
    : Exception(message)
{
    public string ErrorCode { get; } = code;
}
