using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Evidence.Dtos;
using AuditX.Application.Evidence.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Evidence;
using FluentValidation;

namespace AuditX.Application.Evidence.Commands;

// ---- Request evidence ----

public sealed record RequestEvidenceCommand(
    Guid AuditId, Guid? ChecklistItemId, string Title, string? DocumentType, DateOnly? DueDate, string? Notes)
    : ICommand<EvidenceRequestDto>;

public sealed class RequestEvidenceCommandValidator : AbstractValidator<RequestEvidenceCommand>
{
    public RequestEvidenceCommandValidator()
    {
        RuleFor(x => x.AuditId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.DocumentType).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class RequestEvidenceCommandHandler(
    IAuditRepository audits, IEvidenceRequestRepository requests, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RequestEvidenceCommand, EvidenceRequestDto>
{
    public async Task<EvidenceRequestDto> Handle(RequestEvidenceCommand command, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        await EvidenceRequestAccess.EnsureCanActionAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        if (command.ChecklistItemId is { } itemId && auditEntity.ChecklistItems.All(i => i.Id != itemId))
        {
            throw new NotFoundException("Checklist item", itemId);
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var request = EvidenceRequest.Request(command.AuditId, command.ChecklistItemId, command.Title, command.DocumentType, userId, today, command.DueDate, command.Notes);
        requests.Add(request);
        audit.Record(AuditEventTypes.EvidenceRequested, AuditTargetTypes.EvidenceRequest, request.Id,
            after: new { request.AuditId, request.Title, request.DocumentType });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToDto(today);
    }
}

// ---- Mark received ----

public sealed record MarkEvidenceReceivedCommand(Guid Id, string Version) : ICommand<EvidenceRequestDto>;

public sealed class MarkEvidenceReceivedCommandHandler(
    IAuditRepository audits, IEvidenceRequestRepository requests, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<MarkEvidenceReceivedCommand, EvidenceRequestDto>
{
    public async Task<EvidenceRequestDto> Handle(MarkEvidenceReceivedCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Evidence request", command.Id);
        var auditEntity = await audits.GetByIdAsync(request.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", request.AuditId);
        await EvidenceRequestAccess.EnsureCanActionAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        request.EnsureVersion(command.Version);

        request.MarkReceived(currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.EvidenceReceived, AuditTargetTypes.EvidenceRequest, request.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

// ---- Waive ----

public sealed record WaiveEvidenceCommand(Guid Id, string Reason, string Version) : ICommand<EvidenceRequestDto>;

public sealed class WaiveEvidenceCommandValidator : AbstractValidator<WaiveEvidenceCommand>
{
    public WaiveEvidenceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class WaiveEvidenceCommandHandler(
    IAuditRepository audits, IEvidenceRequestRepository requests, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<WaiveEvidenceCommand, EvidenceRequestDto>
{
    public async Task<EvidenceRequestDto> Handle(WaiveEvidenceCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Evidence request", command.Id);
        var auditEntity = await audits.GetByIdAsync(request.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", request.AuditId);
        await EvidenceRequestAccess.EnsureCanActionAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        request.EnsureVersion(command.Version);

        request.Waive(command.Reason);
        audit.Record(AuditEventTypes.EvidenceWaived, AuditTargetTypes.EvidenceRequest, request.Id, after: new { command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

// ---- Delete (soft) ----

public sealed record DeleteEvidenceRequestCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class DeleteEvidenceRequestCommandHandler(
    IAuditRepository audits, IEvidenceRequestRepository requests, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteEvidenceRequestCommand, Unit>
{
    public async Task<Unit> Handle(DeleteEvidenceRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Evidence request", command.Id);
        var auditEntity = await audits.GetByIdAsync(request.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", request.AuditId);
        await EvidenceRequestAccess.EnsureCanActionAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        request.EnsureVersion(command.Version);

        request.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.EvidenceRequestDeleted, AuditTargetTypes.EvidenceRequest, request.Id, before: new { request.Title });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
