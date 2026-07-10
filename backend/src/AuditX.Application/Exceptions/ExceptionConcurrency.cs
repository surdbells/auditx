using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Exceptions;

internal static class ExceptionConcurrency
{
    /// <summary>Optimistic-concurrency guard mirroring the M4/M5 pattern; rejects a stale rowversion with 409.</summary>
    public static void EnsureVersion(this AuditException exception, string expectedVersion)
    {
        if (!string.Equals(RowVersionToken.Encode(exception.Version), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("exception.concurrency_conflict", "The exception was modified by someone else; reload and retry.");
        }
    }
}
