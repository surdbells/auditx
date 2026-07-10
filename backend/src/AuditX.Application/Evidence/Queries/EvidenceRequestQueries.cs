using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Evidence.Dtos;
using AuditX.Application.Evidence.Mapping;

namespace AuditX.Application.Evidence.Queries;

public sealed record ListAuditEvidenceRequestsQuery(Guid AuditId) : IQuery<IReadOnlyList<EvidenceRequestDto>>;

public sealed class ListAuditEvidenceRequestsQueryHandler(
    IAuditRepository audits, IEvidenceRequestRepository requests, IPermissionResolver permissions, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<ListAuditEvidenceRequestsQuery, IReadOnlyList<EvidenceRequestDto>>
{
    public async Task<IReadOnlyList<EvidenceRequestDto>> Handle(ListAuditEvidenceRequestsQuery query, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        await EvidenceRequestAccess.EnsureCanViewAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var list = await requests.ListByAuditAsync(query.AuditId, cancellationToken);
        return list.Select(r => r.ToDto(today)).ToArray();
    }
}
