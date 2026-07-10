using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Execution;

/// <summary>
/// A typed fieldwork procedure performed during an audit (P2-C): a sampling test, an interview or a walkthrough.
/// A standalone aggregate linked to an audit (like <c>TimeEntry</c>) — NOT a child of the Audit rowversion —
/// so procedures can be appended independently. Sampling procedures carry the numeric test fields (population /
/// sample size / items tested / exceptions found + method); interviews and walkthroughs carry only the narrative.
/// Soft-deletable; rowversion-guarded for optimistic concurrency.
/// </summary>
public sealed class AuditProcedure : Entity, ISoftDeletable
{
    private AuditProcedure()
    {
    }

    public Guid AuditId { get; private set; }

    /// <summary>Optional link to the specific checklist item this procedure supports.</summary>
    public Guid? ChecklistItemId { get; private set; }

    public ProcedureType Type { get; private set; }

    public Guid PerformedByUserId { get; private set; }

    public DateOnly PerformedOn { get; private set; }

    /// <summary>What was done (the sampling objective, the walkthrough scope, the interview topic).</summary>
    public string Summary { get; private set; } = null!;

    /// <summary>Who the auditor spoke to / the process owner (interview + walkthrough); null for sampling.</summary>
    public string? Counterparty { get; private set; }

    // ---- Sampling-only fields (null for interview / walkthrough) ----

    public int? Population { get; private set; }

    public int? SampleSize { get; private set; }

    public int? ItemsTested { get; private set; }

    public int? ExceptionsFound { get; private set; }

    public SamplingMethod? Method { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static AuditProcedure Record(
        Guid auditId, ProcedureType type, Guid? checklistItemId, Guid performedByUserId, DateOnly performedOn,
        string summary, string? counterparty,
        int? population, int? sampleSize, int? itemsTested, int? exceptionsFound, SamplingMethod? method)
    {
        Guard.Against(auditId == Guid.Empty, "procedure.audit_required", "An audit is required.");
        Guard.Against(performedByUserId == Guid.Empty, "procedure.performer_required", "A performer is required.");

        var procedure = new AuditProcedure
        {
            AuditId = auditId,
            Type = type,
            ChecklistItemId = checklistItemId,
            PerformedByUserId = performedByUserId,
            PerformedOn = performedOn,
            Summary = Guard.NotNullOrWhiteSpace(summary, "procedure.summary_required", "A summary is required."),
            // Counterparty is only meaningful for interviews / walkthroughs; sampling clears it (enforced here, not just client-side).
            Counterparty = type == ProcedureType.Sampling ? null : Normalise(counterparty),
        };

        if (type == ProcedureType.Sampling)
        {
            procedure.ApplySampling(population, sampleSize, itemsTested, exceptionsFound, method);
        }
        // Interview / walkthrough: the numeric sampling fields stay null.

        return procedure;
    }

    private void ApplySampling(int? population, int? sampleSize, int? itemsTested, int? exceptionsFound, SamplingMethod? method)
    {
        Guard.Against(population is < 0, "procedure.population_negative", "Population cannot be negative.");
        Guard.Against(sampleSize is < 0, "procedure.sample_negative", "Sample size cannot be negative.");
        Guard.Against(itemsTested is < 0, "procedure.tested_negative", "Items tested cannot be negative.");
        Guard.Against(exceptionsFound is < 0, "procedure.exceptions_negative", "Exceptions found cannot be negative.");

        // Coherence chain: a lower count may only be supplied when its upper bound is too, so every provided
        // count always has a validatable bound (and the analytics error-rate never counts orphan exceptions).
        Guard.Against(sampleSize is not null && population is null, "procedure.sample_needs_population", "A sample size requires a population.");
        Guard.Against(itemsTested is not null && sampleSize is null, "procedure.tested_needs_sample", "Items tested requires a sample size.");
        Guard.Against(exceptionsFound is not null && itemsTested is null, "procedure.exceptions_need_tested", "Exceptions found requires items tested.");

        Guard.Against(population is { } p && sampleSize is { } s && s > p, "procedure.sample_exceeds_population", "The sample size cannot exceed the population.");
        Guard.Against(sampleSize is { } ss && itemsTested is { } t && t > ss, "procedure.tested_exceeds_sample", "Items tested cannot exceed the sample size.");
        Guard.Against(itemsTested is { } it && exceptionsFound is { } e && e > it, "procedure.exceptions_exceed_tested", "Exceptions found cannot exceed the items tested.");

        Population = population;
        SampleSize = sampleSize;
        ItemsTested = itemsTested;
        ExceptionsFound = exceptionsFound;
        Method = method;
    }

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
