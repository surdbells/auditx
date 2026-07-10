using AuditX.Application.Common.Enums;
using AuditX.Application.Sharing.Dtos;
using AuditX.Domain.Sharing;

namespace AuditX.Application.Sharing.Mapping;

internal static class SharedLinkMappings
{
    public static SharedLinkDto ToDto(this SharedLink link, DateTimeOffset nowUtc) => new(
        link.Id,
        link.Slug,
        link.TargetType.ToSnake(),
        link.TargetId,
        link.CreatedByUserId,
        link.CreatedAt,
        link.ExpiresAt,
        link.RevokedAt,
        link.IsActive(nowUtc),
        RowVersionToken.Encode(link.Version));
}
