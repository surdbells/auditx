using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.ReferenceData;

namespace AuditX.Application.ReferenceData;

public sealed record ListReferenceDataQuery(string Category, bool IncludeInactive) : IQuery<IReadOnlyList<ReferenceDataItemDto>>;

public sealed class ListReferenceDataQueryHandler(IReferenceDataRepository items)
    : IQueryHandler<ListReferenceDataQuery, IReadOnlyList<ReferenceDataItemDto>>
{
    public async Task<IReadOnlyList<ReferenceDataItemDto>> Handle(ListReferenceDataQuery query, CancellationToken cancellationToken)
    {
        var result = await items.ListByCategoryAsync(query.Category, query.IncludeInactive, cancellationToken);
        return result.Select(i => i.ToDto()).ToArray();
    }
}

public sealed record ListReferenceDataCategoriesQuery : IQuery<IReadOnlyList<ReferenceDataCategoryDto>>;

public sealed class ListReferenceDataCategoriesQueryHandler
    : IQueryHandler<ListReferenceDataCategoriesQuery, IReadOnlyList<ReferenceDataCategoryDto>>
{
    public Task<IReadOnlyList<ReferenceDataCategoryDto>> Handle(ListReferenceDataCategoriesQuery query, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ReferenceDataCategoryDto>>(ReferenceDataCategories.All.Select(c => c.ToDto()).ToArray());
}
