using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Compliance;

namespace AuditX.Application.Compliance;

internal static class RegulationConcurrency
{
    public static void EnsureVersion(this Regulation regulation, string expectedVersion)
    {
        if (!string.Equals(RowVersionToken.Encode(regulation.Version), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("regulation.concurrency_conflict", "The regulation was modified by someone else; reload and retry.");
        }
    }
}
