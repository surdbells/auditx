using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Execution;

namespace AuditX.Domain.Tests.Execution;

public sealed class AuditProcedureTests
{
    private static readonly Guid AuditId = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateOnly Today = new(2027, 3, 1);

    private static AuditProcedure Sampling(int? population, int? sample, int? tested, int? exceptions)
        => AuditProcedure.Record(AuditId, ProcedureType.Sampling, null, User, Today, "Test 25 of 300 payments", null,
            population, sample, tested, exceptions, SamplingMethod.Random);

    [Fact]
    public void Sampling_captures_the_numeric_test_fields()
    {
        var p = Sampling(300, 25, 25, 2);
        Assert.Equal(ProcedureType.Sampling, p.Type);
        Assert.Equal(300, p.Population);
        Assert.Equal(25, p.SampleSize);
        Assert.Equal(2, p.ExceptionsFound);
        Assert.Equal(SamplingMethod.Random, p.Method);
    }

    [Fact]
    public void Sampling_rejects_inconsistent_counts()
    {
        Assert.Throws<DomainException>(() => Sampling(300, 400, null, null));   // sample > population
        Assert.Throws<DomainException>(() => Sampling(300, 25, 30, null));      // tested > sample
        Assert.Throws<DomainException>(() => Sampling(300, 25, 20, 25));        // exceptions > tested
        Assert.Throws<DomainException>(() => Sampling(-1, null, null, null));   // negative
    }

    [Fact]
    public void Sampling_requires_each_count_to_have_its_upper_bound()
    {
        // A lower count without its bound would let the analytics error-rate count orphan exceptions.
        Assert.Throws<DomainException>(() => Sampling(null, 25, null, null));   // sample without population
        Assert.Throws<DomainException>(() => Sampling(300, null, 25, null));    // tested without sample
        Assert.Throws<DomainException>(() => Sampling(300, 25, null, 2));       // exceptions without tested

        // A coherent partial set (population + sample, nothing tested yet) is allowed.
        var partial = Sampling(300, 25, null, null);
        Assert.Equal(25, partial.SampleSize);
        Assert.Null(partial.ItemsTested);
    }

    [Fact]
    public void Sampling_clears_the_counterparty()
    {
        var p = AuditProcedure.Record(AuditId, ProcedureType.Sampling, null, User, Today, "Sample test",
            "should be dropped", 300, 25, 25, 1, SamplingMethod.Random);
        Assert.Null(p.Counterparty);
    }

    [Fact]
    public void Non_sampling_procedures_ignore_the_numeric_fields()
    {
        var interview = AuditProcedure.Record(AuditId, ProcedureType.Interview, null, User, Today,
            "Discussed the reconciliation control", "J. Doe, Branch Manager", 100, 10, 10, 1, SamplingMethod.Random);
        Assert.Equal(ProcedureType.Interview, interview.Type);
        Assert.Null(interview.Population);
        Assert.Null(interview.SampleSize);
        Assert.Null(interview.Method);
        Assert.Equal("J. Doe, Branch Manager", interview.Counterparty);
    }

    [Fact]
    public void Summary_is_required()
        => Assert.Throws<DomainException>(() =>
            AuditProcedure.Record(AuditId, ProcedureType.Walkthrough, null, User, Today, "   ", null, null, null, null, null, null));
}
