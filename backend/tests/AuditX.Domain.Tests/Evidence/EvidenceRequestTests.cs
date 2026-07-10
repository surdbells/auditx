using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;

namespace AuditX.Domain.Tests.Evidence;

public sealed class EvidenceRequestTests
{
    private static readonly Guid AuditId = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateOnly Requested = new(2027, 1, 10);
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static EvidenceRequest New(DateOnly? due = null)
        => EvidenceRequest.Request(AuditId, null, "Signed dual-authorisation matrix", "policy_procedure", User, Requested, due, null);

    [Fact]
    public void Request_starts_outstanding()
    {
        var r = New();
        Assert.Equal(EvidenceRequestStatus.Requested, r.Status);
        Assert.Equal("policy_procedure", r.DocumentType);
        Assert.Null(r.ReceivedAt);
    }

    [Fact]
    public void Mark_received_only_from_requested()
    {
        var r = New();
        r.MarkReceived(User, Now);
        Assert.Equal(EvidenceRequestStatus.Received, r.Status);
        Assert.Equal(User, r.ReceivedByUserId);

        // Cannot receive again (or waive) once actioned.
        Assert.Throws<InvalidStateTransitionException>(() => r.MarkReceived(User, Now));
        Assert.Throws<InvalidStateTransitionException>(() => r.Waive("no longer needed"));
    }

    [Fact]
    public void Waive_requires_a_reason()
    {
        var r = New();
        Assert.Throws<DomainException>(() => r.Waive("   "));
        r.Waive("Superseded by an automated control; evidence no longer applicable.");
        Assert.Equal(EvidenceRequestStatus.Waived, r.Status);
        Assert.NotNull(r.WaiveReason);

        // A waived request was never received — the received fields must stay null.
        Assert.Null(r.ReceivedByUserId);
        Assert.Null(r.ReceivedAt);
    }

    [Fact]
    public void Overdue_only_while_outstanding_and_past_due()
    {
        var today = new DateOnly(2027, 2, 1);
        Assert.False(New().IsOverdue(today));                              // no due date
        Assert.True(New(new DateOnly(2027, 1, 20)).IsOverdue(today));      // past due, still outstanding
        Assert.False(New(new DateOnly(2027, 3, 1)).IsOverdue(today));      // due in the future

        var received = New(new DateOnly(2027, 1, 20));
        received.MarkReceived(User, Now);
        Assert.False(received.IsOverdue(today));                          // received → never overdue
    }
}
