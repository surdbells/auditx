using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using FluentValidation;

namespace AuditX.Application.Reports.Commands;

/// <summary>
/// Requests asynchronous generation of a new report version for an audit (M8). Returns 202 with the report id (the
/// report id IS the job handle — D1). The audit must be UnderReview or Completed (422). HTML is always produced;
/// <c>Docx</c> additionally requests the DOCX format when the active renderer supports it.
/// </summary>
public sealed record GenerateReportCommand(Guid AuditId, bool Docx) : ICommand<ReportGenerationAcceptedDto>;

public sealed class GenerateReportCommandValidator : AbstractValidator<GenerateReportCommand>
{
    public GenerateReportCommandValidator() => RuleFor(x => x.AuditId).NotEmpty();
}

public sealed class GenerateReportCommandHandler(
    IReportRepository reports, IReportTemplateRepository templates, IAuditRepository audits,
    IReportGenerationQueue queue, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<GenerateReportCommand, ReportGenerationAcceptedDto>
{
    public async Task<ReportGenerationAcceptedDto> Handle(GenerateReportCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        var auditEntity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        await ReportAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        // A report is generated against a frozen/near-frozen audit (US-M8-002).
        if (auditEntity.Status is not (AuditStatus.UnderReview or AuditStatus.Completed))
        {
            throw new DomainException("report.audit_not_reviewable", "A report can only be generated for an audit that is under review or completed.");
        }

        var template = await templates.GetActiveAsync(cancellationToken)
            ?? throw new DomainException("report.no_active_template", "No active report template is configured.");

        var nextVersion = await reports.GetNextVersionAsync(command.AuditId, cancellationToken) + 1;

        var requestedFormats = command.Docx ? new[] { "html", "docx" } : ["html"];
        var report = Report.Start(
            command.AuditId, nextVersion, template.Id, template.VersionNumber, template.TemplateDefinitionJson,
            requestedFormats, actorId, clock.UtcNow);
        reports.Add(report);

        // Record the REQUEST here (report is still pending); the completion/failure trail entries are written by
        // ReportGenerationService once the async job actually finishes, so the trail never claims a pending/failed
        // report was "generated".
        audit.Record(AuditEventTypes.ReportGenerationRequested, AuditTargetTypes.Report, report.Id,
            after: new { report.AuditId, report.VersionNumber, status = ReportStatus.Pending.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Fire-and-forget the generation job AFTER the row is committed so the worker can load it.
        queue.Enqueue(report.Id);

        return new ReportGenerationAcceptedDto(report.Id, ReportStatus.Pending.ToString().ToLowerInvariant());
    }
}
