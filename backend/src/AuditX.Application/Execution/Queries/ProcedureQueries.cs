using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Execution.Mapping;

namespace AuditX.Application.Execution.Queries;

public sealed record ListAuditProceduresQuery(Guid AuditId) : IQuery<IReadOnlyList<AuditProcedureDto>>;

public sealed class ListAuditProceduresQueryHandler(
    IAuditRepository audits, IAuditProcedureRepository procedures, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListAuditProceduresQuery, IReadOnlyList<AuditProcedureDto>>
{
    public async Task<IReadOnlyList<AuditProcedureDto>> Handle(ListAuditProceduresQuery query, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        await ProcedureAccess.EnsureCanViewAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var list = await procedures.ListByAuditAsync(query.AuditId, cancellationToken);
        return list.Select(p => p.ToDto()).ToArray();
    }
}
