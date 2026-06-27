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

// ---- Version timeline for a domain (newest first, cursor-paged) ----

public sealed record GetConfigurationVersionsQuery(string Domain, string? Cursor, int? Limit) : IQuery<CursorPage<ConfigurationVersionDto>>;

public sealed class GetConfigurationVersionsQueryHandler(IBankConfigurationRepository configurations)
    : IQueryHandler<GetConfigurationVersionsQuery, CursorPage<ConfigurationVersionDto>>
{
    public async Task<CursorPage<ConfigurationVersionDto>> Handle(GetConfigurationVersionsQuery query, CancellationToken cancellationToken)
    {
        if (!ConfigurationDomains.IsKnown(query.Domain))
        {
            throw new DomainException("configuration.unknown_domain", $"Unknown configuration domain '{query.Domain}'.");
        }

        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await configurations.ListVersionsAsync(query.Domain, page, cancellationToken);
        return new CursorPage<ConfigurationVersionDto>(result.Items.Select(c => c.ToDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}
