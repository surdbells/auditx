namespace AuditX.Application.Configuration.Dtos;

/// <summary>One configuration version projection (used for both the active-config read and the version timeline).</summary>
public sealed record ConfigurationVersionDto(
    Guid Id,
    string Domain,
    int VersionNumber,
    string DefinitionJson,
    bool IsActive,
    string ChangeReason,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc,
    Guid? ActivatedBy,
    DateTimeOffset? ActivatedAt,
    string Version);

/// <summary>
/// Result of a maker-checker-gateable configuration action (activate/rollback): either the resulting version or a
/// captured pending-action id when the action is held for a second approver. Mirrors SanctionsGridActionResult.
/// </summary>
public sealed record ConfigurationActionResult(ConfigurationVersionDto? Version, Guid? PendingActionId);
