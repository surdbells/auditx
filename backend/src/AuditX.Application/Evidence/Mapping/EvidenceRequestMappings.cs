using AuditX.Application.Common.Enums;
using AuditX.Application.Evidence.Dtos;
using AuditX.Application.Execution.Dtos;
using AuditX.Domain.Evidence;

namespace AuditX.Application.Evidence.Mapping;

public static class EvidenceRequestMappings
{
    public static EvidenceRequestDto ToDto(this EvidenceRequest r, DateOnly today, IReadOnlyList<EvidenceFileDto>? files = null) => new(
        r.Id, r.AuditId, r.ChecklistItemId, r.ExceptionId, r.Purpose.ToSnake(), r.Title, r.DocumentType,
        r.RequestedByUserId, r.RequestedFromUserId, r.RequestedOn, r.DueDate,
        r.Status.ToSnake(), r.ReceivedByUserId, r.ReceivedAt, r.WaiveReason, r.Notes, r.IsOverdue(today),
        files ?? [], RowVersionToken.Encode(r.Version));

    // Overload keeping the previous count-based call sites compiling (fileCount is informational only here).
    public static EvidenceRequestDto ToDto(this EvidenceRequest r, DateOnly today, int fileCount) => r.ToDto(today, files: null);
}
