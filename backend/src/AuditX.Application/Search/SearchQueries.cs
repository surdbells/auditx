using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Domain.Authorization;

namespace AuditX.Application.Search;

/// <summary>A single global-search hit. <see cref="Type"/> discriminates the module so the client can route + icon it.</summary>
public sealed record SearchHitDto(string Type, string Id, string Title, string? Subtitle);

public sealed record GlobalSearchResultsDto(IReadOnlyList<SearchHitDto> Hits);

/// <summary>
/// Cross-module quick search for the header. Returns up to a few hits per module, and only for the modules the
/// current user is permitted to view — the effective permission set is resolved once and each group is gated on it.
/// </summary>
public sealed record SearchQuery(string? Q) : IQuery<GlobalSearchResultsDto>;

public sealed class SearchQueryHandler(
    ICurrentUser currentUser,
    IPermissionResolver permissions,
    IClock clock,
    IAuditRepository audits,
    IAnnualPlanRepository plans,
    IExceptionRepository exceptions,
    ITemplateRepository templates,
    IUserRepository users)
    : IQueryHandler<SearchQuery, GlobalSearchResultsDto>
{
    private const int PerModule = 5;
    private const int MinTermLength = 2;

    public async Task<GlobalSearchResultsDto> Handle(SearchQuery query, CancellationToken cancellationToken)
    {
        var term = query.Q?.Trim();
        if (currentUser.UserId is not { } userId || string.IsNullOrWhiteSpace(term) || term.Length < MinTermLength)
        {
            return new GlobalSearchResultsDto([]);
        }

        var effective = await permissions.GetEffectivePermissionsAsync(userId, cancellationToken);
        var granted = effective.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var page = PageRequest.Of(null, PerModule);
        // Offset spec for repositories already migrated to PagedResult (cursor `page` above covers the rest during the migration).
        var pageSpec = PageSpec.Of(1, PerModule);
        var hits = new List<SearchHitDto>();

        if (granted.Contains(PermissionKeys.ViewAudits))
        {
            var result = await audits.SearchAsync(null, null, null, null, term, pageSpec, cancellationToken);
            hits.AddRange(result.Items.Select(a => new SearchHitDto("audit", a.Id.ToString(), a.Name, a.Status.ToString())));
        }

        if (granted.Contains(PermissionKeys.ViewPlan))
        {
            var result = await plans.SearchByLabelAsync(term, page, cancellationToken);
            hits.AddRange(result.Items.Select(p => new SearchHitDto("plan", p.Id.ToString(), p.PeriodLabel, p.Status.ToString())));
        }

        if (granted.Contains(PermissionKeys.ViewExceptions))
        {
            var filter = new ExceptionSearchFilter(
                null, null, null, null, null, null, null, null,
                DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), term);
            var result = await exceptions.SearchAsync(filter, page, cancellationToken);
            hits.AddRange(result.Items.Select(e => new SearchHitDto("exception", e.Id.ToString(), e.Title, e.Severity.ToString())));
        }

        if (granted.Contains(PermissionKeys.ViewTemplates))
        {
            var result = await templates.SearchAsync(null, null, term, page, cancellationToken);
            hits.AddRange(result.Items.Select(t => new SearchHitDto("template", t.Id.ToString(), t.Name, t.AuditType)));
        }

        if (granted.Contains(PermissionKeys.ManageUsers))
        {
            var result = await users.SearchAsync(term, null, null, page, cancellationToken);
            hits.AddRange(result.Items.Select(u => new SearchHitDto("user", u.Id.ToString(), u.DisplayName, u.Email)));
        }

        return new GlobalSearchResultsDto(hits);
    }
}
