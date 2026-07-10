using AuditX.Application.Common.Enums;
using AuditX.Application.Execution.Dtos;
using AuditX.Domain.Execution;

namespace AuditX.Application.Execution.Mapping;

public static class AuditProcedureMappings
{
    public static AuditProcedureDto ToDto(this AuditProcedure p) => new(
        p.Id, p.AuditId, p.ChecklistItemId, p.Type.ToSnake(), p.PerformedByUserId, p.PerformedOn,
        p.Summary, p.Counterparty, p.Population, p.SampleSize, p.ItemsTested, p.ExceptionsFound,
        p.Method?.ToSnake(), Convert.ToBase64String(p.Version ?? []));
}
