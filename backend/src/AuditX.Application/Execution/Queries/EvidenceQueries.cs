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
using AuditX.Domain.Enums;

namespace AuditX.Application.Execution.Queries;

public sealed record ListEvidenceForResponseQuery(Guid AuditId, Guid ResponseId) : IQuery<IReadOnlyList<EvidenceFileDto>>;

public sealed class ListEvidenceForResponseQueryHandler(
    IAuditRepository audits, IEvidenceRepository evidence, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListEvidenceForResponseQuery, IReadOnlyList<EvidenceFileDto>>
{
    public async Task<IReadOnlyList<EvidenceFileDto>> Handle(ListEvidenceForResponseQuery query, CancellationToken cancellationToken)
    {
        var audit = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        await AuditAccess.EnsureCanAccessAsync(audit, currentUser.UserId, permissions, cancellationToken);

        var files = await evidence.ListForContextAsync(query.AuditId, EvidenceContextType.Response, query.ResponseId, cancellationToken);
        return files.Select(f => f.ToDto()).ToArray();
    }
}

/// <summary>Globally lists flagged (integrity-quarantined) evidence for the M11 integrity admin view (AdminOps).</summary>
public sealed record FlaggedEvidenceDto(
    Guid Id, Guid AuditId, string OriginalFilename, string MimeType, long SizeBytes, string Sha256Hash, DateTimeOffset UploadedAt, Guid UploadedBy);

public sealed record ListFlaggedEvidenceQuery : IQuery<IReadOnlyList<FlaggedEvidenceDto>>;

public sealed class ListFlaggedEvidenceQueryHandler(IEvidenceRepository evidence)
    : IQueryHandler<ListFlaggedEvidenceQuery, IReadOnlyList<FlaggedEvidenceDto>>
{
    public async Task<IReadOnlyList<FlaggedEvidenceDto>> Handle(ListFlaggedEvidenceQuery query, CancellationToken cancellationToken)
    {
        var files = await evidence.ListFlaggedAsync(cancellationToken);
        return files.Select(f => new FlaggedEvidenceDto(
            f.Id, f.AuditId, f.OriginalFilename, f.MimeType, f.SizeBytes, f.Sha256Hash, f.UploadedAt, f.UploadedBy)).ToArray();
    }
}

public sealed record DownloadEvidenceQuery(Guid AuditId, Guid EvidenceId) : IQuery<EvidenceDownloadResult>;

public sealed class DownloadEvidenceQueryHandler(
    IAuditRepository audits,
    IEvidenceRepository evidence,
    IPermissionResolver permissions,
    ICurrentUser currentUser,
    IFileStorage storage,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : IQueryHandler<DownloadEvidenceQuery, EvidenceDownloadResult>
{
    public async Task<EvidenceDownloadResult> Handle(DownloadEvidenceQuery query, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        await AuditAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var file = await evidence.GetByIdAsync(query.EvidenceId, cancellationToken);
        if (file is null || file.AuditId != query.AuditId)
        {
            throw new NotFoundException("Evidence", query.EvidenceId);
        }

        // A previously-flagged file is locked until M11 admin review clears it (US-M5-008).
        if (file.IsFlagged)
        {
            throw new EvidenceLockedException();
        }

        var content = await storage.ReadAsync(file.StoragePath, cancellationToken);
        var actual = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!string.Equals(actual, file.Sha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            file.Flag();
            audit.Record(AuditEventTypes.EvidenceHashMismatch, AuditTargetTypes.EvidenceFile, file.Id,
                payload: new { expected = file.Sha256Hash, actual });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new EvidenceIntegrityException();
        }

        return new EvidenceDownloadResult(file.OriginalFilename, file.MimeType, content);
    }
}
