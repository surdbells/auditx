using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Evidence.Dtos;
using AuditX.Application.Evidence.Mapping;
using AuditX.Application.Execution.Mapping;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;

namespace AuditX.Application.Evidence.Queries;

/// <summary>Builds request DTOs with the documents the auditee has uploaded against each request attached.</summary>
internal static class EvidenceRequestDtoAssembler
{
    public static async Task<EvidenceRequestDto> WithFilesAsync(EvidenceRequest r, DateOnly today, IEvidenceRepository evidence, CancellationToken ct)
    {
        var files = await evidence.ListForContextAsync(r.AuditId, EvidenceContextType.EvidenceRequest, r.Id, ct);
        return r.ToDto(today, files.Select(f => f.ToDto()).ToArray());
    }
}

public sealed record ListAuditEvidenceRequestsQuery(Guid AuditId) : IQuery<IReadOnlyList<EvidenceRequestDto>>;

public sealed class ListAuditEvidenceRequestsQueryHandler(
    IAuditRepository audits, IEvidenceRequestRepository requests, IEvidenceRepository evidence, IPermissionResolver permissions, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<ListAuditEvidenceRequestsQuery, IReadOnlyList<EvidenceRequestDto>>
{
    public async Task<IReadOnlyList<EvidenceRequestDto>> Handle(ListAuditEvidenceRequestsQuery query, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        await EvidenceRequestAccess.EnsureCanViewAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var list = await requests.ListByAuditAsync(query.AuditId, cancellationToken);
        var dtos = new List<EvidenceRequestDto>(list.Count);
        foreach (var r in list)
        {
            dtos.Add(await EvidenceRequestDtoAssembler.WithFilesAsync(r, today, evidence, cancellationToken));
        }

        return dtos;
    }
}

/// <summary>The document requests addressed to the current user (auditee) — their upload worklist.</summary>
public sealed record MyEvidenceRequestsQuery(bool OutstandingOnly) : IQuery<IReadOnlyList<EvidenceRequestDto>>;

public sealed class MyEvidenceRequestsQueryHandler(
    IEvidenceRequestRepository requests, IEvidenceRepository evidence, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<MyEvidenceRequestsQuery, IReadOnlyList<EvidenceRequestDto>>
{
    public async Task<IReadOnlyList<EvidenceRequestDto>> Handle(MyEvidenceRequestsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var list = await requests.ListByRequestedFromAsync(userId, query.OutstandingOnly, cancellationToken);
        var dtos = new List<EvidenceRequestDto>(list.Count);
        foreach (var r in list)
        {
            dtos.Add(await EvidenceRequestDtoAssembler.WithFilesAsync(r, today, evidence, cancellationToken));
        }

        return dtos;
    }
}
