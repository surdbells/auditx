using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports.Dtos;
using AuditX.Application.Reports.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Reports;
using FluentValidation;

namespace AuditX.Application.Reports.Commands;

// ---- Create (inactive draft version) ----

public sealed record CreateReportTemplateCommand(string Name, string TemplateDefinitionJson) : ICommand<ReportTemplateDto>;

public sealed class CreateReportTemplateCommandValidator : AbstractValidator<CreateReportTemplateCommand>
{
    public CreateReportTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TemplateDefinitionJson).NotEmpty();
    }
}

public sealed class CreateReportTemplateCommandHandler(
    IReportTemplateRepository templates, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateReportTemplateCommand, ReportTemplateDto>
{
    public async Task<ReportTemplateDto> Handle(CreateReportTemplateCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        ReportTemplateDefinition.ValidateShape(command.TemplateDefinitionJson);

        var next = await templates.GetMaxVersionNumberAsync(cancellationToken) + 1;
        var template = ReportTemplate.CreateVersion(command.Name, command.TemplateDefinitionJson, next, actorId, clock.UtcNow);
        templates.Add(template);
        audit.Record(AuditEventTypes.ReportTemplateCreated, AuditTargetTypes.ReportTemplate, template.Id, after: new { template.Name, template.VersionNumber });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}

// ---- Activate (atomic single-active switch; reason ≥ 20) ----

public sealed record ActivateReportTemplateCommand(Guid TemplateId, string Reason) : ICommand<ReportTemplateDto>;

public sealed class ActivateReportTemplateCommandValidator : AbstractValidator<ActivateReportTemplateCommand>
{
    public ActivateReportTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(20);
    }
}

public sealed class ActivateReportTemplateCommandHandler(
    IReportTemplateRepository templates, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateReportTemplateCommand, ReportTemplateDto>
{
    public async Task<ReportTemplateDto> Handle(ActivateReportTemplateCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Report template", command.TemplateId);
        if (template.IsActive)
        {
            throw new ConflictException("report_template.already_active", "This report template is already active.");
        }

        // Deactivate the prior active version with an immediate set-based UPDATE so it is ordered BEFORE the tracked
        // activate below — otherwise the filtered unique index on is_active=1 can transiently see two active rows and
        // throw a spurious unique violation (the M7 grid HIGH-fix lesson).
        await templates.DeactivateActiveAsync(cancellationToken);
        template.Activate(command.Reason, actorId, clock.UtcNow);
        audit.Record(AuditEventTypes.ReportTemplateActivated, AuditTargetTypes.ReportTemplate, template.Id, after: new { template.VersionNumber, command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}
