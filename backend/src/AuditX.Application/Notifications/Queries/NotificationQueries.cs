using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Notifications.Dtos;
using AuditX.Application.Notifications.Mapping;
using AuditX.Application.Notifications.Services;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Application.Notifications.Queries;

public sealed record ListNotificationRulesQuery : IQuery<IReadOnlyList<NotificationRuleDto>>;

public sealed class ListNotificationRulesQueryHandler(INotificationRuleRepository rules) : IQueryHandler<ListNotificationRulesQuery, IReadOnlyList<NotificationRuleDto>>
{
    public async Task<IReadOnlyList<NotificationRuleDto>> Handle(ListNotificationRulesQuery query, CancellationToken cancellationToken)
        => (await rules.ListAsync(cancellationToken)).Select(r => r.ToDto()).ToArray();
}

public sealed record ListNotificationTemplatesQuery : IQuery<IReadOnlyList<NotificationTemplateDto>>;

public sealed class ListNotificationTemplatesQueryHandler(INotificationTemplateRepository templates) : IQueryHandler<ListNotificationTemplatesQuery, IReadOnlyList<NotificationTemplateDto>>
{
    public async Task<IReadOnlyList<NotificationTemplateDto>> Handle(ListNotificationTemplatesQuery query, CancellationToken cancellationToken)
        => (await templates.ListAsync(cancellationToken)).Select(t => t.ToDto()).ToArray();
}

public sealed record ListDispatchesQuery(string? Status, string? EventType, Guid? RecipientUserId, int? Page, int? PageSize) : IQuery<PagedResult<NotificationDispatchDto>>;

public sealed class ListDispatchesQueryHandler(INotificationDispatchRepository dispatches) : IQueryHandler<ListDispatchesQuery, PagedResult<NotificationDispatchDto>>
{
    public async Task<PagedResult<NotificationDispatchDto>> Handle(ListDispatchesQuery query, CancellationToken cancellationToken)
    {
        DispatchStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = Enum.TryParse<DispatchStatus>(query.Status.Replace("_", string.Empty), ignoreCase: true, out var s)
                ? s
                : throw new DomainException("notification.invalid_status", $"Unknown status '{query.Status}'.");
        }

        var result = await dispatches.SearchAsync(status, query.EventType, query.RecipientUserId, PageSpec.Of(query.Page, query.PageSize), cancellationToken);
        return result.Map(d => d.ToDto());
    }
}

public sealed record ListDeadLetterQuery(int? Page, int? PageSize) : IQuery<PagedResult<NotificationDispatchDto>>;

public sealed class ListDeadLetterQueryHandler(INotificationDispatchRepository dispatches) : IQueryHandler<ListDeadLetterQuery, PagedResult<NotificationDispatchDto>>
{
    public async Task<PagedResult<NotificationDispatchDto>> Handle(ListDeadLetterQuery query, CancellationToken cancellationToken)
    {
        var result = await dispatches.SearchAsync(DispatchStatus.DeadLetter, null, null, PageSpec.Of(query.Page, query.PageSize), cancellationToken);
        return result.Map(d => d.ToDto());
    }
}

public sealed record GetEventCatalogueQuery : IQuery<IReadOnlyList<string>>;

public sealed class GetEventCatalogueQueryHandler : IQueryHandler<GetEventCatalogueQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(GetEventCatalogueQuery query, CancellationToken cancellationToken)
        => Task.FromResult(NotificationEvents.Catalogue);
}

public sealed record PreviewNotificationRuleQuery(string RecipientResolutionJson, string TemplateKey, string SamplePayloadJson) : IQuery<RulePreviewDto>;

public sealed class PreviewNotificationRuleQueryHandler(NotificationIngestService ingest) : IQueryHandler<PreviewNotificationRuleQuery, RulePreviewDto>
{
    public Task<RulePreviewDto> Handle(PreviewNotificationRuleQuery query, CancellationToken cancellationToken)
        => ingest.PreviewAsync(query.RecipientResolutionJson, query.TemplateKey, query.SamplePayloadJson, cancellationToken);
}
