using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports.Dtos;
using AuditX.Application.Reports.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using FluentValidation;

namespace AuditX.Application.Reports.Commands;

/// <summary>
/// Draft-to-final review of an engagement report. A DRAFT is discussed with management, who comment on it; the FINAL
/// is signed off by the Chief Internal Auditor (the CIA authority) — never by whoever generated it, and never by the
/// audit's own auditee — before it can be distributed. A returned final cannot go out; the team generates a new one.
/// </summary>
internal static class ReportReview
{
    public static async Task<(Report Report, Domain.Audits.Audit Audit)> LoadForSignOffAsync(
        Guid reportId, IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions,
        ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var report = await reports.GetByIdAsync(reportId, cancellationToken) ?? throw new NotFoundException("Report", reportId);
        if (report.AuditId is not { } auditId)
        {
            throw new DomainException("report.signoff_not_final", "Only a final engagement report is signed off.");
        }

        var audit = await audits.GetByIdAsync(auditId, cancellationToken) ?? throw new NotFoundException("Audit", auditId);
        Execution.AuditIndependence.EnsureNotAuditee(audit, userId);
        if (!await permissions.HasPermissionAsync(userId, PermissionKeys.Cia, scopeValue: null, cancellationToken))
        {
            throw new ForbiddenAccessException("Only the Chief Internal Auditor can sign off a final report.");
        }

        return (report, audit);
    }
}

// ---- Sign off ----

public sealed record SignOffReportCommand(Guid ReportId, string? Note) : ICommand<ReportDto>;

public sealed class SignOffReportCommandValidator : AbstractValidator<SignOffReportCommand>
{
    public SignOffReportCommandValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class SignOffReportCommandHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SignOffReportCommand, ReportDto>
{
    public async Task<ReportDto> Handle(SignOffReportCommand command, CancellationToken cancellationToken)
    {
        var (report, _) = await ReportReview.LoadForSignOffAsync(command.ReportId, reports, audits, permissions, currentUser, cancellationToken);
        report.SignOff(currentUser.UserId!.Value, command.Note, clock.UtcNow);
        audit.Record(AuditEventTypes.ReportSignedOff, AuditTargetTypes.Report, report.Id,
            after: new { report.AuditId, report.VersionNumber, report.Sha256Hash, note = report.SignOffNote });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return report.ToDto();
    }
}

// ---- Return for changes ----

public sealed record ReturnReportCommand(Guid ReportId, string Reason) : ICommand<ReportDto>;

public sealed class ReturnReportCommandValidator : AbstractValidator<ReturnReportCommand>
{
    public ReturnReportCommandValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(2000);
    }
}

public sealed class ReturnReportCommandHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ReturnReportCommand, ReportDto>
{
    public async Task<ReportDto> Handle(ReturnReportCommand command, CancellationToken cancellationToken)
    {
        var (report, _) = await ReportReview.LoadForSignOffAsync(command.ReportId, reports, audits, permissions, currentUser, cancellationToken);
        report.ReturnForChanges(currentUser.UserId!.Value, command.Reason, clock.UtcNow);
        audit.Record(AuditEventTypes.ReportReturnedForChanges, AuditTargetTypes.Report, report.Id,
            after: new { report.AuditId, report.VersionNumber, reason = report.SignOffNote });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return report.ToDto();
    }
}

// ---- Comments on a draft ----

public sealed record AddReportCommentCommand(Guid ReportId, string Body) : ICommand<ReportCommentDto>;

public sealed class AddReportCommentCommandValidator : AbstractValidator<AddReportCommentCommand>
{
    public AddReportCommentCommandValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(ReportComment.MaxBodyLength);
    }
}

/// <summary>
/// Whoever may read the draft may comment on it: the audit team, the Chief Internal Auditor, and — once the draft has
/// been sent to them — the audit's auditees.
/// </summary>
public sealed class AddReportCommentCommandHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AddReportCommentCommand, ReportCommentDto>
{
    public async Task<ReportCommentDto> Handle(AddReportCommentCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var report = await reports.GetByIdAsync(command.ReportId, cancellationToken) ?? throw new NotFoundException("Report", command.ReportId);
        await ReportAccess.EnsureCanReadAsync(report, audits, userId, permissions, cancellationToken);

        var comment = report.AddComment(userId, command.Body, clock.UtcNow);
        audit.Record(AuditEventTypes.ReportCommentAdded, AuditTargetTypes.Report, report.Id,
            after: new { report.AuditId, report.VersionNumber, commentId = comment.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return comment.ToDto();
    }
}

public sealed record ListReportCommentsQuery(Guid ReportId) : IQuery<IReadOnlyList<ReportCommentDto>>;

public sealed class ListReportCommentsQueryHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListReportCommentsQuery, IReadOnlyList<ReportCommentDto>>
{
    public async Task<IReadOnlyList<ReportCommentDto>> Handle(ListReportCommentsQuery query, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(query.ReportId, cancellationToken) ?? throw new NotFoundException("Report", query.ReportId);
        await ReportAccess.EnsureCanReadAsync(report, audits, currentUser.UserId, permissions, cancellationToken);
        return report.Comments.OrderBy(c => c.PostedAt).Select(c => c.ToDto()).ToArray();
    }
}

// ---- The Chief Internal Auditor's sign-off queue ----

public sealed record ListReportsAwaitingSignOffQuery : IQuery<IReadOnlyList<ReportSignOffQueueItemDto>>;

public sealed class ListReportsAwaitingSignOffQueryHandler(IReportRepository reports, IAuditRepository audits)
    : IQueryHandler<ListReportsAwaitingSignOffQuery, IReadOnlyList<ReportSignOffQueueItemDto>>
{
    public async Task<IReadOnlyList<ReportSignOffQueueItemDto>> Handle(ListReportsAwaitingSignOffQuery query, CancellationToken cancellationToken)
    {
        var awaiting = await reports.ListAwaitingSignOffAsync(cancellationToken);
        var result = new List<ReportSignOffQueueItemDto>(awaiting.Count);
        foreach (var report in awaiting)
        {
            var auditEntity = await audits.GetByIdAsync(report.AuditId!.Value, cancellationToken);
            if (auditEntity is null)
            {
                continue;
            }

            result.Add(new ReportSignOffQueueItemDto(
                report.Id, auditEntity.Id, auditEntity.Name, report.VersionNumber, report.GeneratedBy, report.CompletedAt));
        }

        return result;
    }
}
