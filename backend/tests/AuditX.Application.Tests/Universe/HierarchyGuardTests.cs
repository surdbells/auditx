using AuditX.Application.Universe.Services;

namespace AuditX.Application.Tests.Universe;

public sealed class HierarchyGuardTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void Self_parent_is_a_cycle()
        => Assert.True(HierarchyGuard.WouldCreateCycle(Map(), A, A));

    [Fact]
    public void Null_parent_is_never_a_cycle()
        => Assert.False(HierarchyGuard.WouldCreateCycle(Map(), A, null));

    [Fact]
    public void Descendant_as_parent_is_a_cycle()
    {
        // B's parent is A; making A's parent B closes a loop.
        var map = Map((B, A), (A, null));
        Assert.True(HierarchyGuard.WouldCreateCycle(map, A, B));
    }

    [Fact]
    public void Deep_descendant_as_parent_is_a_cycle()
    {
        // C → B → A ; making A child of C is a cycle.
        var map = Map((C, B), (B, A), (A, null));
        Assert.True(HierarchyGuard.WouldCreateCycle(map, A, C));
    }

    [Fact]
    public void Unrelated_parent_is_allowed()
    {
        var map = Map((A, null), (B, null), (C, null));
        Assert.False(HierarchyGuard.WouldCreateCycle(map, A, B));
    }

    private static Dictionary<Guid, Guid?> Map(params (Guid Id, Guid? Parent)[] edges)
        => edges.ToDictionary(e => e.Id, e => e.Parent);
}
