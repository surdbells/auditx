using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Compliance.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Controls;

namespace AuditX.Application.Compliance.Commands;

public sealed record LinkRiskToControlCommand(Guid ControlId, Guid RiskId) : ICommand<ControlRiskLinkDto>;

public sealed class LinkRiskToControlCommandHandler(
    IControlRepository controls, IRiskRepository risks, IControlRiskLinkRepository links, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<LinkRiskToControlCommand, ControlRiskLinkDto>
{
    public async Task<ControlRiskLinkDto> Handle(LinkRiskToControlCommand command, CancellationToken cancellationToken)
    {
        _ = await controls.GetByIdAsync(command.ControlId, cancellationToken) ?? throw new NotFoundException("Control", command.ControlId);
        var risk = await risks.GetByIdAsync(command.RiskId, cancellationToken) ?? throw new NotFoundException("Risk", command.RiskId);

        var existing = await links.GetLinkAsync(command.ControlId, command.RiskId, cancellationToken);
        if (existing is not null)
        {
            return new ControlRiskLinkDto(existing.Id, risk.Id, risk.Title, risk.Category, risk.Status.ToSnake(), existing.CreatedAt);
        }

        var link = ControlRiskLink.Create(command.ControlId, command.RiskId, currentUser.UserId ?? Guid.Empty);
        links.Add(link);
        audit.Record(AuditEventTypes.ControlRiskLinked, AuditTargetTypes.Control, command.ControlId,
            payload: new { command.ControlId, command.RiskId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ControlRiskLinkDto(link.Id, risk.Id, risk.Title, risk.Category, risk.Status.ToSnake(), link.CreatedAt);
    }
}

public sealed record UnlinkRiskFromControlCommand(Guid ControlId, Guid RiskId) : ICommand<Unit>;

public sealed class UnlinkRiskFromControlCommandHandler(
    IControlRiskLinkRepository links, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UnlinkRiskFromControlCommand, Unit>
{
    public async Task<Unit> Handle(UnlinkRiskFromControlCommand command, CancellationToken cancellationToken)
    {
        var link = await links.GetLinkAsync(command.ControlId, command.RiskId, cancellationToken);
        if (link is not null)
        {
            links.Remove(link);
            audit.Record(AuditEventTypes.ControlRiskUnlinked, AuditTargetTypes.Control, command.ControlId,
                payload: new { command.ControlId, command.RiskId });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}

public sealed record ListControlRisksQuery(Guid ControlId) : IQuery<IReadOnlyList<ControlRiskLinkDto>>;

public sealed class ListControlRisksQueryHandler(IControlRiskLinkRepository links)
    : IQueryHandler<ListControlRisksQuery, IReadOnlyList<ControlRiskLinkDto>>
{
    public async Task<IReadOnlyList<ControlRiskLinkDto>> Handle(ListControlRisksQuery query, CancellationToken cancellationToken)
    {
        var rows = await links.ListRisksForControlAsync(query.ControlId, cancellationToken);
        return rows.Select(r => new ControlRiskLinkDto(r.LinkId, r.RiskId, r.Title, r.Category, r.Status.ToSnake(), r.LinkedAt)).ToArray();
    }
}
