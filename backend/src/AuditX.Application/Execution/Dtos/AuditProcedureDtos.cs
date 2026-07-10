namespace AuditX.Application.Execution.Dtos;

/// <summary>A typed fieldwork procedure (P2-C). Sampling numerics/method are null for interviews + walkthroughs.</summary>
public sealed record AuditProcedureDto(
    Guid Id,
    Guid AuditId,
    Guid? ChecklistItemId,
    string Type,
    Guid PerformedByUserId,
    DateOnly PerformedOn,
    string Summary,
    string? Counterparty,
    int? Population,
    int? SampleSize,
    int? ItemsTested,
    int? ExceptionsFound,
    string? Method,
    string Version);
