using AuditX.Application.Common.Enums;
using AuditX.Application.Scheduling.Dtos;
using AuditX.Domain.Scheduling;

namespace AuditX.Application.Scheduling.Mapping;

internal static class ReportScheduleMappings
{
    public static ReportScheduleDto ToDto(this ReportSchedule schedule)
    {
        var recipients = ReportScheduleRecipients.Parse(schedule.RecipientsJson);
        return new ReportScheduleDto(
            schedule.Id,
            schedule.Name,
            schedule.Kind.ToSnake(),
            schedule.Cadence.ToSnake(),
            recipients.UserIds,
            recipients.Emails,
            schedule.IsActive,
            schedule.NextRunAt,
            schedule.LastRunAt,
            schedule.LastReportId,
            schedule.CreatedByUserId,
            RowVersionToken.Encode(schedule.Version));
    }
}
