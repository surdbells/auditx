using System.Diagnostics;
using AuditX.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>Base controller that wraps results in the standard <see cref="ApiResponse{T}"/> envelope.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected string RequestId => Activity.Current?.Id ?? HttpContext.TraceIdentifier;

    protected OkObjectResult Envelope<T>(T data) => Ok(ApiResponse<T>.Create(data, RequestId));

    protected ObjectResult Created<T>(T data) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Create(data, RequestId));

    protected ObjectResult Accepted<T>(T data) =>
        StatusCode(StatusCodes.Status202Accepted, ApiResponse<T>.Create(data, RequestId));
}
