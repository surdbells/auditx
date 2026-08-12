using AuditX.Application.Audits.Dtos;
using AuditX.Application.Common.Enums;
using AuditX.Domain.Audits;
using AuditX.Domain.Enums;

namespace AuditX.Application.Audits.Mapping;

public static class AuditMappings
{
    public static AuditDto ToDto(this Audit audit) => new(
        audit.Id,
        audit.Name,
        audit.ScopeDescription,
        audit.AuditType,
        audit.Status.ToSnake(),
        audit.StartDate,
        audit.TargetEndDate,
        audit.ActualEndDate,
        audit.TemplateId,
        audit.TemplateVersion,
        audit.PlanItemId,
        audit.AuditableEntityId,
        audit.LeadUserId,
        audit.AuditeeUserId,
        audit.CancellationReason,
        audit.BudgetedHours,
        RowVersionToken.Encode(audit.Version),
        audit.TeamMembers.Select(m => new AuditTeamMemberDto(m.Id, m.UserId, m.TeamRole.ToSnake(), m.IsActive, m.AddedAt, m.RemovedAt)).ToArray(),
        audit.Sections.OrderBy(s => s.OrderIndex).Select(s => new AuditSectionDto(s.Id, s.Name, s.OrderIndex)).ToArray(),
        audit.ChecklistItems.OrderBy(i => i.OrderIndex).Select(i => i.ToDto()).ToArray());

    public static AuditChecklistItemDto ToDto(this AuditChecklistItem item) => new(
        item.Id, item.SectionName, item.OrderIndex, item.Prompt, item.ReferenceNotes,
        item.ResponseType.ToSnake(), item.ResponseConfigJson, item.AssignedUserId, item.IsRequired, item.ItemState.ToSnake(),
        item.RiskRating?.ToSnake());

    public static AuditListItemDto ToListItemDto(this Audit audit) => new(
        audit.Id, audit.Name, audit.AuditType, audit.Status.ToSnake(), audit.StartDate, audit.TargetEndDate,
        audit.LeadUserId, audit.ChecklistItems.Count, audit.ChecklistItems.Count(i => i.ItemState == ChecklistItemState.Responded));
}
