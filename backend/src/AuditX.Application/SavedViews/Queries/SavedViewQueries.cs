using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.SavedViews.Dtos;
using AuditX.Application.SavedViews.Mapping;

namespace AuditX.Application.SavedViews.Queries;

/// <summary>Saved views visible to the caller for a screen: their own plus any shared, owned-first then by name.</summary>
public sealed record ListSavedViewsQuery(string ViewKey) : IQuery<IReadOnlyList<SavedViewDto>>;

public sealed class ListSavedViewsQueryHandler(ISavedViewRepository views, ICurrentUser currentUser)
    : IQueryHandler<ListSavedViewsQuery, IReadOnlyList<SavedViewDto>>
{
    public async Task<IReadOnlyList<SavedViewDto>> Handle(ListSavedViewsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var items = await views.ListVisibleAsync(userId, (query.ViewKey ?? string.Empty).Trim(), cancellationToken);
        return items.Select(v => v.ToDto(userId)).ToArray();
    }
}
