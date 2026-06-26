using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuditX.Api.Authorization;

/// <summary>
/// Gates an action (or controller) behind a permission key. Returns 401 when unauthenticated and 403
/// when the authenticated user lacks the permission (BR-M1-001), with no resource-existence leak.
/// Resource-scoped checks (e.g. audit-scoped permissions) are enforced inside the relevant handlers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : Attribute, IFilterFactory
{
    public string Permission => permission;

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) => new PermissionFilter(
        permission,
        serviceProvider.GetRequiredService<ICurrentUser>(),
        serviceProvider.GetRequiredService<IPermissionResolver>());

    private sealed class PermissionFilter(string permission, ICurrentUser currentUser, IPermissionResolver resolver)
        : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (currentUser.UserId is not { } userId)
            {
                throw new UnauthorizedException();
            }

            if (!await resolver.HasPermissionAsync(userId, permission, scopeValue: null, context.HttpContext.RequestAborted))
            {
                throw new ForbiddenAccessException();
            }
        }
    }
}
