using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Scheduling;

namespace AuditX.Domain.Tests.Scheduling;

public sealed class ReportScheduleTests
{
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2027, 3, 10, 8, 0, 0, TimeSpan.Zero);

    private static ReportSchedule New(ReportCadence cadence = ReportCadence.Weekly, DateTimeOffset? firstRun = null)
        => ReportSchedule.Create("Monthly executive summary", ReportKind.ExecutiveSummary, cadence, "{\"userIds\":[],\"emails\":[\"a@b.c\"]}", Creator, firstRun ?? Now);

    [Fact]
    public void Create_starts_active_and_pending_its_first_run()
    {
        var s = New(ReportCadence.Monthly, Now);
        Assert.True(s.IsActive);
        Assert.Equal(Now, s.NextRunAt);
        Assert.Null(s.LastRunAt);
        Assert.Null(s.LastReportId);
    }

    [Fact]
    public void Create_rejects_the_engagement_kind()
        => Assert.Throws<DomainException>(() =>
            ReportSchedule.Create("bad", ReportKind.AuditEngagement, ReportCadence.Daily, "{}", Creator, Now));

    [Fact]
    public void Create_requires_a_creator()
        => Assert.Throws<DomainException>(() =>
            ReportSchedule.Create("x", ReportKind.KpiPack, ReportCadence.Daily, "{}", Guid.Empty, Now));

    [Theory]
    [InlineData(ReportCadence.Daily, 1)]
    [InlineData(ReportCadence.Weekly, 7)]
    public void NextFrom_advances_day_based_cadences(ReportCadence cadence, int days)
        => Assert.Equal(Now.AddDays(days), ReportSchedule.NextFrom(Now, cadence));

    [Fact]
    public void NextFrom_advances_month_and_quarter_cadences()
    {
        Assert.Equal(Now.AddMonths(1), ReportSchedule.NextFrom(Now, ReportCadence.Monthly));
        Assert.Equal(Now.AddMonths(3), ReportSchedule.NextFrom(Now, ReportCadence.Quarterly));
    }

    [Fact]
    public void IsDue_only_when_active_undeleted_and_at_or_past_next_run()
    {
        var s = New(ReportCadence.Daily, Now);
        Assert.True(s.IsDue(Now));                       // exactly due
        Assert.True(s.IsDue(Now.AddHours(1)));           // past due
        Assert.False(s.IsDue(Now.AddSeconds(-1)));       // not yet due

        s.Update("x", ReportCadence.Daily, "{}", isActive: false);
        Assert.False(s.IsDue(Now));                      // inactive → never due

        var deleted = New(ReportCadence.Daily, Now);
        deleted.SoftDelete(Creator, Now);
        Assert.False(deleted.IsDue(Now));                // soft-deleted → never due
    }

    [Fact]
    public void RecordRun_stamps_last_run_and_advances_to_the_next_slot_from_now()
    {
        var s = New(ReportCadence.Weekly, Now);
        var reportId = Guid.NewGuid();
        var ranAt = Now.AddDays(2);                      // fired a bit after the scheduled slot

        s.RecordRun(reportId, ranAt);

        Assert.Equal(ranAt, s.LastRunAt);
        Assert.Equal(reportId, s.LastReportId);
        // Advances relative to the actual run time (so a dormant schedule catches up forward, not a backlog).
        Assert.Equal(ranAt.AddDays(7), s.NextRunAt);
        Assert.False(s.IsDue(ranAt));                    // no longer due immediately after a run
    }

    [Fact]
    public void Update_changes_the_mutable_fields()
    {
        var s = New(ReportCadence.Daily, Now);
        s.Update("Renamed", ReportCadence.Quarterly, "{\"userIds\":[],\"emails\":[]}", isActive: false);

        Assert.Equal("Renamed", s.Name);
        Assert.Equal(ReportCadence.Quarterly, s.Cadence);
        Assert.False(s.IsActive);
    }

    [Fact]
    public void SoftDelete_is_idempotent()
    {
        var s = New();
        s.SoftDelete(Creator, Now);
        s.SoftDelete(Creator, Now.AddDays(1));   // second call is a no-op
        Assert.True(s.IsDeleted);
        Assert.Equal(Now, s.DeletedAt);
    }
}
