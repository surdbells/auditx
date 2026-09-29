using AuditX.Api.Authentication;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Authentication;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Passwords;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuditX.Api.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController(IDispatcher dispatcher) : ApiControllerBase
{
    /// <summary>Forms-fallback login: AD credentials validated by an LDAP bind. Sets the session cookie.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new LoginCommand(request.Username, request.Password), cancellationToken);
        WriteSession(result);
        return Envelope(result.Session);
    }

    /// <summary>Kerberos/IWA SSO. Requires Windows authentication; sets the session cookie on success.</summary>
    [Authorize(AuthenticationSchemes = AuthenticationSetup.NegotiateScheme)]
    [HttpGet("sso")]
    public async Task<IActionResult> Sso(CancellationToken cancellationToken)
    {
        var accountName = User.Identity?.Name
            ?? throw new UnauthorizedException("sso_unresolved", "Windows identity was not provided.");
        var result = await dispatcher.Send(new SsoLoginCommand(accountName), cancellationToken);
        WriteSession(result);
        return Envelope(result.Session);
    }

    /// <summary>Logout: revokes the current token and clears the cookie.</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await dispatcher.Send(new LogoutCommand(), cancellationToken);
        SessionCookie.Clear(HttpContext);
        return NoContent();
    }

    /// <summary>Current session, re-verifying AD account status on each call.</summary>
    [Authorize]
    [HttpGet("session")]
    public async Task<IActionResult> Session(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetSessionQuery(), cancellationToken));

    /// <summary>Change the signed-in local user's password; re-issues the session cookie with a fresh stamp.</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken);
        WriteSession(result);
        return Envelope(result.Session);
    }

    /// <summary>Request a password-reset link. Always succeeds (no account-existence disclosure).</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ForgotPasswordCommand(request.UsernameOrEmail), cancellationToken);
        return NoContent();
    }

    /// <summary>Set a new password from a valid reset/invite link.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ResetPasswordCommand(request.Token, request.NewPassword), cancellationToken);
        return NoContent();
    }

    private void WriteSession(AuthResultDto result)
        => SessionCookie.Write(HttpContext, result.Token, result.AbsoluteExpiresAt);
}
