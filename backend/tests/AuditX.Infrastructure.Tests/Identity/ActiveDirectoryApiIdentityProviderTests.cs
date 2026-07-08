using System.Net;
using System.Text;
using AuditX.Infrastructure.Identity;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Tests.Identity;

/// <summary>
/// Unit tests for the generic AD-over-REST identity provider. A stub <see cref="HttpMessageHandler"/> plays the
/// bank's gateway so the provider is exercised end-to-end against the documented contract without any network.
/// </summary>
public sealed class ActiveDirectoryApiIdentityProviderTests
{
    private const string UserJson =
        """
        {
          "samAccountName": "jdoe",
          "userPrincipalName": "jdoe@bank.local",
          "objectSid": "S-1-5-21-1-2-3-1001",
          "email": "jdoe@bank.com",
          "firstName": "John",
          "lastName": "Doe",
          "displayName": "John Doe",
          "enabled": true,
          "distinguishedName": "CN=John Doe,OU=Audit,DC=bank,DC=local",
          "groups": ["CN=Auditors,OU=Groups,DC=bank,DC=local"]
        }
        """;

    private static ActiveDirectoryApiIdentityProvider Build(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = new HttpClient(new StubHandler(responder)) { BaseAddress = null };
        var options = Microsoft.Extensions.Options.Options.Create(new ActiveDirectoryApiOptions { BaseUrl = "https://ad.bank.internal/api" });
        return new ActiveDirectoryApiIdentityProvider(client, options, NullLogger<ActiveDirectoryApiIdentityProvider>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Authenticate_maps_gateway_user_on_success()
    {
        var provider = Build(_ => Json(HttpStatusCode.OK, UserJson));

        var user = await provider.AuthenticateAsync("jdoe", "correct-horse");

        Assert.NotNull(user);
        Assert.Equal("jdoe", user!.SamAccountName);
        Assert.Equal("jdoe@bank.local", user.UserPrincipalName);
        Assert.Equal("S-1-5-21-1-2-3-1001", user.ObjectSid);
        Assert.Equal("John Doe", user.DisplayName);
        Assert.True(user.IsEnabled);
    }

    [Fact]
    public async Task Authenticate_posts_to_the_configured_url()
    {
        Uri? seen = null;
        var provider = Build(req => { seen = req.RequestUri; return Json(HttpStatusCode.OK, UserJson); });

        await provider.AuthenticateAsync("jdoe", "pw");

        Assert.Equal("https://ad.bank.internal/api/authenticate", seen!.ToString());
    }

    [Fact]
    public async Task Authenticate_returns_null_on_rejected_credentials()
    {
        var provider = Build(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        Assert.Null(await provider.AuthenticateAsync("jdoe", "wrong"));
    }

    [Fact]
    public async Task Authenticate_fails_closed_when_gateway_unreachable()
    {
        var provider = Build(_ => throw new HttpRequestException("no route to host"));

        Assert.Null(await provider.AuthenticateAsync("jdoe", "pw"));
    }

    [Fact]
    public async Task FindByAccountName_returns_null_on_404()
    {
        var provider = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await provider.FindByAccountNameAsync("ghost"));
    }

    [Fact]
    public async Task IsAccountEnabled_reflects_gateway_flag()
    {
        var disabled = UserJson.Replace("\"enabled\": true", "\"enabled\": false");
        var provider = Build(_ => Json(HttpStatusCode.OK, disabled));

        Assert.False(await provider.IsAccountEnabledAsync("S-1-5-21-1-2-3-1001"));
    }

    [Fact]
    public async Task Provision_filter_allows_when_group_matches()
    {
        var provider = Build(_ => Json(HttpStatusCode.OK, UserJson));
        var user = (await provider.AuthenticateAsync("jdoe", "pw"))!;

        Assert.True(await provider.IsPermittedToProvisionAsync(user, filterOuDn: null, filterGroupSid: "CN=Auditors,OU=Groups,DC=bank,DC=local"));
    }

    [Fact]
    public async Task Provision_filter_denies_when_group_absent()
    {
        var provider = Build(_ => Json(HttpStatusCode.OK, UserJson));
        var user = (await provider.AuthenticateAsync("jdoe", "pw"))!;

        Assert.False(await provider.IsPermittedToProvisionAsync(user, filterOuDn: null, filterGroupSid: "CN=Nope,DC=bank,DC=local"));
    }

    [Fact]
    public async Task Provision_filter_allows_when_no_filter_configured()
    {
        var provider = Build(_ => Json(HttpStatusCode.OK, UserJson));
        var user = (await provider.AuthenticateAsync("jdoe", "pw"))!;

        Assert.True(await provider.IsPermittedToProvisionAsync(user, filterOuDn: null, filterGroupSid: null));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
