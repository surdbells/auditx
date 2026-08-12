using AuditX.Domain.Common;

namespace AuditX.Domain.Controls;

/// <summary>
/// Links an internal control to a risk it mitigates (P1-B) — a many-to-many join between the control register
/// and the risk register. A simple join row: create to link, delete to unlink; the audit trail records both.
/// Neither side owns the other (both are independent registers), so the link restricts deletion on both FKs.
/// </summary>
public sealed class ControlRiskLink : Entity
{
    private ControlRiskLink()
    {
    }

    public Guid ControlId { get; private set; }

    public Guid RiskId { get; private set; }

    public Guid LinkedByUserId { get; private set; }

    public static ControlRiskLink Create(Guid controlId, Guid riskId, Guid linkedByUserId)
    {
        Guard.Against(controlId == Guid.Empty, "control_risk_link.control_required", "A control is required.");
        Guard.Against(riskId == Guid.Empty, "control_risk_link.risk_required", "A risk is required.");
        return new ControlRiskLink { ControlId = controlId, RiskId = riskId, LinkedByUserId = linkedByUserId };
    }
}
