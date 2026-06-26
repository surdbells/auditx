namespace AuditX.Application.Identity.Services;

/// <summary>Acyclicity checks over the role-inheritance graph (BR-M1-009).</summary>
public static class RoleHierarchy
{
    /// <summary>
    /// Returns true if making <paramref name="roleId"/> inherit from <paramref name="proposedParents"/>
    /// would introduce a cycle, given the current inheritance <paramref name="graph"/>
    /// (roleId → its parent role ids).
    /// </summary>
    public static bool WouldCreateCycle(
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> graph,
        Guid roleId,
        IReadOnlyCollection<Guid> proposedParents)
    {
        bool Reaches(Guid start)
        {
            var stack = new Stack<Guid>();
            var seen = new HashSet<Guid>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current == roleId)
                {
                    return true;
                }

                if (!seen.Add(current))
                {
                    continue;
                }

                if (graph.TryGetValue(current, out var parents))
                {
                    foreach (var parent in parents)
                    {
                        stack.Push(parent);
                    }
                }
            }

            return false;
        }

        return proposedParents.Any(parent => parent == roleId || Reaches(parent));
    }
}
