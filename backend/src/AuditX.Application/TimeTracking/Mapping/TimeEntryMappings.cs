using AuditX.Application.Common.Enums;
using AuditX.Application.TimeTracking.Dtos;
using AuditX.Domain.TimeTracking;

namespace AuditX.Application.TimeTracking.Mapping;

public static class TimeEntryMappings
{
    public static TimeEntryDto ToDto(this TimeEntry entry) => new(
        entry.Id,
        entry.AuditId,
        entry.UserId,
        entry.ChecklistItemId,
        entry.WorkDate,
        entry.Hours,
        entry.Category.ToSnake(),
        entry.Notes,
        Convert.ToBase64String(entry.Version ?? []));
}
