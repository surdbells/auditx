using AuditX.Domain.AuditTrail;

namespace AuditX.Domain.Integrations;

/// <summary>A subscribable webhook event: its stable <paramref name="Code"/> and human <paramref name="Label"/>.</summary>
public sealed record WebhookEventDescriptor(string Code, string Label);

/// <summary>
/// The curated catalogue of business events an external system may subscribe to via a webhook. These are
/// the meaningful lifecycle events (not the full internal audit-trail vocabulary); the subscription editor
/// renders this list as a dropdown instead of accepting free text, and only known codes are accepted.
/// </summary>
public static class WebhookEventCatalog
{
    public static readonly IReadOnlyList<WebhookEventDescriptor> All =
    [
        new(AuditEventTypes.AuditCreated, "Audit created"),
        new(AuditEventTypes.AuditCompleted, "Audit completed"),
        new(AuditEventTypes.AuditTransitioned, "Audit status changed"),
        new(AuditEventTypes.ExceptionRaised, "Exception raised"),
        new(AuditEventTypes.ExceptionClosed, "Exception closed"),
        new(AuditEventTypes.ExceptionCancelled, "Exception cancelled"),
        new(AuditEventTypes.MapSubmitted, "Action plan submitted"),
        new(AuditEventTypes.MapApproved, "Action plan approved"),
        new(AuditEventTypes.MapRejected, "Action plan rejected"),
        new(AuditEventTypes.ReportGenerated, "Report generated"),
        new(AuditEventTypes.ReportDistributed, "Report distributed"),
        new(AuditEventTypes.PlanSubmitted, "Annual plan submitted"),
        new(AuditEventTypes.PlanDecisionRecorded, "Annual plan decision recorded"),
        new(AuditEventTypes.SanctionsTriggered, "Sanctions process triggered"),
        new(AuditEventTypes.ControlStatusChanged, "Control status changed"),
        new(AuditEventTypes.RegulationStatusChanged, "Regulation status changed"),
        new(AuditEventTypes.UserProvisioned, "User provisioned"),
        new(AuditEventTypes.UserDeactivated, "User deactivated"),
    ];

    private static readonly HashSet<string> Codes = All.Select(e => e.Code).ToHashSet(StringComparer.Ordinal);

    /// <summary>True when the code is a recognised, subscribable webhook event.</summary>
    public static bool IsKnown(string code) => Codes.Contains(code);
}
