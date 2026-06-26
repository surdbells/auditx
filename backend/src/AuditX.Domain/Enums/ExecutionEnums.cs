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
}
