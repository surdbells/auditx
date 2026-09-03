namespace AuditX.Domain.Enums;

/// <summary>Verdict an auditor records against a checklist item (M5). A null verdict means a draft.</summary>
public enum ResponseVerdict
{
    Pass,
    Fail,
    Na,
}

/// <summary>
/// What an uploaded evidence file is attached to. M5 only writes <see cref="Response"/>; the others are
/// stable seams so M6 (MAP actions) and M9 (sanctions dossiers) can reuse the same store without a schema break.
/// </summary>
public enum EvidenceContextType
{
    Response,
    MapAction,
    SanctionsDossier,

    /// <summary>A file the auditee uploaded to satisfy an <see cref="Evidence.EvidenceRequest"/>.</summary>
    EvidenceRequest,
}

/// <summary>
/// Why a piece of evidence is being requested — a general document the auditor needs for review, or evidence
/// that backs a specific finding. Lets the auditee's document-request list and reporting distinguish the two.
/// </summary>
public enum EvidenceRequestPurpose
{
    /// <summary>An audit document required by the auditor for review.</summary>
    ReviewDocument,

    /// <summary>A document that backs up a finding.</summary>
    FindingEvidence,
}
