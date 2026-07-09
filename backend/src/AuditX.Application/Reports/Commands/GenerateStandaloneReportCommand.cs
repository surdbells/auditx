using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports.Dtos;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using FluentValidation;

namespace AuditX.Application.Reports.Commands;

/// <summary>
/// Requests asynchronous generation of a new STANDALONE (cross-audit) report version (M8). Returns 202 with the
/// report id (the id IS the job handle — D1). Versioning is per <see cref="ReportKind"/>. HTML is always produced;
/// <c>Docx</c> additionally requests DOCX when the active renderer supports it. Requires <c>GenerateReport</c> (gated
/// at the controller) AND <c>ViewAnalytics</c> (enforced in-handler) because the content is function-wide analytics.
/// </summary>
public sealed record GenerateStandaloneReportCommand(ReportKind Kind, bool Docx) : ICommand<ReportGenerationAcceptedDto>;

public sealed class GenerateStandaloneReportCommandValidator : AbstractValidator<GenerateStandaloneReportCommand>
{
    public GenerateStandaloneReportCommandValidator() =>
        RuleFor(x => x.Kind)
            .NotEqual(ReportKind.AuditEngagement)
            .WithMessage("An engagement report must be generated against an audit.")
            .WithErrorCode("report.kind_not_standalone");
}

public sealed class GenerateStandaloneReportCommandHandler(
    IReportRepository reports, IReportTemplateRepository templates,
    IReportGenerationQueue queue, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<GenerateStandaloneReportCommand, ReportGenerationAcceptedDto>
{
    public async Task<ReportGenerationAcceptedDto> Handle(GenerateStandaloneReportCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        if (command.Kind == ReportKind.AuditEngagement)
        {
            throw new DomainException("report.kind_not_standalone", "An engagement report must be generated against an audit.");
        }

        // Permission-only gate: the caller must hold ViewAnalytics (GenerateReport is gated at the controller).
        await ReportAccess.EnsureCanAccessStandaloneAsync(currentUser.UserId, permissions, cancellationToken);

        // A standalone report snapshots the active template when one is configured (for traceability), otherwise a
        // minimal synthetic definition — the standalone renderers compose their own sections from the analytics model,
        // so the template layout is not consulted, but the snapshot columns must still be populated.
        var template = await templates.GetActiveAsync(cancellationToken);
        var templateId = template?.Id ?? Guid.Empty;
        var templateVersion = template?.VersionNumber ?? 0;
        var templateJson = template?.TemplateDefinitionJson
            ?? $"{{\"title\":\"{StandaloneReportModel.TitleFor(command.Kind)}\"}}";

        var nextVersion = await reports.GetNextVersionForKindAsync(command.Kind, cancellationToken) + 1;

        var requestedFormats = command.Docx ? new[] { "html", "docx" } : ["html"];
        var report = Report.StartStandalone(
            command.Kind, nextVersion, templateId, templateVersion, templateJson,
            requestedFormats, actorId, clock.UtcNow);
        reports.Add(report);

        audit.Record(AuditEventTypes.ReportGenerationRequested, AuditTargetTypes.Report, report.Id,
            after: new { kind = command.Kind.ToString(), report.VersionNumber, status = ReportStatus.Pending.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Fire-and-forget the generation job AFTER the row is committed so the worker can load it.
        queue.Enqueue(report.Id);

        return new ReportGenerationAcceptedDto(report.Id, ReportStatus.Pending.ToString().ToLowerInvariant());
    }
}
