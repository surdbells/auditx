using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Compliance.Dtos;
using AuditX.Application.Exceptions;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Compliance;

namespace AuditX.Application.Compliance.Commands;

// ---- Link a control to a finding ----

public sealed record LinkControlToFindingCommand(Guid ExceptionId, Guid ControlId) : ICommand<FindingControlLinkDto>;

public sealed class LinkControlToFindingCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IControlRepository controls, IFindingLinkRepository links,
    IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<LinkControlToFindingCommand, FindingControlLinkDto>
{
    public async Task<FindingControlLinkDto> Handle(LinkControlToFindingCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var control = await controls.GetByIdAsync(command.ControlId, cancellationToken) ?? throw new NotFoundException("Control", command.ControlId);

        var existing = await links.GetControlLinkAsync(command.ExceptionId, command.ControlId, cancellationToken);
        if (existing is not null)
        {
            return new FindingControlLinkDto(existing.Id, control.Id, control.Code, control.Title, existing.CreatedAt);
        }

        var link = ExceptionControlLink.Create(command.ExceptionId, command.ControlId, currentUser.UserId ?? Guid.Empty);
        links.AddControlLink(link);
        audit.Record(AuditEventTypes.FindingLinkAdded, AuditTargetTypes.FindingLink, link.Id,
            payload: new { command.ExceptionId, kind = "control", command.ControlId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FindingControlLinkDto(link.Id, control.Id, control.Code, control.Title, link.CreatedAt);
    }
}

public sealed record UnlinkControlFromFindingCommand(Guid ExceptionId, Guid ControlId) : ICommand<Unit>;

public sealed class UnlinkControlFromFindingCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IFindingLinkRepository links,
    IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UnlinkControlFromFindingCommand, Unit>
{
    public async Task<Unit> Handle(UnlinkControlFromFindingCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var link = await links.GetControlLinkAsync(command.ExceptionId, command.ControlId, cancellationToken);
        if (link is not null)
        {
            links.RemoveControlLink(link);
            audit.Record(AuditEventTypes.FindingLinkRemoved, AuditTargetTypes.FindingLink, link.Id,
                payload: new { command.ExceptionId, kind = "control", command.ControlId });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}

// ---- Link a regulation to a finding ----

public sealed record LinkRegulationToFindingCommand(Guid ExceptionId, Guid RegulationId) : ICommand<FindingRegulationLinkDto>;

public sealed class LinkRegulationToFindingCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IRegulationRepository regulations, IFindingLinkRepository links,
    IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<LinkRegulationToFindingCommand, FindingRegulationLinkDto>
{
    public async Task<FindingRegulationLinkDto> Handle(LinkRegulationToFindingCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var regulation = await regulations.GetByIdAsync(command.RegulationId, cancellationToken) ?? throw new NotFoundException("Regulation", command.RegulationId);

        var existing = await links.GetRegulationLinkAsync(command.ExceptionId, command.RegulationId, cancellationToken);
        if (existing is not null)
        {
            return new FindingRegulationLinkDto(existing.Id, regulation.Id, regulation.Code, regulation.Name, existing.CreatedAt);
        }

        var link = ExceptionRegulationLink.Create(command.ExceptionId, command.RegulationId, currentUser.UserId ?? Guid.Empty);
        links.AddRegulationLink(link);
        audit.Record(AuditEventTypes.FindingLinkAdded, AuditTargetTypes.FindingLink, link.Id,
            payload: new { command.ExceptionId, kind = "regulation", command.RegulationId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FindingRegulationLinkDto(link.Id, regulation.Id, regulation.Code, regulation.Name, link.CreatedAt);
    }
}

public sealed record UnlinkRegulationFromFindingCommand(Guid ExceptionId, Guid RegulationId) : ICommand<Unit>;

public sealed class UnlinkRegulationFromFindingCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IFindingLinkRepository links,
    IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UnlinkRegulationFromFindingCommand, Unit>
{
    public async Task<Unit> Handle(UnlinkRegulationFromFindingCommand command, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(command.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", command.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var link = await links.GetRegulationLinkAsync(command.ExceptionId, command.RegulationId, cancellationToken);
        if (link is not null)
        {
            links.RemoveRegulationLink(link);
            audit.Record(AuditEventTypes.FindingLinkRemoved, AuditTargetTypes.FindingLink, link.Id,
                payload: new { command.ExceptionId, kind = "regulation", command.RegulationId });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
