using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Configuration.Dtos;
using AuditX.Application.Configuration.Mapping;
using AuditX.Domain.Common;
using AuditX.Domain.Configuration;

namespace AuditX.Application.Configuration.Queries;

// ---- Active configuration for a domain ----

public sealed record GetActiveConfigurationQuery(string Domain) : IQuery<ConfigurationVersionDto?>;

public sealed class GetActiveConfigurationQueryHandler(IBankConfigurationRepository configurations)
    : IQueryHandler<GetActiveConfigurationQuery, ConfigurationVersionDto?>
{
    public async Task<ConfigurationVersionDto?> Handle(GetActiveConfigurationQuery query, CancellationToken cancellationToken)
    {
        if (!ConfigurationDomains.IsKnown(query.Domain))
        {
            throw new DomainException("configuration.unknown_domain", $"Unknown configuration domain '{query.Domain}'.");
        }

        var active = await configurations.GetActiveAsync(query.Domain, cancellationToken);
        return active?.ToDto();
    }
}

// ---- Version timeline for a domain (newest first, offset-paged) ----

public sealed record GetConfigurationVersionsQuery(string Domain, int? Page, int? PageSize) : IQuery<PagedResult<ConfigurationVersionDto>>;

public sealed class GetConfigurationVersionsQueryHandler(IBankConfigurationRepository configurations)
    : IQueryHandler<GetConfigurationVersionsQuery, PagedResult<ConfigurationVersionDto>>
{
    public async Task<PagedResult<ConfigurationVersionDto>> Handle(GetConfigurationVersionsQuery query, CancellationToken cancellationToken)
    {
        if (!ConfigurationDomains.IsKnown(query.Domain))
        {
            throw new DomainException("configuration.unknown_domain", $"Unknown configuration domain '{query.Domain}'.");
        }

        var result = await configurations.ListVersionsAsync(query.Domain, PageSpec.Of(query.Page, query.PageSize), cancellationToken);
        return result.Map(c => c.ToDto());
    }
}
