using AuditX.Domain.Sanctions;

namespace AuditX.Application.Abstractions.Sanctions;

/// <summary>An evidence reference embedded in a dossier — its filename and SHA-256 hash (hashes only, never bytes — US-M7-007).</summary>
public sealed record DossierEvidenceReference(string Filename, string Sha256Hash);

/// <summary>The full context the generator needs to compose a dossier, assembled in the application layer.</summary>
public sealed record DossierContext(
    SanctionsCase Case,
    string? ExceptionTitle,
    string? ExceptionSeverity,
    string? ExceptionCategory,
    IReadOnlyList<DossierEvidenceReference> EvidenceReferences);

/// <summary>The produced dossier artefact: its bytes, MIME type, suggested filename, and the artefact's own SHA-256.</summary>
public sealed record DossierArtefact(byte[] Content, string ContentType, string Filename, string Sha256Hash);

/// <summary>
/// Produces a sanctions dossier artefact (M7). The v1 implementation renders structured, browser-printable HTML
/// (no PDF library — license/audit risk). A vetted PDF renderer is a later swap behind this port.
/// </summary>
public interface IDossierGenerator
{
    DossierArtefact Generate(DossierContext context);
}
