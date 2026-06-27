using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using AuditX.Domain.Evidence.Events;

namespace AuditX.Domain.Tests.Evidence;

public sealed class EvidenceIntegrityTests
{
    private static EvidenceFile NewFile() => EvidenceFile.Create(
        Guid.NewGuid(), EvidenceContextType.Response, Guid.NewGuid(), "audits/x/y", "f.pdf", "application/pdf",
        sizeBytes: 10, sha256Hash: "abc", uploadedBy: Guid.NewGuid(), uploadedAt: DateTimeOffset.UnixEpoch);

    [Fact]
    public void ClearFlag_on_a_flagged_file_clears_it_and_raises_event()
    {
        var file = NewFile();
        file.Flag();
        Assert.True(file.IsFlagged);

        file.ClearFlag("Investigated: false positive, re-hashed clean.");

        Assert.False(file.IsFlagged);
        Assert.Contains(file.DomainEvents, e => e is EvidenceUnflaggedEvent);
    }

    [Fact]
    public void ClearFlag_on_an_unflagged_file_throws()
    {
        var file = NewFile();
        var ex = Assert.Throws<DomainException>(() => file.ClearFlag("n/a"));
        Assert.Equal("evidence.not_flagged", ex.Code);
    }

    [Fact]
    public void ClearFlag_requires_a_resolution()
    {
        var file = NewFile();
        file.Flag();
        var ex = Assert.Throws<DomainException>(() => file.ClearFlag("   "));
        Assert.Equal("evidence.resolution_required", ex.Code);
    }
}
