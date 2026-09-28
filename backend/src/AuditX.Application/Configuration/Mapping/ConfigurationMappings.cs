using AuditX.Application.Configuration.Dtos;
using AuditX.Domain.Configuration;

namespace AuditX.Application.Configuration.Mapping;

public static class ConfigurationMappings
{
    public static ConfigurationVersionDto ToDto(this InstitutionConfiguration c) => new(
        c.Id,
        c.Domain,
        c.VersionNumber,
        c.DefinitionJson,
        c.IsActive,
        c.ChangeReason,
        c.CreatedByUserId,
        c.CreatedAtUtc,
        c.ActivatedBy,
        c.ActivatedAt,
        RowVersionToken.Encode(c.Version));
}
