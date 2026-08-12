using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Controls;

/// <summary>
/// An append-only record of one test of a control's effectiveness (P1-B). Tests are normally driven by an audit
/// checklist item that tests the control (<see cref="AuditId"/> + <see cref="ChecklistItemId"/> provide the
/// evidence trail), but a control may also be tested ad-hoc outside an audit (both nullable). The latest test's
/// <see cref="Result"/> is reflected on the control via <see cref="Control.RecordTest"/>.
/// </summary>
public sealed class ControlTest : Entity
{
    private ControlTest()
    {
    }

    public Guid ControlId { get; private set; }

    public Guid? AuditId { get; private set; }

    public Guid? ChecklistItemId { get; private set; }

    public ControlEffectiveness Result { get; private set; }

    public Guid TestedByUserId { get; private set; }

    public DateTimeOffset TestedAt { get; private set; }

    public string? Notes { get; private set; }

    public static ControlTest Create(
        Guid controlId, Guid? auditId, Guid? checklistItemId, ControlEffectiveness result, Guid testedByUserId, DateTimeOffset testedAt, string? notes)
    {
        Guard.Against(controlId == Guid.Empty, "control_test.control_required", "A control is required.");
        Guard.Against(result == ControlEffectiveness.NotTested, "control_test.result_required", "A control test must record a concrete effectiveness outcome.");
        return new ControlTest
        {
            ControlId = controlId,
            AuditId = auditId,
            ChecklistItemId = checklistItemId,
            Result = result,
            TestedByUserId = testedByUserId,
            TestedAt = testedAt,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
    }
}
