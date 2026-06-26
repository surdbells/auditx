namespace AuditX.Application.Common.Messaging;

/// <summary>Represents the absence of a meaningful return value from a command.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
