namespace AuditX.Domain.Reports;

/// <summary>
/// A rendered report artefact recorded on the <see cref="Report"/> at completion (M8): one entry per produced
/// format. Serialized into <c>ProducedArtefactsJson</c>. <see cref="FileKey"/> is the opaque blob-store key;
/// <see cref="Sha256"/> is that artefact's own hash (the canonical HTML's hash is also promoted to a column).
/// </summary>
public sealed record ProducedArtefact(string Format, string FileKey, string ContentType, long SizeBytes, string Sha256);
