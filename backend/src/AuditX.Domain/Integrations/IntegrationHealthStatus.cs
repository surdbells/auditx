using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Integrations;

/// <summary>
/// Rolling health for an integration (US-M14-012). Recent failures move the state Healthy → Degraded →
/// Failing; a success resets it. The transition to Failing is what triggers an operational alert.
/// </summary>
public sealed class IntegrationHealthStatus : Entity
{
    public const int DegradedThreshold = 3;
    public const int FailingThreshold = 10;

    private IntegrationHealthStatus()
    {
    }

    public Guid IntegrationId { get; private set; }

    public IntegrationHealthState State { get; private set; } = IntegrationHealthState.Healthy;

    public DateTimeOffset? LastSuccessAt { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    public int RecentFailureCount { get; private set; }

    public static IntegrationHealthStatus Create(Guid integrationId) => new() { IntegrationId = integrationId };

    public void RecordSuccess(DateTimeOffset atUtc)
    {
        LastSuccessAt = atUtc;
        RecentFailureCount = 0;
        State = IntegrationHealthState.Healthy;
    }

    /// <summary>Record a failure; returns true if the state just transitioned to Failing (alert trigger).</summary>
    public bool RecordFailure(DateTimeOffset atUtc)
    {
        var wasFailing = State == IntegrationHealthState.Failing;
        LastFailureAt = atUtc;
        RecentFailureCount++;
        State = RecentFailureCount >= FailingThreshold
            ? IntegrationHealthState.Failing
            : RecentFailureCount >= DegradedThreshold
                ? IntegrationHealthState.Degraded
                : IntegrationHealthState.Healthy;
        return State == IntegrationHealthState.Failing && !wasFailing;
    }
}
