namespace AuditX.Api.Middleware;

/// <summary>
/// Adds baseline HTTP security-response headers to every response (defence-in-depth for a bank deployment):
/// nosniff, deny framing, a strict referrer policy, and a conservative permissions policy. HSTS is applied
/// separately by <c>UseHsts</c> (HTTPS only). The API serves JSON (the SPA is a separate origin), so a tight CSP
/// that forbids active content is safe.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        // The API only returns data (JSON / file downloads); forbid any active content from being interpreted.
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        headers.Remove("X-Powered-By");
        headers.Remove("Server");

        await next(context);
    }
}
