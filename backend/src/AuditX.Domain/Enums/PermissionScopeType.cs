namespace AuditX.Domain.Enums;

/// <summary>
/// Granularity at which a permission grant applies. Most permissions are <see cref="Global"/>, but
/// several may be narrowed to a specific resource so the audit function's real structure can be
/// reflected (e.g. "Close Exception" scoped to a single audit, or "Recommend Sanction" scoped to an
/// audit type). <see cref="Relational"/> scopes are resolved dynamically from a JSON predicate.
/// </summary>
public enum PermissionScopeType
{
    Global,
    AuditType,
    Audit,
    UniverseEntity,
    Report,
    Relational,
}
