using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Scheduling;

/// <summary>
/// A recurring schedule that auto-generates a standalone (cross-audit) report on a cadence and delivers it to a set
/// of recipients (D3-C). The runner background job produces a new report version each time <see cref="NextRunAt"/>
/// falls due, then advances the schedule. <see cref="RecipientsJson"/> is an opaque JSON payload (<c>{userIds,emails}</c>)
/// the runner resolves for delivery — like a saved parameter set. A standalone soft-deletable, rowversion-guarded
/// aggregate. Only standalone report kinds are schedulable (an engagement report needs a specific audit).
/// </summary>
public sealed class ReportSchedule : Entity, ISoftDeletable
{
    private ReportSchedule()
    {
    }

    public string Name { get; private set; } = null!;

    public ReportKind Kind { get; private set; }

    public ReportCadence Cadence { get; private set; }

    /// <summary>Opaque JSON recipient payload (<c>{"userIds":[...],"emails":[...]}</c>) resolved by the runner.</summary>
    public string RecipientsJson { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTimeOffset NextRunAt { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    /// <summary>The report produced by the most recent run (null until the first run).</summary>
    public Guid? LastReportId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static ReportSchedule Create(
        string name, ReportKind kind, ReportCadence cadence, string recipientsJson, Guid createdByUserId, DateTimeOffset firstRunAt)
    {
        if (kind == ReportKind.AuditEngagement)
        {
            throw new DomainException("report_schedule.kind_not_standalone", "Only standalone report kinds can be scheduled.");
        }

        Guard.Against(createdByUserId == Guid.Empty, "report_schedule.creator_required", "A creator is required.");
        return new ReportSchedule
        {
            Name = Guard.NotNullOrWhiteSpace(name, "report_schedule.name_required", "A name is required."),
            Kind = kind,
            Cadence = cadence,
            RecipientsJson = Guard.NotNullOrWhiteSpace(recipientsJson, "report_schedule.recipients_required", "Recipients are required."),
            IsActive = true,
            NextRunAt = firstRunAt,
            CreatedByUserId = createdByUserId,
        };
    }

    /// <summary>Owner/admin edit: name, cadence, recipients and active flag (kind is fixed at creation).</summary>
    public void Update(string name, ReportCadence cadence, string recipientsJson, bool isActive)
    {
        Name = Guard.NotNullOrWhiteSpace(name, "report_schedule.name_required", "A name is required.");
        Cadence = cadence;
        RecipientsJson = Guard.NotNullOrWhiteSpace(recipientsJson, "report_schedule.recipients_required", "Recipients are required.");
        IsActive = isActive;
    }

    /// <summary>True when active and due to run at or before <paramref name="nowUtc"/>.</summary>
    public bool IsDue(DateTimeOffset nowUtc) => IsActive && !IsDeleted && NextRunAt <= nowUtc;

    /// <summary>
    /// Record a completed run and advance the schedule to the next slot relative to <paramref name="nowUtc"/> (so a
    /// long-dormant schedule fires once and catches up going forward rather than firing a backlog).
    /// </summary>
    public void RecordRun(Guid reportId, DateTimeOffset nowUtc)
    {
        LastRunAt = nowUtc;
        LastReportId = reportId;
        NextRunAt = NextFrom(nowUtc, Cadence);
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

    /// <summary>The next occurrence after <paramref name="fromUtc"/> for a cadence.</summary>
    public static DateTimeOffset NextFrom(DateTimeOffset fromUtc, ReportCadence cadence) => cadence switch
    {
        ReportCadence.Daily => fromUtc.AddDays(1),
        ReportCadence.Weekly => fromUtc.AddDays(7),
        ReportCadence.Monthly => fromUtc.AddMonths(1),
        ReportCadence.Quarterly => fromUtc.AddMonths(3),
        _ => fromUtc.AddDays(1),
    };
}
