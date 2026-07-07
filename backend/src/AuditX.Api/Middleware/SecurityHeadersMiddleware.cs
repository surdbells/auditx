namespace AuditX.Api.Middleware;

/// <summary>
/// Adds baseline HTTP security-response headers to every response (defence-in-depth for a bank deployment):
/// nosniff, deny framing, a strict referrer policy, and a conservative permissions policy. HSTS is applied
/// separately by <c>UseHsts</c> (HTTPS only). The API serves JSON (the SPA is a separate origin), so a tight CSP
/// that forbids active content is safe.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // Default: the API only returns data (JSON / file downloads); forbid any active content from being interpreted.
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    // The Swagger UI documentation page (Development only) is a real HTML app: it must load its own bundled
    // scripts/styles, apply inline styles, and fetch the OpenAPI document + issue "Try it out" calls to this same
    // origin. A "default-src 'none'" policy renders it blank, so scope a self-only policy to the /swagger path.
    private const string SwaggerContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'none'";

    public async Task Invoke(HttpContext context)
    {
        var isSwagger = context.Request.Path.StartsWithSegments("/swagger", System.StringComparison.OrdinalIgnoreCase);

        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        headers["Content-Security-Policy"] = isSwagger ? SwaggerContentSecurityPolicy : ApiContentSecurityPolicy;
        headers.Remove("X-Powered-By");
        headers.Remove("Server");

        await next(context);
    }
}
