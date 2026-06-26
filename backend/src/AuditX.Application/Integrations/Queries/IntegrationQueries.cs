using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Integrations.Dtos;
using AuditX.Application.Integrations.Mapping;

namespace AuditX.Application.Integrations.Queries;

public sealed record ListIntegrationsQuery : IQuery<IReadOnlyList<IntegrationDto>>;

public sealed class ListIntegrationsQueryHandler(IIntegrationRepository integrations)
    : IQueryHandler<ListIntegrationsQuery, IReadOnlyList<IntegrationDto>>
{
    public async Task<IReadOnlyList<IntegrationDto>> Handle(ListIntegrationsQuery query, CancellationToken cancellationToken)
        => (await integrations.GetAllAsync(cancellationToken)).Select(i => i.ToDto()).ToArray();
}

public sealed record GetIntegrationQuery(Guid Id) : IQuery<IntegrationDto>;

public sealed class GetIntegrationQueryHandler(IIntegrationRepository integrations)
    : IQueryHandler<GetIntegrationQuery, IntegrationDto>
{
    public async Task<IntegrationDto> Handle(GetIntegrationQuery query, CancellationToken cancellationToken)
    {
        var integration = await integrations.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Integration", query.Id);
        return integration.ToDto();
    }
}

public sealed record GetIntegrationHealthQuery(Guid Id) : IQuery<IntegrationHealthDto>;

public sealed class GetIntegrationHealthQueryHandler(IIntegrationRepository integrations)
    : IQueryHandler<GetIntegrationHealthQuery, IntegrationHealthDto>
{
    public async Task<IntegrationHealthDto> Handle(GetIntegrationHealthQuery query, CancellationToken cancellationToken)
    {
        var health = await integrations.GetHealthAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Integration health", query.Id);
        return health.ToDto();
    }
}
