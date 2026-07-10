namespace AuditX.Api.Contracts;

/// <summary>Record a typed fieldwork procedure (P2-C). Sampling numerics/method are ignored for interviews + walkthroughs.</summary>
public sealed record RecordProcedureRequest(
    string Type,
    Guid? ChecklistItemId,
    DateOnly PerformedOn,
    string Summary,
    string? Counterparty,
    int? Population,
    int? SampleSize,
    int? ItemsTested,
    int? ExceptionsFound,
    string? Method);
