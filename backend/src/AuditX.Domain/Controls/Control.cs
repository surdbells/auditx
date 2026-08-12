using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Controls;

/// <summary>
/// An internal-control register entry (P1-B): a control that mitigates risk, with a type, operating frequency,
/// owner, optional link to an auditable entity, and a tested effectiveness rating. Powers the control-register
/// and control-effectiveness reports. The <c>Code</c> is a stable business key (immutable after creation);
/// active/retired is a lifecycle flag, soft-delete removes an erroneous entry.
/// </summary>
public sealed class Control : AggregateRoot, ISoftDeletable
{
    private Control()
    {
    }

    public string Code { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public ControlType ControlType { get; private set; }

    public ControlFrequency Frequency { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public Guid? AuditableEntityId { get; private set; }

    public ControlEffectiveness Effectiveness { get; private set; }

    public DateOnly? LastTestedDate { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static Control Register(
        string code, string title, string? description, ControlType controlType, ControlFrequency frequency,
        Guid ownerUserId, Guid? auditableEntityId)
    {
        Guard.Against(ownerUserId == Guid.Empty, "control.owner_required", "An owner is required.");
        return new Control
        {
            Code = Guard.NotNullOrWhiteSpace(code, "control.code_required", "A code is required.").Trim(),
            Title = Guard.NotNullOrWhiteSpace(title, "control.title_required", "A title is required.").Trim(),
            Description = Normalise(description),
            ControlType = controlType,
            Frequency = frequency,
            OwnerUserId = ownerUserId,
            AuditableEntityId = auditableEntityId,
            Effectiveness = ControlEffectiveness.NotTested,
            IsActive = true,
        };
    }

    /// <summary>Updates the metadata + the tested effectiveness. The <see cref="Code"/> is immutable.</summary>
    public void Update(
        string title, string? description, ControlType controlType, ControlFrequency frequency, Guid ownerUserId,
        Guid? auditableEntityId, ControlEffectiveness effectiveness, DateOnly? lastTestedDate)
    {
        Guard.Against(ownerUserId == Guid.Empty, "control.owner_required", "An owner is required.");
        if (effectiveness != ControlEffectiveness.NotTested)
        {
            Guard.Against(lastTestedDate is null, "control.tested_date_required", "A last-tested date is required once a control is tested.");
        }

        Title = Guard.NotNullOrWhiteSpace(title, "control.title_required", "A title is required.").Trim();
        Description = Normalise(description);
        ControlType = controlType;
        Frequency = frequency;
        OwnerUserId = ownerUserId;
        AuditableEntityId = auditableEntityId;
        Effectiveness = effectiveness;
        LastTestedDate = effectiveness == ControlEffectiveness.NotTested ? null : lastTestedDate;
    }

    /// <summary>
    /// Record the outcome of testing this control (typically via an audit checklist item that tests it): sets the
    /// current effectiveness and last-tested date. A test result is always a concrete outcome, never NotTested.
    /// </summary>
    public void RecordTest(ControlEffectiveness result, DateOnly testedDate)
    {
        Guard.Against(result == ControlEffectiveness.NotTested, "control.test_result_required", "A control test must record a concrete effectiveness outcome.");
        Effectiveness = result;
        LastTestedDate = testedDate;
    }

    public void SetActive(bool active) => IsActive = active;

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
