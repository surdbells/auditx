namespace AuditX.Api.Contracts;

public sealed record CreateAuditRequest(
    string Name,
    string AuditType,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    string? ScopeDescription,
    Guid? TemplateId,
    Guid? PlanItemId,
    Guid LeadUserId,
    Guid AuditeeUserId,
    IReadOnlyList<Guid>? TeamMemberUserIds,
    bool BackdatingOverride,
    string? BackdatingReason);

public sealed record UpdateAuditRequest(string Name, string? ScopeDescription, DateOnly StartDate, DateOnly TargetEndDate, string Version);

public sealed record TransitionAuditRequest(string TargetState, string? Reason, string Version);

public sealed record CancelAuditRequest(string Reason, string Version);

public sealed record AddAuditTeamMemberRequest(Guid UserId, string TeamRole, string Version);

public sealed record TransferAuditLeadRequest(Guid NewLeadUserId, bool RemoveOutgoing, string Version);

public sealed record AddAuditChecklistItemRequest(string Prompt, string? ReferenceNotes, string ResponseType, string? SectionName, bool IsRequired, Guid? AssignedUserId, string Version);

public sealed record EditAuditChecklistItemRequest(string Prompt, string? ReferenceNotes, string? SectionName, bool IsRequired, Guid? AssignedUserId, string Version);
