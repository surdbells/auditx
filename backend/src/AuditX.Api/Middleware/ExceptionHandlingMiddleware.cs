using System.Diagnostics;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Middleware;

/// <summary>
/// Translates exceptions into RFC 7807 <c>application/problem+json</c> responses with a stable
/// <c>error_code</c>, the correlation <c>request_id</c>, and <c>field_errors</c> for validation
/// failures. Unhandled exceptions are logged and returned as an opaque 500.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private const string ProblemContentType = "application/problem+json";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var requestId = Activity.Current?.Id ?? context.TraceIdentifier;
        var (status, errorCode, title, detail, fieldErrors) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception ({RequestId})", requestId);
        }
        else
        {
            logger.LogInformation("Request failed ({RequestId}): {ErrorCode} - {Detail}", requestId, errorCode, detail);
        }

        var problem = new ProblemDetails
        {
            Type = $"https://docs.auditx.local/errors/{errorCode}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["request_id"] = requestId;
        problem.Extensions["error_code"] = errorCode;
        if (fieldErrors is not null)
        {
            problem.Extensions["field_errors"] = fieldErrors;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = ProblemContentType;
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static (int Status, string ErrorCode, string Title, string Detail, object? FieldErrors) Map(Exception exception) => exception switch
    {
        ValidationException validation => (
            StatusCodes.Status422UnprocessableEntity,
            "validation_failed",
            "Validation failed",
            "One or more fields are invalid.",
            validation.Errors.Select(e => new
            {
                field = ToCamelCase(e.PropertyName),
                code = e.ErrorCode,
                message = e.ErrorMessage,
            }).ToArray()),
        UnauthorizedException unauthorized => (StatusCodes.Status401Unauthorized, unauthorized.ErrorCode, "Unauthorized", unauthorized.Message, null),
        ForbiddenAccessException forbidden => (StatusCodes.Status403Forbidden, forbidden.ErrorCode, "Forbidden", forbidden.Message, null),
        NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.ErrorCode, "Not found", notFound.Message, null),
        ConflictException conflict => (StatusCodes.Status409Conflict, conflict.ErrorCode, "Conflict", conflict.Message, null),
        InvalidStateTransitionException invalid => (StatusCodes.Status409Conflict, invalid.Code, "Invalid state transition", invalid.Message, null),
        PayloadTooLargeException tooLarge => (StatusCodes.Status413PayloadTooLarge, tooLarge.ErrorCode, "Payload too large", tooLarge.Message, null),
        EvidenceLockedException locked => (StatusCodes.Status423Locked, locked.ErrorCode, "Locked", locked.Message, null),
        EvidenceIntegrityException integrity => (StatusCodes.Status500InternalServerError, integrity.ErrorCode, "Evidence integrity failure", integrity.Message, null),
        ReportIntegrityException reportIntegrity => (StatusCodes.Status500InternalServerError, reportIntegrity.ErrorCode, "Report integrity failure", reportIntegrity.Message, null),
        DomainException domain => (StatusCodes.Status422UnprocessableEntity, domain.Code, "Business rule violation", domain.Message, null),
        Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrency_conflict", "Conflict", "The record was modified by someone else; reload and retry.", null),
        _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred", "An unexpected error occurred. Please contact support with the request id.", null),
    };

    private static string ToCamelCase(string value)
        => string.IsNullOrEmpty(value) || char.IsLower(value[0]) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
