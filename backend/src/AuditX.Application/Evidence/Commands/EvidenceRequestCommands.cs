using System.Security.Cryptography;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Evidence.Dtos;
using AuditX.Application.Evidence.Mapping;
using AuditX.Application.Execution.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Evidence.Commands;

// ---- Request evidence ----

public sealed record RequestEvidenceCommand(
    Guid AuditId, Guid? ChecklistItemId, Guid? ExceptionId, string? Purpose, string Title, string? DocumentType,
    Guid RequestedFromUserId, DateOnly? DueDate, string? Notes)
    : ICommand<EvidenceRequestDto>;

public sealed class RequestEvidenceCommandValidator : AbstractValidator<RequestEvidenceCommand>
{
    public RequestEvidenceCommandValidator()
    {
        RuleFor(x => x.AuditId).NotEmpty();
        RuleFor(x => x.RequestedFromUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.DocumentType).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class RequestEvidenceCommandHandler(
    IAuditRepository audits, IEvidenceRequestRepository requests, IUserRepository users, IPermissionResolver permissions,
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

        // The auditee the document is requested from must be a real, active account.
        var recipient = await users.GetByIdAsync(command.RequestedFromUserId, cancellationToken);
        if (recipient is null || recipient.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("evidence_request.recipient_not_assignable", "The auditee does not exist or is deactivated.");
        }

        var purpose = EvidenceRequestSupport.ParsePurpose(command.Purpose);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var request = EvidenceRequest.Request(
            command.AuditId, auditEntity.Name, command.ChecklistItemId, command.ExceptionId, purpose,
            command.Title, command.DocumentType, userId, command.RequestedFromUserId, today, command.DueDate, command.Notes);
        requests.Add(request);
        audit.Record(AuditEventTypes.EvidenceRequested, AuditTargetTypes.EvidenceRequest, request.Id,
            after: new { request.AuditId, request.Title, request.DocumentType, request.RequestedFromUserId, purpose = purpose.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToDto(today, fileCount: 0);
    }
}

// ---- Auditee uploads a document against a request ----

public sealed record UploadEvidenceRequestFileCommand(Guid RequestId, byte[] Content, string Filename, string ContentType) : ICommand<EvidenceRequestDto>;

public sealed class UploadEvidenceRequestFileCommandHandler(
    IEvidenceRequestRepository requests, IEvidenceRepository evidence, IFileStorage storage, IFileSignatureInspector inspector,
    IBankSettingsRepository bankSettings, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<UploadEvidenceRequestFileCommand, EvidenceRequestDto>
{
    public async Task<EvidenceRequestDto> Handle(UploadEvidenceRequestFileCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetByIdAsync(command.RequestId, cancellationToken) ?? throw new NotFoundException("Evidence request", command.RequestId);
        var uploaderId = currentUser.UserId ?? throw new UnauthorizedException();

        // Only the auditee the request is addressed to may upload — unless a manager is acting on the audit.
        var isRecipient = request.RequestedFromUserId == uploaderId;
        var isManager = await permissions.HasPermissionAsync(uploaderId, PermissionKeys.ManageAudit, request.AuditId.ToString(), cancellationToken);
        if (!isRecipient && !isManager)
        {
            throw new ForbiddenAccessException("Only the requested auditee may upload documents for this request.");
        }

        if (command.Content.LongLength == 0)
        {
            throw new DomainException("evidence.empty_file", "The uploaded file is empty.");
        }

        var settings = await bankSettings.GetAsync(cancellationToken);
        if (command.Content.LongLength > settings.MaxEvidenceFileMb * 1024L * 1024L)
        {
            throw new PayloadTooLargeException("evidence.file_too_large", $"The file exceeds the {settings.MaxEvidenceFileMb} MB per-file limit.");
        }

        var existing = await evidence.SumSizeForAuditAsync(request.AuditId, cancellationToken);
        if (existing + command.Content.LongLength > settings.MaxAuditEvidenceGb * 1024L * 1024L * 1024L)
        {
            throw new PayloadTooLargeException("evidence.audit_quota_exceeded", $"This audit's evidence would exceed the {settings.MaxAuditEvidenceGb} GB limit.");
        }

        if (!inspector.IsAllowed(command.Content, command.ContentType))
        {
            throw new DomainException("evidence.mime_not_allowed", "The file type is not permitted, or its contents do not match its declared type.");
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(command.Content));
        var key = $"audits/{request.AuditId}/evidence-requests/{request.Id}/{Guid.NewGuid():N}";
        var storagePath = await storage.SaveAsync(key, command.Content, cancellationToken);

        var file = EvidenceFile.Create(request.AuditId, EvidenceContextType.EvidenceRequest, request.Id, storagePath,
            command.Filename, command.ContentType, command.Content.LongLength, sha256, uploaderId, clock.UtcNow);
        evidence.Add(file);
        request.RecordUpload(uploaderId, clock.UtcNow);
        audit.Record(AuditEventTypes.EvidenceUploaded, AuditTargetTypes.EvidenceFile, file.Id, payload: new { request.Id, file.OriginalFilename });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var files = await evidence.ListForContextAsync(request.AuditId, EvidenceContextType.EvidenceRequest, request.Id, cancellationToken);
        return request.ToDto(today, files.Select(f => f.ToDto()).ToArray());
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
