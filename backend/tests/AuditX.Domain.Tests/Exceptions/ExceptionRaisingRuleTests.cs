using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Domain.Tests.Exceptions;

public sealed class ExceptionRaisingRuleTests
{
    [Fact]
    public void Threshold_out_of_range_is_rejected()
    {
        Assert.Throws<DomainException>(() => ExceptionRaisingRule.Create(ResponseType.Rating, allowOnNa: false, scoreThreshold: 101));
        Assert.Throws<DomainException>(() => ExceptionRaisingRule.Create(ResponseType.Rating, allowOnNa: false, scoreThreshold: -1));
    }

    [Fact]
    public void Na_eligibility_only_when_allowed()
    {
        var allow = ExceptionRaisingRule.Create(ResponseType.PassFailNa, allowOnNa: true, scoreThreshold: null);
        Assert.True(allow.IsEligible(ResponseVerdict.Na, score: null));

        var deny = ExceptionRaisingRule.Create(ResponseType.PassFailNa, allowOnNa: false, scoreThreshold: null);
        Assert.False(deny.IsEligible(ResponseVerdict.Na, score: null));
    }

    [Fact]
    public void Score_eligibility_is_at_or_below_the_threshold()
    {
        var rule = ExceptionRaisingRule.Create(ResponseType.Rating, allowOnNa: false, scoreThreshold: 50);
        Assert.True(rule.IsEligible(verdict: null, score: 50));
        Assert.True(rule.IsEligible(verdict: null, score: 25));
        Assert.False(rule.IsEligible(verdict: null, score: 75));
        Assert.False(rule.IsEligible(verdict: null, score: null));
    }

    [Fact]
    public void Inactive_rule_is_never_eligible()
    {
        var rule = ExceptionRaisingRule.Create(ResponseType.Rating, allowOnNa: true, scoreThreshold: 50);
        rule.Update(allowOnNa: true, scoreThreshold: 50, isActive: false);
        Assert.False(rule.IsEligible(ResponseVerdict.Na, score: 10));
    }

    [Fact]
    public void Score_gate_is_verdict_agnostic_but_a_pass_above_threshold_stays_ineligible()
    {
        // The score gate exists so a poor score warrants a finding regardless of verdict — value-type items
        // (Rating) may carry no verdict at all. A Pass scoring at/below the threshold is therefore still
        // eligible via the score path; a Pass scoring above it is not (and has no N/A allowance to fall back on).
        var rule = ExceptionRaisingRule.Create(ResponseType.Rating, allowOnNa: false, scoreThreshold: 50);
        Assert.True(rule.IsEligible(ResponseVerdict.Pass, score: 25));
        Assert.False(rule.IsEligible(ResponseVerdict.Pass, score: 75));
    }

    [Fact]
    public void Na_allowance_does_not_leak_to_other_verdicts()
    {
        var rule = ExceptionRaisingRule.Create(ResponseType.PassFailNa, allowOnNa: true, scoreThreshold: null);
        Assert.False(rule.IsEligible(ResponseVerdict.Pass, score: null));
    }
}
