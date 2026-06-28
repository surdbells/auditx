using AuditX.Domain.Ac;
using AuditX.Domain.Ac.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Ac;

public sealed class AcPackTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly P1 = new(2026, 10, 1);
    private static readonly DateOnly P2 = new(2026, 12, 31);

    private static AcPack Started() => AcPack.Start(1, P1, P2, "Q4 2026", ["html", "docx"], Guid.NewGuid(), Now);

    private static AcPack Completed()
    {
        var p = Started();
        p.MarkRunning();
        p.Complete("{\"versionNumber\":1}", "abc123", "ac-packs/x/html.html", "[{\"format\":\"html\"}]", 1, Now);
        return p;
    }

    [Fact]
    public void Start_is_generated_and_raises_the_requested_event()
    {
        var p = Started();
        Assert.Equal(AcPackStatus.Generated, p.Status);
        Assert.Contains(p.DomainEvents, e => e is AcPackGenerationRequestedEvent);
    }

    [Fact]
    public void Period_end_before_start_is_rejected()
        => Assert.Throws<DomainException>(() => AcPack.Start(1, P2, P1, null, ["html"], Guid.NewGuid(), Now));

    [Fact]
    public void Complete_moves_to_pending_review_and_raises_generated()
    {
        var p = Completed();
        Assert.Equal(AcPackStatus.PendingReview, p.Status);
        Assert.Contains(p.DomainEvents, e => e is AcPackGeneratedEvent);
    }

    [Fact]
    public void Supplementary_text_only_editable_while_pending_review()
    {
        var p = Completed();
        p.AddSupplementaryText("Executive narrative for the committee");
        Assert.Equal("Executive narrative for the committee", p.CiaSupplementaryText);

        p.Approve(Guid.NewGuid(), Now);
        Assert.Throws<InvalidStateTransitionException>(() => p.AddSupplementaryText("too late"));
    }

    [Fact]
    public void Reseal_only_while_pending_review_then_locks()
    {
        var p = Completed();
        p.ResealArtefacts("newhash", "ac-packs/x/approved/html.html", "[{\"format\":\"html\"}]", 1);
        Assert.Equal("newhash", p.Sha256Hash);

        p.Approve(Guid.NewGuid(), Now);
        Assert.Throws<InvalidStateTransitionException>(() => p.ResealArtefacts("x", "y", "[{}]", 1));
    }

    [Fact]
    public void Distribution_is_per_recipient_and_first_call_moves_to_distributed()
    {
        var p = Completed();
        p.Approve(Guid.NewGuid(), Now);

        p.RecordDistribution(Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Equal(AcPackStatus.Distributed, p.Status);
        p.RecordDistribution(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal(2, p.Distributions.Count);
        Assert.Equal(2, p.DomainEvents.Count(e => e is AcPackDistributedEvent));
    }

    [Fact]
    public void Cannot_distribute_before_approval()
    {
        var p = Completed(); // PendingReview
        Assert.Throws<InvalidStateTransitionException>(() => p.RecordDistribution(Guid.NewGuid(), Guid.NewGuid(), Now));
    }
}

public sealed class AcActionItemTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_is_open_and_raises_created()
    {
        var item = AcActionItem.Create("Tighten branch cash controls", null, null, null, Guid.NewGuid());
        Assert.Equal(AcActionItemStatus.Open, item.Status);
        Assert.Contains(item.DomainEvents, e => e is AcActionItemCreatedEvent);
    }

    [Fact]
    public void Close_requires_a_non_blank_response()
    {
        var item = AcActionItem.Create("X", null, null, null, Guid.NewGuid());
        Assert.Throws<DomainException>(() => item.Close("   ", Guid.NewGuid(), Now));
    }

    [Fact]
    public void Close_then_acknowledge_is_terminal()
    {
        var item = AcActionItem.Create("X", null, null, null, Guid.NewGuid());
        item.Close("Remediated and verified", Guid.NewGuid(), Now);
        Assert.Equal(AcActionItemStatus.Closed, item.Status);

        item.AcknowledgeClosure(Guid.NewGuid(), Now);
        Assert.Equal(AcActionItemStatus.AcknowledgedClosed, item.Status);
        Assert.Contains(item.DomainEvents, e => e is AcActionItemClosureAcknowledgedEvent);

        Assert.Throws<InvalidStateTransitionException>(() => item.AcknowledgeClosure(Guid.NewGuid(), Now));
    }
}
