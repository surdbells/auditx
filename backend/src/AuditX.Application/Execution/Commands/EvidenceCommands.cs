using System.Security.Cryptography;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Execution.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;

namespace AuditX.Application.Execution.Commands;

public sealed record UploadEvidenceCommand(Guid AuditId, Guid ResponseId, byte[] Content, string Filename, string ContentType) : ICommand<EvidenceFileDto>;

public sealed class UploadEvidenceCommandHandler(
    IAuditRepository audits,
    IEvidenceRepository evidence,
    IFileStorage storage,
    IFileSignatureInspector inspector,
    IInstitutionSettingsRepository institutionSettings,
    IPermissionResolver permissions,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UploadEvidenceCommand, EvidenceFileDto>
{
    public async Task<EvidenceFileDto> Handle(UploadEvidenceCommand command, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        await AuditAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        var uploaderId = currentUser.UserId ?? throw new UnauthorizedException();
        var response = auditEntity.Responses.FirstOrDefault(r => r.Id == command.ResponseId)
            ?? throw new NotFoundException("Response", command.ResponseId);

        if (auditEntity.Status != AuditStatus.InProgress)
        {
            throw new ConflictException("evidence.audit_not_in_progress", "Evidence can only be attached while the audit is in progress.");
        }

        if (command.Content.LongLength == 0)
        {
            throw new DomainException("evidence.empty_file", "The uploaded file is empty.");
        }

        var settings = await institutionSettings.GetAsync(cancellationToken);
        var maxFileBytes = settings.MaxEvidenceFileMb * 1024L * 1024L;
        if (command.Content.LongLength > maxFileBytes)
        {
            throw new PayloadTooLargeException("evidence.file_too_large", $"The file exceeds the {settings.MaxEvidenceFileMb} MB per-file limit.");
        }

        var existing = await evidence.SumSizeForAuditAsync(command.AuditId, cancellationToken);
        var maxAuditBytes = settings.MaxAuditEvidenceGb * 1024L * 1024L * 1024L;
        if (existing + command.Content.LongLength > maxAuditBytes)
        {
            throw new PayloadTooLargeException("evidence.audit_quota_exceeded", $"This audit's evidence would exceed the {settings.MaxAuditEvidenceGb} GB limit.");
        }

        if (!inspector.IsAllowed(command.Content, command.ContentType))
        {
            throw new DomainException("evidence.mime_not_allowed", "The file type is not permitted, or its contents do not match its declared type.");
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(command.Content));
        var storageKey = $"audits/{command.AuditId}/responses/{command.ResponseId}/{Guid.NewGuid():N}";
        var storagePath = await storage.SaveAsync(storageKey, command.Content, cancellationToken);

        var file = EvidenceFile.Create(
            command.AuditId, EvidenceContextType.Response, command.ResponseId, storagePath,
            command.Filename, command.ContentType, command.Content.LongLength, sha256, uploaderId, clock.UtcNow);

        evidence.Add(file);
        audit.Record(AuditEventTypes.EvidenceUploaded, AuditTargetTypes.EvidenceFile, file.Id,
            payload: new { command.ResponseId, file.OriginalFilename, file.SizeBytes });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return file.ToDto();
    }
}

public sealed record SoftDeleteEvidenceCommand(Guid AuditId, Guid EvidenceId, string Reason) : ICommand<Unit>;

public sealed class SoftDeleteEvidenceCommandHandler(
    IAuditRepository audits, IEvidenceRepository evidence, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SoftDeleteEvidenceCommand, Unit>
{
    public async Task<Unit> Handle(SoftDeleteEvidenceCommand command, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        await AuditAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var file = await evidence.GetByIdAsync(command.EvidenceId, cancellationToken);
        if (file is null || file.AuditId != command.AuditId)
        {
            throw new NotFoundException("Evidence", command.EvidenceId);
        }

        file.SoftDelete(command.Reason, currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.EvidenceSoftDeleted, AuditTargetTypes.EvidenceFile, file.Id, payload: new { command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Clear an evidence integrity flag after investigation (US-M11 evidence unflag). Reason is mandatory.</summary>
public sealed record UnflagEvidenceCommand(Guid AuditId, Guid EvidenceId, string Resolution) : ICommand<Unit>;

public sealed class UnflagEvidenceCommandHandler(
    IAuditRepository audits, IEvidenceRepository evidence, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UnflagEvidenceCommand, Unit>
{
    public async Task<Unit> Handle(UnflagEvidenceCommand command, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        await AuditAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var file = await evidence.GetByIdAsync(command.EvidenceId, cancellationToken);
        if (file is null || file.AuditId != command.AuditId)
        {
            throw new NotFoundException("Evidence", command.EvidenceId);
        }

        file.ClearFlag(command.Resolution); // guards not-flagged + blank-resolution (422).
        audit.Record(AuditEventTypes.EvidenceUnflagged, AuditTargetTypes.EvidenceFile, file.Id, payload: new { command.Resolution });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
