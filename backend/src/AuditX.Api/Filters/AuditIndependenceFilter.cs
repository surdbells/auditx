using AuditX.Api.Controllers;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Execution;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuditX.Api.Filters;

/// <summary>
/// Independence, enforced once for every request that CHANGES an audit: the audit's own auditee may not. It covers
/// every non-read route addressed to an audit — <c>api/v1/audits/{id}/…</c> on the audits controller and any route
/// carrying an <c>{auditId}</c> (its findings, procedures, time, document requests, reports) — so a new endpoint
/// added under an audit is protected by default rather than by remembering to add a check.
///
/// The global <c>[RequirePermission]</c> check cannot do this: an auditor or audit manager holds ManageAudit and
/// RaiseException everywhere, including on the audit of their own area. What an auditee legitimately does lives on
/// other routes (answering a document request, the action plan for a finding they own) and is untouched; a
/// self-assessment has no independent auditor and is exempt. Actions addressed by something other than the audit
/// (a finding, a report, a procedure by id) are guarded in their handlers.
/// </summary>
public sealed class AuditIndependenceFilter(IAuditIndependenceReader independence, ICurrentUser currentUser) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        var isRead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

        if (!isRead
            && TryGetAuditId(context, out var auditId)
            && currentUser.UserId is { } userId
            && await independence.IsAuditeeAsync(auditId, userId, context.HttpContext.RequestAborted))
        {
            throw new ForbiddenAccessException(AuditIndependence.Message);
        }

        await next();
    }

    private static bool TryGetAuditId(ActionExecutingContext context, out Guid auditId)
    {
        var values = context.RouteData.Values;
        if (values.TryGetValue("auditId", out var named) && Guid.TryParse(named?.ToString(), out auditId))
        {
            return true;
        }

        // On the audits controller the audit is simply {id}.
        if (context.Controller is AuditsController && values.TryGetValue("id", out var id) && Guid.TryParse(id?.ToString(), out auditId))
        {
            return true;
        }

        auditId = default;
        return false;
    }
}
