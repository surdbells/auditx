using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;

namespace AuditX.Application.Audits;

internal static class AuditConcurrency
{
    /// <summary>
    /// Optimistic-concurrency guard mirroring the M3 universe pattern: the caller echoes the rowversion
    /// it last read; a mismatch means someone else changed the audit (root scalar OR any child row — the
    /// persistence layer advances the root token on child mutations) so we reject with 409.
    /// </summary>
    public static void EnsureVersion(this Audit audit, string expectedVersion)
    {
        if (!string.Equals(Convert.ToBase64String(audit.Version ?? []), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("audit.concurrency_conflict", "The audit was modified by someone else; reload and retry.");
        }
    }
}
