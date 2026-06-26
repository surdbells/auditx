using AuditX.Application.Identity.Services;

namespace AuditX.Application.Tests.Identity;

public sealed class RoleHierarchyTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void Direct_self_reference_is_a_cycle()
        => Assert.True(RoleHierarchy.WouldCreateCycle(Graph(), A, [A]));

    [Fact]
    public void Simple_parent_is_not_a_cycle()
        => Assert.False(RoleHierarchy.WouldCreateCycle(Graph(), A, [B]));

    [Fact]
    public void Indirect_cycle_is_detected()
    {
        var graph = Graph((B, [C]), (C, [A]));
        Assert.True(RoleHierarchy.WouldCreateCycle(graph, A, [B]));
    }

    [Fact]
    public void Diamond_without_cycle_is_allowed()
    {
        var d = Guid.NewGuid();
        var graph = Graph((B, [d]), (C, [d]));
        Assert.False(RoleHierarchy.WouldCreateCycle(graph, A, [B, C]));
    }

    private static Dictionary<Guid, IReadOnlyList<Guid>> Graph(params (Guid Role, Guid[] Parents)[] edges)
        => edges.ToDictionary(e => e.Role, e => (IReadOnlyList<Guid>)e.Parents);
}
