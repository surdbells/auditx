using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Execution.Mapping;
using AuditX.Domain.Enums;

namespace AuditX.Application.Exceptions.Queries;

public sealed record ListMapActionEvidenceQuery(Guid ExceptionId, Guid ActionId) : IQuery<IReadOnlyList<EvidenceFileDto>>;

public sealed class ListMapActionEvidenceQueryHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IEvidenceRepository evidence, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListMapActionEvidenceQuery, IReadOnlyList<EvidenceFileDto>>
{
    public async Task<IReadOnlyList<EvidenceFileDto>> Handle(ListMapActionEvidenceQuery query, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(query.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", query.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var files = await evidence.ListForContextAsync(exception.AuditId, EvidenceContextType.MapAction, query.ActionId, cancellationToken);
        return files.Select(f => f.ToDto()).ToArray();
    }
}
