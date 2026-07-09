namespace AuditX.Domain.Enums;

/// <summary>The kind of audit work a <c>TimeEntry</c> records, for utilisation-by-activity reporting.</summary>
public enum TimeEntryCategory
{
    Fieldwork,
    Review,
    Reporting,
    Planning,
    Administration,
}
