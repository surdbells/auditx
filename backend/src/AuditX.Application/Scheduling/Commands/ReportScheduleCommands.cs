using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Scheduling.Dtos;
using AuditX.Application.Scheduling.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Scheduling;
using FluentValidation;

namespace AuditX.Application.Scheduling.Commands;

internal static class ReportScheduleConcurrency
{
    public static void EnsureVersion(this ReportSchedule schedule, string expectedVersion)
    {
        if (!string.Equals(Convert.ToBase64String(schedule.Version ?? []), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("report_schedule.concurrency_conflict", "The schedule was modified elsewhere; reload and retry.");
        }
    }

    public static ReportKind ParseStandaloneKind(string? value)
    {
        if (!EnumExtensions.TryParseSnake<ReportKind>(value, out var kind) || kind == ReportKind.AuditEngagement)
        {
            throw new DomainException("report_schedule.invalid_kind", "The report kind must be a standalone kind (not an engagement report).");
        }

        return kind;
    }

    public static ReportCadence ParseCadence(string? value)
    {
        if (!EnumExtensions.TryParseSnake<ReportCadence>(value, out var cadence))
        {
            throw new DomainException("report_schedule.invalid_cadence", "Cadence must be one of: daily, weekly, monthly, quarterly.");
        }

        return cadence;
    }

    /// <summary>
    /// The most sensitive standalone kind (per-lead performance) stays gated by its dedicated permission even when
    /// reached through a schedule — mirrors the interactive generate/view gate (<c>ReportAccess</c>). The runner
    /// generates as System (bypassing that check), so create + update are the only places to enforce it: without it a
    /// scheduler could leak scorecards it could never open itself.
    /// </summary>
    public static async Task EnsureCanScheduleKindAsync(
        ReportKind kind, Guid actorId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (kind == ReportKind.PerformanceScorecards
            && !await permissions.HasPermissionAsync(actorId, PermissionKeys.PerformanceAnalyticsView, null, cancellationToken))
        {
            throw new ForbiddenAccessException("Performance scorecards require the performance-analytics permission.");
        }
    }
}

// ---- Create ----

public sealed record CreateReportScheduleCommand(
    string Name, string Kind, string Cadence, IReadOnlyList<Guid> RecipientUserIds, IReadOnlyList<string> RecipientEmails)
    : ICommand<ReportScheduleDto>;

public sealed class CreateReportScheduleCommandValidator : AbstractValidator<CreateReportScheduleCommand>
{
    public CreateReportScheduleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x)
            .Must(x => (x.RecipientUserIds?.Count ?? 0) + (x.RecipientEmails?.Count ?? 0) > 0)
            .WithMessage("At least one recipient is required.")
            .WithErrorCode("report_schedule.recipients_required");
        RuleForEach(x => x.RecipientEmails).EmailAddress().When(x => x.RecipientEmails is not null);
    }
}

public sealed class CreateReportScheduleCommandHandler(
    IReportScheduleRepository schedules, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateReportScheduleCommand, ReportScheduleDto>
{
    public async Task<ReportScheduleDto> Handle(CreateReportScheduleCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var kind = ReportScheduleConcurrency.ParseStandaloneKind(command.Kind);
        var cadence = ReportScheduleConcurrency.ParseCadence(command.Cadence);
        await ReportScheduleConcurrency.EnsureCanScheduleKindAsync(kind, actorId, permissions, cancellationToken);
        var recipientsJson = new ReportScheduleRecipients(command.RecipientUserIds ?? [], command.RecipientEmails ?? []).ToJson();

        // First run is one cadence out (not immediately): creating a schedule must never surprise-send a report to
        // recipients on save. An immediate one-off is the on-demand standalone-generate path, not the scheduler.
        var firstRunAt = ReportSchedule.NextFrom(clock.UtcNow, cadence);
        var schedule = ReportSchedule.Create(command.Name.Trim(), kind, cadence, recipientsJson, actorId, firstRunAt);
        schedules.Add(schedule);
        audit.Record(AuditEventTypes.ReportScheduleCreated, AuditTargetTypes.ReportSchedule, schedule.Id,
            after: new { schedule.Name, kind = kind.ToString(), cadence = cadence.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return schedule.ToDto();
    }
}

// ---- Update ----

public sealed record UpdateReportScheduleCommand(
    Guid Id, string Name, string Cadence, IReadOnlyList<Guid> RecipientUserIds, IReadOnlyList<string> RecipientEmails,
    bool IsActive, string Version)
    : ICommand<ReportScheduleDto>;

public sealed class UpdateReportScheduleCommandValidator : AbstractValidator<UpdateReportScheduleCommand>
{
    public UpdateReportScheduleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x)
            .Must(x => (x.RecipientUserIds?.Count ?? 0) + (x.RecipientEmails?.Count ?? 0) > 0)
            .WithMessage("At least one recipient is required.")
            .WithErrorCode("report_schedule.recipients_required");
        RuleForEach(x => x.RecipientEmails).EmailAddress().When(x => x.RecipientEmails is not null);
    }
}

public sealed class UpdateReportScheduleCommandHandler(
    IReportScheduleRepository schedules, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateReportScheduleCommand, ReportScheduleDto>
{
    public async Task<ReportScheduleDto> Handle(UpdateReportScheduleCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var schedule = await schedules.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Report schedule", command.Id);
        schedule.EnsureVersion(command.Version);
        // Editing a scorecards schedule (e.g. re-pointing its recipients) needs the same permission as creating one.
        await ReportScheduleConcurrency.EnsureCanScheduleKindAsync(schedule.Kind, actorId, permissions, cancellationToken);
        var cadence = ReportScheduleConcurrency.ParseCadence(command.Cadence);
        var recipientsJson = new ReportScheduleRecipients(command.RecipientUserIds ?? [], command.RecipientEmails ?? []).ToJson();

        schedule.Update(command.Name.Trim(), cadence, recipientsJson, command.IsActive);
        audit.Record(AuditEventTypes.ReportScheduleUpdated, AuditTargetTypes.ReportSchedule, schedule.Id,
            after: new { schedule.Name, cadence = cadence.ToString(), schedule.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return schedule.ToDto();
    }
}

// ---- Delete (soft) ----

public sealed record DeleteReportScheduleCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class DeleteReportScheduleCommandHandler(
    IReportScheduleRepository schedules, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteReportScheduleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteReportScheduleCommand command, CancellationToken cancellationToken)
    {
        var schedule = await schedules.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Report schedule", command.Id);
        schedule.EnsureVersion(command.Version);

        schedule.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.ReportScheduleDeleted, AuditTargetTypes.ReportSchedule, schedule.Id, before: new { schedule.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
