using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution;

namespace AuditX.Application.Engagement.Records;

/// <summary>An audit's engagement records as the signed-in user may see them (anyone who can read the audit).</summary>
public sealed record GetEngagementRecordsQuery(Guid AuditId) : IQuery<EngagementRecordsDto>;

public sealed class GetEngagementRecordsQueryHandler(EngagementRecordsContext context)
    : IQueryHandler<GetEngagementRecordsQuery, EngagementRecordsDto>
{
    public async Task<EngagementRecordsDto> Handle(GetEngagementRecordsQuery query, CancellationToken cancellationToken)
    {
        var audit = await context.LoadAuditAsync(query.AuditId, cancellationToken);
        await AuditAccess.EnsureCanViewAsync(audit, context.UserId, context.Permissions, cancellationToken);
        return await context.ViewAsync(audit, cancellationToken);
    }
}
