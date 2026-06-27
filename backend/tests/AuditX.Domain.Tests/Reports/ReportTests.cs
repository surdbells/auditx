using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using AuditX.Domain.Reports.Events;

namespace AuditX.Domain.Tests.Reports;

public sealed class ReportTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static Report NewReport() => Report.Start(
        Guid.NewGuid(), versionNumber: 1, Guid.NewGuid(), templateVersion: 1, "{\"sections\":[]}",
        ["html"], Guid.NewGuid(), Now);

    private static readonly IReadOnlyList<ProducedArtefact> Artefacts =
        [new ProducedArtefact("html", "reports/x/v1/html/a.html", "text/html", 123, "abc123")];

    [Fact]
    public void Start_begins_pending_and_raises_requested_event()
    {
        var r = NewReport();
        Assert.Equal(ReportStatus.Pending, r.Status);
        Assert.Contains(r.DomainEvents, e => e is ReportGenerationRequestedEvent);
    }

    [Fact]
    public void Complete_after_running_marks_completed_and_raises_generated_event()
    {
        var r = NewReport();
        r.MarkRunning();
        r.Complete("hash-1", Artefacts, "[{}]", Now);

        Assert.Equal(ReportStatus.Completed, r.Status);
        Assert.Equal("hash-1", r.Sha256Hash);
        Assert.Contains(r.DomainEvents, e => e is ReportGeneratedEvent);
    }

    [Fact]
    public void Complete_directly_from_pending_is_an_illegal_transition()
    {
        var r = NewReport(); // still pending (no MarkRunning)
        Assert.ThrowsAny<DomainException>(() => r.Complete("h", Artefacts, "[{}]", Now));
    }

    [Fact]
    public void Complete_requires_at_least_one_artefact()
    {
        var r = NewReport();
        r.MarkRunning();
        var ex = Assert.Throws<DomainException>(() => r.Complete("h", [], "[]", Now));
        Assert.Equal("report.no_artefacts", ex.Code);
    }

    [Fact]
    public void Fail_marks_failed_and_raises_event()
    {
        var r = NewReport();
        r.MarkRunning();
        r.Fail("renderer blew up");
        Assert.Equal(ReportStatus.Failed, r.Status);
        Assert.Contains(r.DomainEvents, e => e is ReportGenerationFailedEvent);
    }

    [Fact]
    public void A_non_completed_report_cannot_be_distributed()
    {
        var r = NewReport(); // pending
        Assert.ThrowsAny<DomainException>(() => r.RecordDistribution(
            Guid.NewGuid(), null, "a@b.com", Guid.NewGuid(), Now, "Audit", 10, 2, "1 high, 1 low"));
    }

    [Fact]
    public void RecordDistribution_on_a_completed_report_adds_a_row_and_raises_event()
    {
        var r = NewReport();
        r.MarkRunning();
        r.Complete("h", Artefacts, "[{}]", Now);

        r.RecordDistribution(Guid.NewGuid(), null, "a@b.com", Guid.NewGuid(), Now, "Audit", 10, 2, "summary");

        Assert.Single(r.Distributions);
        Assert.Contains(r.DomainEvents, e => e is ReportDistributedEvent);
    }

    [Fact]
    public void Distribution_requires_exactly_one_of_user_or_email()
    {
        var r = NewReport();
        r.MarkRunning();
        r.Complete("h", Artefacts, "[{}]", Now);

        // neither user nor ad-hoc email set
        Assert.ThrowsAny<DomainException>(() => r.RecordDistribution(
            null, null, "resolved@b.com", Guid.NewGuid(), Now, "Audit", 1, 0, "none"));
    }
}

public sealed class ReportTemplateTests
{
    [Fact]
    public void Activation_requires_a_reason_of_at_least_twenty_chars()
    {
        var t = ReportTemplate.CreateVersion("Default", "{\"sections\":[]}", 2, Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        Assert.ThrowsAny<DomainException>(() => t.Activate("too short", Guid.NewGuid(), DateTimeOffset.UnixEpoch));
        t.Activate("Adopting the FY27 audit report template", Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        Assert.True(t.IsActive);
    }
}
