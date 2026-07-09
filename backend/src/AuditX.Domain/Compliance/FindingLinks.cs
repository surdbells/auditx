using AuditX.Domain.Common;

namespace AuditX.Domain.Compliance;

/// <summary>
/// Links a finding (audit exception) to an internal control it relates to (P1-B). A simple join row — create to
/// link, delete to unlink; the audit trail records both. <c>CreatedAt/By</c> capture who linked it and when.
/// </summary>
public sealed class ExceptionControlLink : Entity
{
    private ExceptionControlLink()
    {
    }

    public Guid ExceptionId { get; private set; }

    public Guid ControlId { get; private set; }

    public Guid LinkedByUserId { get; private set; }

    public static ExceptionControlLink Create(Guid exceptionId, Guid controlId, Guid linkedByUserId)
    {
        Guard.Against(exceptionId == Guid.Empty, "finding_link.exception_required", "An exception is required.");
        Guard.Against(controlId == Guid.Empty, "finding_link.control_required", "A control is required.");
        return new ExceptionControlLink { ExceptionId = exceptionId, ControlId = controlId, LinkedByUserId = linkedByUserId };
    }
}

/// <summary>
/// Links a finding (audit exception) to a regulation it breaches / relates to (P1-B). Powers the
/// compliance-by-regulation report. Create to link, delete to unlink.
/// </summary>
public sealed class ExceptionRegulationLink : Entity
{
    private ExceptionRegulationLink()
    {
    }

    public Guid ExceptionId { get; private set; }

    public Guid RegulationId { get; private set; }

    public Guid LinkedByUserId { get; private set; }

    public static ExceptionRegulationLink Create(Guid exceptionId, Guid regulationId, Guid linkedByUserId)
    {
        Guard.Against(exceptionId == Guid.Empty, "finding_link.exception_required", "An exception is required.");
        Guard.Against(regulationId == Guid.Empty, "finding_link.regulation_required", "A regulation is required.");
        return new ExceptionRegulationLink { ExceptionId = exceptionId, RegulationId = regulationId, LinkedByUserId = linkedByUserId };
    }
}
