using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports;
using AuditX.Application.Sharing.Dtos;
using AuditX.Application.Sharing.Mapping;
using AuditX.Domain.Enums;

namespace AuditX.Application.Sharing.Queries;

// ---- List a report's shareable links (creator/managers) ----

public sealed record ListReportShareLinksQuery(Guid ReportId) : IQuery<IReadOnlyList<SharedLinkDto>>;

public sealed class ListReportShareLinksQueryHandler(
    IReportRepository reports, IAuditRepository audits, ISharedLinkRepository links, IPermissionResolver permissions,
    ICurrentUser currentUser, IClock clock)
    : IQueryHandler<ListReportShareLinksQuery, IReadOnlyList<SharedLinkDto>>
{
    public async Task<IReadOnlyList<SharedLinkDto>> Handle(ListReportShareLinksQuery query, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(query.ReportId, cancellationToken) ?? throw new NotFoundException("Report", query.ReportId);
        await ReportAccess.EnsureCanReadAsync(report, audits, currentUser.UserId, permissions, cancellationToken);

        var now = clock.UtcNow;
        var items = await links.ListForTargetAsync(SharedLinkTargetType.Report, report.Id, cancellationToken);
        return items.Select(l => l.ToDto(now)).ToArray();
    }
}

// ---- Resolve a slug to its target (permission-enforcing) ----

/// <summary>
/// Resolves a shareable slug to its target descriptor. Returns 404 for a missing / revoked / expired link, and
/// enforces the TARGET's own read permission — a link is a reference, never an access grant. Returns no content.
/// </summary>
public sealed record ResolveSharedLinkQuery(string Slug) : IQuery<SharedLinkTargetDto>;

public sealed class ResolveSharedLinkQueryHandler(
    IReportRepository reports, IAuditRepository audits, ISharedLinkRepository links, IPermissionResolver permissions,
    ICurrentUser currentUser, IClock clock)
    : IQueryHandler<ResolveSharedLinkQuery, SharedLinkTargetDto>
{
    public async Task<SharedLinkTargetDto> Handle(ResolveSharedLinkQuery query, CancellationToken cancellationToken)
    {
        var link = await links.GetBySlugAsync((query.Slug ?? string.Empty).Trim(), cancellationToken);
        if (link is null || !link.IsActive(clock.UtcNow))
        {
            // Don't distinguish revoked/expired from never-existed — avoid leaking which slugs were ever valid.
            throw new NotFoundException("Shared link", query.Slug ?? string.Empty);
        }

        // The viewer must be able to open the target through its own gate; the link cannot widen access.
        switch (link.TargetType)
        {
            case SharedLinkTargetType.Report:
                var report = await reports.GetByIdAsync(link.TargetId, cancellationToken) ?? throw new NotFoundException("Report", link.TargetId);
                await ReportAccess.EnsureCanReadAsync(report, audits, currentUser.UserId, permissions, cancellationToken);
                break;
            default:
                throw new NotFoundException("Shared link", query.Slug ?? string.Empty);
        }

        return new SharedLinkTargetDto(link.TargetType.ToSnake(), link.TargetId);
    }
}
