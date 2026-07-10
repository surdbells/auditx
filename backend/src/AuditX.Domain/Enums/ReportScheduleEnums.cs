namespace AuditX.Domain.Enums;

/// <summary>How often a <see cref="AuditX.Domain.Scheduling.ReportSchedule"/> auto-generates its report.</summary>
public enum ReportCadence
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
}
