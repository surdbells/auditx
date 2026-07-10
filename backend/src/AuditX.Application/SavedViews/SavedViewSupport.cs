using AuditX.Application.Common.Exceptions;
using AuditX.Domain.SavedViews;

namespace AuditX.Application.SavedViews;

internal static class SavedViewConcurrency
{
    public static void EnsureVersion(this SavedView view, string expectedVersion)
    {
        if (!string.Equals(RowVersionToken.Encode(view.Version), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("saved_view.concurrency_conflict", "The saved view was modified elsewhere; reload and retry.");
        }
    }
}

internal static class SavedViewAccess
{
    /// <summary>Only the owner may edit, share or delete a saved view.</summary>
    public static void EnsureOwner(SavedView view, Guid? userId)
    {
        if (userId is not { } uid || view.OwnerUserId != uid)
        {
            throw new ForbiddenAccessException("Only the owner can modify this saved view.");
        }
    }
}
