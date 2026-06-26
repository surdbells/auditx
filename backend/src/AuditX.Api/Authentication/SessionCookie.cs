namespace AuditX.Api.Authentication;

/// <summary>Helpers for the HttpOnly session cookie that carries the JWT (the SPA never reads the token).</summary>
public static class SessionCookie
{
    public const string Name = "auditx.session";

    public static void Write(HttpContext context, string token, DateTimeOffset expires)
        => context.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = "/",
            IsEssential = true,
        });

    public static void Clear(HttpContext context)
        => context.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
}
