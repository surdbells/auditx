namespace AuditX.Application.Audits.Dtos;

public sealed record AuditTeamMemberDto(Guid Id, Guid UserId, string TeamRole, bool IsActive, DateTimeOffset AddedAt, DateTimeOffset? RemovedAt);

public sealed record AuditSectionDto(Guid Id, string Name, int OrderIndex);

public sealed record AuditChecklistItemDto(
    Guid Id, string? SectionName, int OrderIndex, string Prompt, string? ReferenceNotes,
    string ResponseType, string? ResponseConfigJson, Guid? AssignedUserId, bool IsRequired, string ItemState,
    string? RiskRating = null, Guid? ControlId = null);

public sealed record AuditDto(
    Guid Id,
    string Name,
    string? ScopeDescription,
    string AuditType,
    string Status,
    DateOnly StartDate,
    DateOnly TargetEndDate,
    DateOnly? ActualEndDate,
    Guid? TemplateId,
    int? TemplateVersion,
    Guid? PlanItemId,
    Guid? AuditableEntityId,
    Guid LeadUserId,
    Guid AuditeeUserId,
    string? CancellationReason,
    decimal? BudgetedHours,
    bool IsSelfAssessment,
    string Version,
    IReadOnlyList<AuditTeamMemberDto> TeamMembers,
    IReadOnlyList<AuditSectionDto> Sections,
    IReadOnlyList<AuditChecklistItemDto> ChecklistItems);

public sealed record AuditListItemDto(
    Guid Id, string Name, string AuditType, string Status, DateOnly StartDate, DateOnly TargetEndDate,
    Guid LeadUserId, int ChecklistItemCount, int RespondedItemCount, bool IsSelfAssessment = false);

public sealed record AuditCountsDto(IReadOnlyDictionary<string, int> ByStatus);

/// <summary>One entry in the audit's activity timeline (from the append-only trail): lifecycle, team + checklist events.</summary>
public sealed record AuditHistoryEntryDto(
    Guid Id, string EventType, string TargetObjectType, Guid? ActorUserId, DateTimeOffset OccurredAtUtc, string? StateJson);
