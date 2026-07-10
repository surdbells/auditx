namespace AuditX.Application.Sharing.Dtos;

/// <summary>A shareable link's metadata (D3-B). The slug is what appears in the shared URL.</summary>
public sealed record SharedLinkDto(
    Guid Id,
    string Slug,
    string TargetType,
    Guid TargetId,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    bool IsActive,
    string Version);

/// <summary>The minimal target descriptor a resolved link returns — never any content. The caller then opens the
/// target through its own permission-gated endpoint.</summary>
public sealed record SharedLinkTargetDto(string TargetType, Guid TargetId);
