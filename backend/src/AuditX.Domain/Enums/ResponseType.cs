namespace AuditX.Domain.Enums;

/// <summary>
/// The kind of response a checklist item expects. v2.0 ships a single type; the enum exists so future
/// response types can be added without a breaking change.
/// </summary>
public enum ResponseType
{
    PassFailNa,
}
