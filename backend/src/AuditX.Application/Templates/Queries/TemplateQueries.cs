using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Templates.Dtos;
using AuditX.Application.Templates.Mapping;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;

namespace AuditX.Application.Templates.Queries;

/// <summary>List templates. Defaults to Published only; pass status="all" to include every state (US-M2-017).</summary>
public sealed record ListTemplatesQuery(string? AuditType, string? Status, string? Search, int? Page, int? PageSize)
    : IQuery<PagedResult<TemplateListItemDto>>;

public sealed class ListTemplatesQueryHandler(ITemplateRepository templates)
    : IQueryHandler<ListTemplatesQuery, PagedResult<TemplateListItemDto>>
{
    public async Task<PagedResult<TemplateListItemDto>> Handle(ListTemplatesQuery query, CancellationToken cancellationToken)
    {
        TemplateStatus? status;
        if (string.IsNullOrWhiteSpace(query.Status))
        {
            status = TemplateStatus.Published;
        }
        else if (string.Equals(query.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }
        else if (Enum.TryParse<TemplateStatus>(query.Status, ignoreCase: true, out var parsed))
        {
            status = parsed;
        }
        else
        {
            throw new ConflictException("invalid_status", $"Unknown template status '{query.Status}'.");
        }

        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await templates.SearchAsync(query.AuditType, status, query.Search, page, cancellationToken);
        return result.Map(t => t.ToListItemDto());
    }
}

public sealed record GetTemplateQuery(Guid Id) : IQuery<TemplateDto>;

public sealed class GetTemplateQueryHandler(ITemplateRepository templates)
    : IQueryHandler<GetTemplateQuery, TemplateDto>
{
    public async Task<TemplateDto> Handle(GetTemplateQuery query, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Template", query.Id);
        return template.ToDto();
    }
}

public sealed record ListTemplateVersionsQuery(Guid TemplateId) : IQuery<IReadOnlyList<TemplateVersionSummaryDto>>;

public sealed class ListTemplateVersionsQueryHandler(ITemplateRepository templates)
    : IQueryHandler<ListTemplateVersionsQuery, IReadOnlyList<TemplateVersionSummaryDto>>
{
    public async Task<IReadOnlyList<TemplateVersionSummaryDto>> Handle(ListTemplateVersionsQuery query, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(query.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", query.TemplateId);
        return template.Versions.OrderBy(v => v.VersionNumber)
            .Select(v => new TemplateVersionSummaryDto(v.Id, v.VersionNumber, v.PublishedAt)).ToArray();
    }
}

public sealed record GetTemplateVersionQuery(Guid TemplateId, int VersionNumber) : IQuery<TemplateVersionDetailDto>;

public sealed class GetTemplateVersionQueryHandler(ITemplateRepository templates)
    : IQueryHandler<GetTemplateVersionQuery, TemplateVersionDetailDto>
{
    public async Task<TemplateVersionDetailDto> Handle(GetTemplateVersionQuery query, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(query.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", query.TemplateId);
        var version = template.Versions.FirstOrDefault(v => v.VersionNumber == query.VersionNumber)
            ?? throw new NotFoundException("Template version", query.VersionNumber);
        return version.ToDetailDto();
    }
}

/// <summary>Structured diff between two published template versions (US-M2-013).</summary>
public sealed record GetTemplateDiffQuery(Guid TemplateId, int FromVersion, int ToVersion) : IQuery<TemplateDiffDto>;

public sealed class GetTemplateDiffQueryHandler(ITemplateRepository templates)
    : IQueryHandler<GetTemplateDiffQuery, TemplateDiffDto>
{
    public async Task<TemplateDiffDto> Handle(GetTemplateDiffQuery query, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(query.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", query.TemplateId);

        var from = Snapshot(template, query.FromVersion);
        var to = Snapshot(template, query.ToVersion);

        var fromByPrompt = from.ToDictionary(i => i.Prompt, StringComparer.Ordinal);
        var toByPrompt = to.ToDictionary(i => i.Prompt, StringComparer.Ordinal);

        var added = to.Where(i => !fromByPrompt.ContainsKey(i.Prompt)).Select(i => i.ToSnapshotDto()).ToArray();
        var removed = from.Where(i => !toByPrompt.ContainsKey(i.Prompt)).Select(i => i.ToSnapshotDto()).ToArray();
        var modified = to
            .Where(i => fromByPrompt.TryGetValue(i.Prompt, out var before) && !Equals(before, i))
            .Select(i => new TemplateDiffModifiedDto(i.Prompt, fromByPrompt[i.Prompt].ToSnapshotDto(), i.ToSnapshotDto()))
            .ToArray();

        return new TemplateDiffDto(query.FromVersion, query.ToVersion, added, removed, modified);
    }

    private static List<TemplateItemSnapshot> Snapshot(Template template, int versionNumber)
    {
        var version = template.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber)
            ?? throw new NotFoundException("Template version", versionNumber);
        return AppJson.Deserialize<List<TemplateItemSnapshot>>(version.ItemsSnapshotJson);
    }
}
