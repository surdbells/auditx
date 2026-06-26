namespace AuditX.Domain.Enums;

/// <summary>Outcome of installing a signed offline release package (US-M15-032).</summary>
public enum ReleaseInstallStatus
{
    Verified,
    Installed,
    Rejected,
}

/// <summary>Outcome of a backup restore drill (US-M15-026).</summary>
public enum RestoreOutcome
{
    Success,
    Failed,
}

/// <summary>Lifecycle of an approval-gated per-object restore request (US-M15-025).</summary>
public enum ObjectRestoreStatus
{
    Requested,
    Approved,
    Rejected,
    Executed,
}
