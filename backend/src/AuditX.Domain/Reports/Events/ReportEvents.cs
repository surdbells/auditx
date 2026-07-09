using AuditX.Domain.Common;

namespace AuditX.Domain.Reports.Events;

public abstract record ReportEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when generation is requested (status → pending). Drives the trail; M10 routes off ReportGenerated instead. <c>AuditId</c> is null for standalone reports.</summary>
public sealed record ReportGenerationRequestedEvent(Guid ReportId, Guid? AuditId, int VersionNumber, Guid GeneratedBy) : ReportEvent;

/// <summary>
/// Raised when a report finishes rendering (status → completed). M10 routes <c>report_generated</c> to the
/// requesting Audit Manager via <c>payload_derived: GeneratedBy</c>. <c>AuditId</c> is null for standalone reports.
/// </summary>
public sealed record ReportGeneratedEvent(Guid ReportId, Guid? AuditId, int VersionNumber, Guid GeneratedBy) : ReportEvent;

/// <summary>Raised when generation fails (status → failed). <c>AuditId</c> is null for standalone reports.</summary>
public sealed record ReportGenerationFailedEvent(Guid ReportId, Guid? AuditId, int VersionNumber, string FailureReason) : ReportEvent;

/// <summary>
/// Raised per recipient when a completed report is distributed. Carries the computed fields the
/// <c>report_issued</c> template renders (audit name + counts + dates) so the email body is never blank (E1).
/// <c>AuditId</c> is null for standalone reports.
/// </summary>
public sealed record ReportDistributedEvent(
    Guid ReportId,
    Guid? AuditId,
    int VersionNumber,
    Guid? RecipientUserId,
    string RecipientEmail,
    string AuditName,
    int TotalCount,
    int ExceptionCount,
    string SeveritySummary) : ReportEvent;

/// <summary>
/// Raised when a stored report artefact fails its SHA-256 verification on read (C1/C2). Payload carries
/// <c>Severity="Critical"</c> so the M10 suppression-override + SMS escalation fires for the security recipients.
/// <c>AuditId</c> is null for standalone reports.
/// </summary>
public sealed record ReportHashMismatchEvent(
    Guid ReportId,
    Guid? AuditId,
    string ExpectedHash,
    string RecomputedHash,
    Guid DetectedBy,
    string Severity = "Critical") : ReportEvent;

public sealed record ReportTemplateCreatedEvent(Guid ReportTemplateId, int VersionNumber, Guid CreatedBy) : ReportEvent;

public sealed record ReportTemplateActivatedEvent(Guid ReportTemplateId, int VersionNumber, Guid ActivatedBy) : ReportEvent;
