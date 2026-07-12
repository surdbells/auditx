using AuditX.Application.Integrations.Webhooks;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Integrations;

namespace AuditX.Application.Tests.Integrations;

public sealed class WebhookEventCatalogTests
{
    private static CreateWebhookSubscriptionCommand Command(params string[] events) =>
        new("https://ops.internal.bank/hooks/x", events, "sixteen-char-secret!", RetryPolicyJson: null);

    [Fact]
    public void Catalogue_recognises_its_own_codes_and_rejects_unknown()
    {
        Assert.True(WebhookEventCatalog.IsKnown(AuditEventTypes.ExceptionRaised));
        Assert.False(WebhookEventCatalog.IsKnown("not_a_real_event"));
        Assert.NotEmpty(WebhookEventCatalog.All);
    }

    [Fact]
    public void Validator_accepts_known_event_types()
    {
        var validator = new CreateWebhookSubscriptionCommandValidator();
        var result = validator.Validate(Command(AuditEventTypes.ExceptionRaised, AuditEventTypes.ReportGenerated));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_an_unknown_event_type()
    {
        var validator = new CreateWebhookSubscriptionCommandValidator();
        var result = validator.Validate(Command(AuditEventTypes.ExceptionRaised, "totally_made_up"));
        Assert.False(result.IsValid);
    }
}
