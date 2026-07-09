namespace AuditX.Api.Contracts;

public sealed record LogTimeRequest(DateOnly WorkDate, decimal Hours, string Category, Guid? ChecklistItemId, string? Notes);

public sealed record AmendTimeEntryRequest(DateOnly WorkDate, decimal Hours, string Category, Guid? ChecklistItemId, string? Notes, string Version);
