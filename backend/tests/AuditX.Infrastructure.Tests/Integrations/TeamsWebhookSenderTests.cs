using System.Net;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Infrastructure.Notifications;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuditX.Infrastructure.Tests.Integrations;

public sealed class TeamsWebhookSenderTests
{
    private const string Url = "https://outlook.office.com/webhook/abc";

    /// <summary>Captures the outgoing request and returns a canned response, so the payload + status mapping are testable.</summary>
    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("1") };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private static TeamsWebhookSender Sender(HttpMessageHandler handler, string? url) => new(
        new SingleClientFactory(handler),
        Microsoft.Extensions.Options.Options.Create(new NotificationOptions { Teams = new TeamsOptions { DefaultWebhookUrl = url } }),
        NullLogger<TeamsWebhookSender>.Instance);

    [Fact]
    public void IsConfigured_reflects_whether_a_webhook_url_is_set()
    {
        Assert.False(Sender(new CapturingHandler(HttpStatusCode.OK), url: null).IsConfigured());
        Assert.False(Sender(new CapturingHandler(HttpStatusCode.OK), url: "  ").IsConfigured());
        Assert.True(Sender(new CapturingHandler(HttpStatusCode.OK), Url).IsConfigured());
    }

    [Fact]
    public async Task Posts_a_message_card_to_the_configured_url_and_reports_sent_on_2xx()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var result = await Sender(handler, Url).SendAsync(ITeamsSender.DefaultChannelRef, "Kickoff", "Meeting at 10am");

        Assert.True(result.Success);
        Assert.Equal(Url, handler.Request!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Contains("\"MessageCard\"", handler.Body);
        Assert.Contains("Kickoff", handler.Body);
        Assert.Contains("Meeting at 10am", handler.Body);
    }

    [Fact]
    public async Task Unconfigured_send_fails_permanently_without_calling_http()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var result = await Sender(handler, url: null).SendAsync(ITeamsSender.DefaultChannelRef, "t", "b");

        Assert.False(result.Success);
        Assert.True(result.IsPermanentFailure);
        Assert.Null(handler.Request); // no HTTP attempted
    }

    [Fact]
    public async Task An_unknown_channel_reference_is_not_delivered()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var result = await Sender(handler, Url).SendAsync("teams:unknown", "t", "b");

        Assert.False(result.Success);
        Assert.True(result.IsPermanentFailure);
        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, true)]      // 400 → permanent (bad payload / URL)
    [InlineData(HttpStatusCode.NotFound, true)]        // 404 → permanent
    [InlineData(HttpStatusCode.TooManyRequests, false)] // 429 → transient (throttled)
    [InlineData(HttpStatusCode.InternalServerError, false)] // 5xx → transient
    public async Task Maps_http_status_to_retry_classification(HttpStatusCode status, bool expectedPermanent)
    {
        var result = await Sender(new CapturingHandler(status), Url).SendAsync(ITeamsSender.DefaultChannelRef, "t", "b");

        Assert.False(result.Success);
        Assert.Equal(expectedPermanent, result.IsPermanentFailure);
    }
}
