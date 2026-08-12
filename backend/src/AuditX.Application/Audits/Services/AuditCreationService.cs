using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Domain.Audits;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;

namespace AuditX.Application.Audits.Services;

/// <summary>Captured intent for creating an audit.</summary>
public sealed record CreateAuditData(
    string Name,
    string AuditType,
    DateOnly StartDate,
    DateOnly TargetEndDate,
    string? ScopeDescription,
    Guid? TemplateId,
    int? TemplateVersion,
    Guid? PlanItemId,
    Guid? AuditableEntityId,
    Guid LeadUserId,
    Guid AuditeeUserId,
    IReadOnlyList<Guid> TeamMemberUserIds);

/// <summary>
/// Builds an <see cref="Audit"/> aggregate, copying the template's latest published version items into
/// the audit's own checklist (US-M4-001). The caller owns persistence, plan linkage and the transaction.
/// </summary>
public sealed class AuditCreationService(ITemplateRepository templates)
{
    public async Task<Audit> BuildAsync(CreateAuditData data, Guid? createdBy, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        IReadOnlyList<TemplateItemSnapshot> snapshot = [];
        int? templateVersion = null;

        if (data.TemplateId is { } templateId)
        {
            var template = await templates.GetByIdAsync(templateId, cancellationToken) ?? throw new NotFoundException("Template", templateId);
            if (template.Status != TemplateStatus.Published)
            {
                throw new ConflictException("audit.template_not_published", "Only a currently published template can seed an audit; the selected template is draft or archived.");
            }

            var version = template.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                ?? throw new ConflictException("audit.template_not_published", "The selected template has no published version to copy.");
            templateVersion = version.VersionNumber;
            snapshot = AppJson.Deserialize<List<TemplateItemSnapshot>>(version.ItemsSnapshotJson);
        }

        var audit = Audit.Create(
            data.Name.Trim(), data.AuditType.Trim(), data.StartDate, data.TargetEndDate,
            data.ScopeDescription, data.TemplateId, templateVersion, data.PlanItemId, data.AuditableEntityId,
            data.LeadUserId, data.AuditeeUserId, configurationVersionsJson: "{}", createdBy, nowUtc);

        foreach (var userId in data.TeamMemberUserIds.Distinct())
        {
            if (userId == data.LeadUserId || userId == data.AuditeeUserId)
            {
                continue;
            }

            audit.AddTeamMember(userId, TeamRole.Auditor, createdBy, nowUtc);
        }

        foreach (var item in snapshot.OrderBy(i => i.OrderIndex))
        {
            audit.AddChecklistItem(item.Prompt, item.ReferenceNotes, item.ResponseType, item.SectionName, item.IsRequired,
                assignedUserId: null, item.ResponseConfigJson, item.RiskRating);
        }

        return audit;
    }
}
