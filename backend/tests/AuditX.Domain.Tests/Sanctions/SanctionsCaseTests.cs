using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Sanctions;
using AuditX.Domain.Sanctions.Events;

namespace AuditX.Domain.Tests.Sanctions;

public sealed class SanctionsCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static SanctionsCase Triggered() => SanctionsCase.Trigger(
        Guid.NewGuid(), Guid.NewGuid(), "cash_handling", ExceptionSeverity.High, isRecurrence: false, Guid.NewGuid(), Now);

    private static SanctionsCase Recommended()
    {
        var c = Triggered();
        c.RecordRecommendation("Written warning", gridVersion: 1, gridRange: "Written warning", withinRange: true, deviationReason: null, Guid.NewGuid(), Now);
        return c;
    }

    [Fact]
    public void Trigger_starts_in_recommendation_drafted_with_the_investigator_on_the_team()
    {
        var c = Triggered();
        Assert.Equal(SanctionsCaseStatus.RecommendationDrafted, c.Status);
        Assert.Single(c.TeamMembers);
        Assert.Contains(c.DomainEvents, e => e is SanctionsTriggeredEvent);
    }

    [Fact]
    public void Out_of_range_recommendation_requires_a_deviation_reason()
    {
        var c = Triggered();
        var ex = Assert.Throws<DomainException>(() => c.RecordRecommendation(
            "Dismissal", gridVersion: 1, gridRange: "Written warning", withinRange: false, deviationReason: null, Guid.NewGuid(), Now));
        Assert.Equal("sanctions.deviation_reason_required", ex.Code);
    }

    [Fact]
    public void Within_range_recommendation_clears_any_deviation_reason_and_pins_the_grid()
    {
        var c = Triggered();
        c.RecordRecommendation("Written warning", gridVersion: 3, gridRange: "Written warning", withinRange: true, deviationReason: "ignored", Guid.NewGuid(), Now);
        Assert.Null(c.DeviationReason);
        Assert.True(c.WithinGridRange);
        Assert.Equal(3, c.GridConsultedVersion);
    }

    [Fact]
    public void Recommendation_can_only_be_recorded_while_drafting()
    {
        var c = Recommended();
        c.SubmitRecommendation(Guid.NewGuid(), Now);
        Assert.ThrowsAny<DomainException>(() => c.RecordRecommendation("x", 1, "x", true, null, Guid.NewGuid(), Now));
    }

    [Fact]
    public void Happy_path_runs_recommend_submit_hr_outcome_close()
    {
        var c = Recommended();
        c.SubmitRecommendation(Guid.NewGuid(), Now);
        Assert.Equal(SanctionsCaseStatus.RecommendationSubmitted, c.Status);

        c.RecordHrOutcome(HrOutcomeType.Imposed, "{\"detail\":\"2-week suspension\"}", Guid.NewGuid(), Now);
        Assert.Equal(SanctionsCaseStatus.HrOutcomeRecorded, c.Status);
        Assert.NotNull(c.HrOutcomeAt);

        c.Close(Guid.NewGuid(), Now);
        Assert.Equal(SanctionsCaseStatus.Closed, c.Status);
    }

    [Fact]
    public void Hr_outcome_of_dc_referral_moves_to_dc_referral_then_decision()
    {
        var c = Recommended();
        c.SubmitRecommendation(Guid.NewGuid(), Now);
        c.RecordHrOutcome(HrOutcomeType.DcReferral, "{\"detail\":\"contested\"}", Guid.NewGuid(), Now);
        Assert.Equal(SanctionsCaseStatus.DcReferral, c.Status);

        c.RecordDcDecision(DcDecisionType.Uphold, "{\"detail\":\"upheld\"}", Guid.NewGuid(), Now);
        Assert.Equal(SanctionsCaseStatus.DcDecisionRecorded, c.Status);
    }

    [Fact]
    public void Direct_referral_does_not_stamp_the_hr_outcome_timestamp()
    {
        var c = Recommended();
        c.SubmitRecommendation(Guid.NewGuid(), Now);
        c.ReferToDc("This case warrants committee review for consistency.", Guid.NewGuid(), Now);
        Assert.Equal(SanctionsCaseStatus.DcReferral, c.Status);
        Assert.Null(c.HrOutcomeAt); // a referral is not an HR outcome — must not pollute the turnaround metric
    }

    [Fact]
    public void Illegal_transition_throws()
    {
        var c = Triggered(); // still drafting
        Assert.ThrowsAny<DomainException>(() => c.RecordHrOutcome(HrOutcomeType.Imposed, "{}", Guid.NewGuid(), Now));
        Assert.ThrowsAny<DomainException>(() => c.Close(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Appeal_after_a_decision_then_appeal_decision_then_close()
    {
        var c = Recommended();
        c.SubmitRecommendation(Guid.NewGuid(), Now);
        c.RecordHrOutcome(HrOutcomeType.Imposed, "{}", Guid.NewGuid(), Now);
        c.MarkAppealed();
        Assert.Equal(SanctionsCaseStatus.Appealed, c.Status);
        c.RecordAppealOutcome(AppealOutcome.Confirm, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(SanctionsCaseStatus.AppealDecisionRecorded, c.Status);
        c.Close(Guid.NewGuid(), Now);
        Assert.Equal(SanctionsCaseStatus.Closed, c.Status);
    }
}

public sealed class GridConsultationTests
{
    private const string Grid = "{\"cells\":{\"cash_handling|critical|false\":{\"recommended_range\":\"Final written warning to dismissal\"}}}";

    [Fact]
    public void Consult_returns_the_matching_cell_range()
    {
        var result = GridConsultation.Consult(Grid, "cash_handling", ExceptionSeverity.Critical, isRecurrence: false);
        Assert.Equal("Final written warning to dismissal", result.RecommendedRange);
        Assert.True(GridConsultation.WithinRange(result, "Final written warning to dismissal"));
        Assert.False(GridConsultation.WithinRange(result, "Verbal warning"));
    }

    [Fact]
    public void Consult_returns_no_range_for_an_unmapped_tuple()
    {
        var result = GridConsultation.Consult(Grid, "unknown_category", ExceptionSeverity.Low, isRecurrence: true);
        Assert.Null(result.RecommendedRange);
    }

    [Fact]
    public void ValidateShape_rejects_non_json()
        => Assert.Throws<DomainException>(() => GridConsultation.ValidateShape("not json"));
}

public sealed class SanctionsGridVersionTests
{
    [Fact]
    public void Activation_requires_a_reason_of_at_least_twenty_chars()
    {
        var grid = SanctionsGridVersion.CreateDraft(2, "{\"cells\":{}}", Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        Assert.Throws<DomainException>(() => grid.Activate("too short", Guid.NewGuid(), DateTimeOffset.UnixEpoch));
        grid.Activate("Adopting the FY27 sanctions grid policy", Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        Assert.True(grid.IsActive);
    }
}
