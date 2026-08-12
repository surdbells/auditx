namespace AuditX.Api.Contracts;

// Controls register
public sealed record RegisterControlRequest(
    string Code, string Title, string? Description, string ControlType, string Frequency,
    Guid OwnerUserId, Guid? AuditableEntityId);

public sealed record UpdateControlRequest(
    string Title, string? Description, string ControlType, string Frequency, Guid OwnerUserId,
    Guid? AuditableEntityId, string? Effectiveness, DateOnly? LastTestedDate, string Version);

public sealed record SetControlStatusRequest(bool IsActive, string Version);

// Regulation / compliance register
public sealed record RegisterRegulationRequest(string Code, string Name, string? Authority, string? Description, string? Category);

public sealed record UpdateRegulationRequest(string Name, string? Authority, string? Description, string? Category, string Version);

public sealed record SetRegulationStatusRequest(bool IsActive, string Version);

// Finding links
public sealed record LinkControlRequest(Guid ControlId);

public sealed record LinkRegulationRequest(Guid RegulationId);

// Control ↔ risk register link
public sealed record LinkRiskToControlRequest(Guid RiskId);

// Control effectiveness test (audit-driven when AuditId+ChecklistItemId set; ad-hoc otherwise). Result optional
// when audit-driven (derived from the item's response); required for an ad-hoc test.
public sealed record RecordControlTestRequest(Guid? AuditId, Guid? ChecklistItemId, string? Result, string? Notes);
