namespace AuditX.Domain.Enums;

/// <summary>Lifecycle of a requested piece of evidence (P2-D): outstanding until received or formally waived.</summary>
public enum EvidenceRequestStatus
{
    Requested,
    Received,
    Waived,
}
