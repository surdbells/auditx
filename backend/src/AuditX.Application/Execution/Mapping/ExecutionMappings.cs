using AuditX.Application.Common.Enums;
using AuditX.Application.Execution.Dtos;
using AuditX.Domain.Audits;
using AuditX.Domain.Evidence;

namespace AuditX.Application.Execution.Mapping;

public static class ExecutionMappings
{
    public static ChecklistResponseDto ToDto(this ChecklistResponse r) => new(
        r.Id, r.AuditId, r.ChecklistItemId, r.Verdict is { } v ? v.ToSnake() : null, r.Comment, r.ValueJson,
        r.ResponderUserId, r.IsDraft, r.ResponseVersion, r.RespondedAt);

    public static EvidenceFileDto ToDto(this EvidenceFile e) => new(
        e.Id, e.AuditId, e.ContextType.ToSnake(), e.ContextId, e.OriginalFilename, e.MimeType,
        e.SizeBytes, e.Sha256Hash, e.UploadedBy, e.UploadedAt, e.IsFlagged);
}
