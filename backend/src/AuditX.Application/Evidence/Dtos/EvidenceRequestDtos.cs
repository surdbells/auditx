namespace AuditX.Application.Evidence.Dtos;

/// <summary>An expected / requested piece of audit evidence (P2-D).</summary>
public sealed record EvidenceRequestDto(
    Guid Id,
    Guid AuditId,
    Guid? ChecklistItemId,
    string Title,
    string? DocumentType,
    Guid RequestedByUserId,
    DateOnly RequestedOn,
    DateOnly? DueDate,
    string Status,
    Guid? ReceivedByUserId,
    DateTimeOffset? ReceivedAt,
    string? WaiveReason,
    string? Notes,
    bool IsOverdue,
    string Version);
