namespace AuditX.Application.Abstractions.Authorization;

/// <summary>
/// Answers one question cheaply, without loading the audit aggregate: is this user the AUDITEE of this audit?
/// Used by the API's independence filter, which runs ahead of every request that changes an audit.
/// </summary>
public interface IAuditIndependenceReader
{
    /// <summary>
    /// True when the user is the audit's auditee (its named auditee, or an active team member in the auditee role).
    /// Always false for a self-assessment, where the area owner is the assessor by design, and for an unknown audit.
    /// </summary>
    Task<bool> IsAuditeeAsync(Guid auditId, Guid userId, CancellationToken cancellationToken = default);
}
