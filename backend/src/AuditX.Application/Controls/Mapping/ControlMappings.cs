using AuditX.Application.Common.Enums;
using AuditX.Application.Controls.Dtos;
using AuditX.Domain.Controls;

namespace AuditX.Application.Controls.Mapping;

public static class ControlMappings
{
    public static ControlDto ToDto(this Control c) => new(
        c.Id, c.Code, c.Title, c.Description, c.ControlType.ToSnake(), c.Frequency.ToSnake(),
        c.OwnerUserId, c.AuditableEntityId, c.Effectiveness.ToSnake(), c.LastTestedDate, c.IsActive,
        Convert.ToBase64String(c.Version ?? []));

    public static ControlListItemDto ToListItemDto(this Control c) => new(
        c.Id, c.Code, c.Title, c.ControlType.ToSnake(), c.Frequency.ToSnake(),
        c.OwnerUserId, c.Effectiveness.ToSnake(), c.LastTestedDate, c.IsActive);
}
