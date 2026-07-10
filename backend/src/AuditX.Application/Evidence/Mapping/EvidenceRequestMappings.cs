using AuditX.Application.Common.Enums;
using AuditX.Application.Evidence.Dtos;
using AuditX.Domain.Evidence;

namespace AuditX.Application.Evidence.Mapping;

public static class EvidenceRequestMappings
{
    public static EvidenceRequestDto ToDto(this EvidenceRequest r, DateOnly today) => new(
        r.Id, r.AuditId, r.ChecklistItemId, r.Title, r.DocumentType, r.RequestedByUserId, r.RequestedOn, r.DueDate,
        r.Status.ToSnake(), r.ReceivedByUserId, r.ReceivedAt, r.WaiveReason, r.Notes, r.IsOverdue(today),
        RowVersionToken.Encode(r.Version));
}
