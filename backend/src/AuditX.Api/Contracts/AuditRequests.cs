namespace AuditX.Api.Contracts;

public sealed record CreateAuditRequest(
    string Name,
    string AuditType,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    string? ScopeDescription,
    Guid? TemplateId,
    Guid? PlanItemId,
    Guid? EntityId,
    Guid LeadUserId,
    Guid AuditeeUserId,
    IReadOnlyList<Guid>? TeamMemberUserIds,
    bool BackdatingOverride,
    string? BackdatingReason);

public sealed record UpdateAuditRequest(string Name, string? ScopeDescription, DateOnly StartDate, DateOnly TargetEndDate, string Version);

public sealed record SetAuditBudgetRequest(decimal? BudgetedHours, string Version);

public sealed record TransitionAuditRequest(string TargetState, string? Reason, string Version);

public sealed record CancelAuditRequest(string Reason, string Version);

public sealed record AddAuditTeamMemberRequest(Guid UserId, string TeamRole, string Version);

public sealed record TransferAuditLeadRequest(Guid NewLeadUserId, bool RemoveOutgoing, string Version);

public sealed record AddAuditChecklistItemRequest(string Prompt, string? ReferenceNotes, string ResponseType, string? ResponseConfigJson, string? SectionName, bool IsRequired, Guid? AssignedUserId, string Version, string? RiskRating = null, Guid? ControlId = null);

public sealed record EditAuditChecklistItemRequest(string Prompt, string? ReferenceNotes, string ResponseType, string? ResponseConfigJson, string? SectionName, bool IsRequired, Guid? AssignedUserId, string Version, string? RiskRating = null, Guid? ControlId = null);

public sealed record ReorderAuditChecklistItemsRequest(IReadOnlyList<Guid> OrderedItemIds, string Version);

public sealed record ChecklistItemPlacementRequest(Guid ItemId, string? SectionName);

public sealed record ArrangeAuditChecklistItemsRequest(IReadOnlyList<ChecklistItemPlacementRequest> Placements, string Version);

public sealed record AddAuditSectionRequest(string Name, string Version);

public sealed record RenameAuditSectionRequest(string CurrentName, string NewName, string Version);

public sealed record RemoveAuditSectionRequest(string Name, string Version);

public sealed record ReorderAuditSectionsRequest(IReadOnlyList<string> OrderedSectionNames, string Version);
