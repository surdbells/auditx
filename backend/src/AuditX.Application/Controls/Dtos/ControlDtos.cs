namespace AuditX.Application.Controls.Dtos;

public sealed record ControlDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    string ControlType,
    string Frequency,
    Guid OwnerUserId,
    Guid? AuditableEntityId,
    string Effectiveness,
    DateOnly? LastTestedDate,
    bool IsActive,
    string Version);

public sealed record ControlListItemDto(
    Guid Id,
    string Code,
    string Title,
    string ControlType,
    string Frequency,
    Guid OwnerUserId,
    string Effectiveness,
    DateOnly? LastTestedDate,
    bool IsActive);
