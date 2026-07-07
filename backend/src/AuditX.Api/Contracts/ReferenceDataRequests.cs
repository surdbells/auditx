namespace AuditX.Api.Contracts;

public sealed record CreateReferenceDataItemRequest(string Code, string Label, string? Description, int SortOrder);

public sealed record UpdateReferenceDataItemRequest(string Label, string? Description, int SortOrder);
