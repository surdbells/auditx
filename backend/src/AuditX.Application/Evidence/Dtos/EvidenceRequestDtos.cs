using AuditX.Application.Execution.Dtos;

namespace AuditX.Application.Evidence.Dtos;

/// <summary>An expected / requested piece of audit evidence (P2-D), with any documents the auditee has uploaded.</summary>
public sealed record EvidenceRequestDto(
    Guid Id,
    Guid AuditId,
    Guid? ChecklistItemId,
    Guid? ExceptionId,
    string Purpose,
    string Title,
    string? DocumentType,
    Guid RequestedByUserId,
    Guid RequestedFromUserId,
    DateOnly RequestedOn,
    DateOnly? DueDate,
    string Status,
    Guid? ReceivedByUserId,
    DateTimeOffset? ReceivedAt,
    string? WaiveReason,
    string? Notes,
    bool IsOverdue,
    IReadOnlyList<EvidenceFileDto> Files,
    string Version);
