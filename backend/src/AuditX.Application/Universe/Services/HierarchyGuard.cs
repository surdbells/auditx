namespace AuditX.Application.Universe.Services;

/// <summary>Acyclicity checks over the universe parent graph (BR-M3-003).</summary>
public static class HierarchyGuard
{
    /// <summary>
    /// True if making <paramref name="entityId"/> a child of <paramref name="proposedParentId"/> would
    /// introduce a cycle, given the current (id → parentId) <paramref name="parentMap"/>.
    /// </summary>
    public static bool WouldCreateCycle(IReadOnlyDictionary<Guid, Guid?> parentMap, Guid entityId, Guid? proposedParentId)
    {
        if (proposedParentId is not { } parent)
        {
            return false;
        }

        if (parent == entityId)
        {
            return true;
        }

        var current = (Guid?)parent;
        var visited = new HashSet<Guid>();
        while (current is { } node)
        {
            if (node == entityId)
            {
                return true;
            }

            if (!visited.Add(node))
            {
                return true; // pre-existing cycle guard
            }

            parentMap.TryGetValue(node, out current);
        }

        return false;
    }
}
