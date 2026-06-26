namespace AuditX.Domain.Enums;

/// <summary>Status of a maker-checker (dual-control) pending action.</summary>
public enum MakerCheckerStatus
{
    Pending,
    Approved,
    Rejected,
    Expired,
}
