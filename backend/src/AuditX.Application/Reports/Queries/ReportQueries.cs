using System.Security.Cryptography;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Reports.Dtos;
using AuditX.Application.Reports.Generation;
using AuditX.Application.Reports.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;

namespace AuditX.Application.Reports.Queries;

// ---- List versions for an audit ----

public sealed record ListAuditReportsQuery(Guid AuditId, string? Cursor, int? Limit) : IQuery<CursorPage<ReportListItemDto>>;

public sealed class ListAuditReportsQueryHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListAuditReportsQuery, CursorPage<ReportListItemDto>>
{
    public async Task<CursorPage<ReportListItemDto>> Handle(ListAuditReportsQuery query, CancellationToken cancellationToken)
    {
        var audit = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        await ReportAccess.EnsureCanAccessAsync(audit, currentUser.UserId, permissions, cancellationToken);

        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await reports.ListByAuditAsync(query.AuditId, page, cancellationToken);
        return new CursorPage<ReportListItemDto>(result.Items.Select(r => r.ToListDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}

// ---- List standalone (cross-audit) reports ----

/// <summary>Lists standalone reports, newest first; optionally scoped to a single <paramref name="Kind"/>.</summary>
public sealed record ListStandaloneReportsQuery(ReportKind? Kind, string? Cursor, int? Limit) : IQuery<CursorPage<ReportListItemDto>>;

public sealed class ListStandaloneReportsQueryHandler(
    IReportRepository reports, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListStandaloneReportsQuery, CursorPage<ReportListItemDto>>
{
    public async Task<CursorPage<ReportListItemDto>> Handle(ListStandaloneReportsQuery query, CancellationToken cancellationToken)
    {
        await ReportAccess.EnsureCanAccessStandaloneAsync(currentUser.UserId, permissions, cancellationToken);

        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await reports.ListStandaloneAsync(query.Kind, page, cancellationToken);
        return new CursorPage<ReportListItemDto>(result.Items.Select(r => r.ToListDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}

// ---- Single report metadata / status surface ----

public sealed record GetReportQuery(Guid Id) : IQuery<ReportDto>;

public sealed class GetReportQueryHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<GetReportQuery, ReportDto>
{
    public async Task<ReportDto> Handle(GetReportQuery query, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Report", query.Id);
        await ReportAccess.EnsureCanReadAsync(report, audits, currentUser.UserId, permissions, cancellationToken);
        return report.ToDto();
    }
}

// ---- Download an artefact (verify-on-read → Critical mismatch alert → 500) ----

public sealed record DownloadReportArtefactQuery(Guid Id, string Format) : IQuery<ReportArtefactResult>;

public sealed class DownloadReportArtefactQueryHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser,
    IFileStorage storage, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : IQueryHandler<DownloadReportArtefactQuery, ReportArtefactResult>
{
    public async Task<ReportArtefactResult> Handle(DownloadReportArtefactQuery query, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Report", query.Id);
        await ReportAccess.EnsureCanReadAsync(report, audits, currentUser.UserId, permissions, cancellationToken);

        var format = (query.Format ?? string.Empty).Trim().ToLowerInvariant();
        var artefacts = ReportMappings.ParseArtefacts(report.ProducedArtefactsJson);
        var artefact = artefacts.FirstOrDefault(a => string.Equals(a.Format, format, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Report artefact", $"{query.Id}/{format}");

        var content = await storage.ReadAsync(artefact.FileKey, cancellationToken);

        // Verify-on-read: recompute SHA-256 over the stored bytes and compare to the recorded artefact hash. On a
        // mismatch, raise the Critical report_hash_mismatch alert (M10 + trail) then fail the read with a 500.
        var recomputed = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!string.Equals(recomputed, artefact.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            report.RaiseHashMismatch(artefact.Sha256, recomputed, currentUser.UserId ?? Guid.Empty);
            audit.Record(AuditEventTypes.ReportHashMismatch, AuditTargetTypes.Report, report.Id,
                payload: new { expected = artefact.Sha256, actual = recomputed, format });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ReportIntegrityException();
        }

        // The format string IS the extension for every supported format (html/docx/csv/xlsx).
        var stem = report.AuditId is { } aid ? $"audit-report-{aid}" : StandaloneReportModel.FileStemFor(report.Kind);
        var filename = $"{stem}-v{report.VersionNumber}.{format}";
        return new ReportArtefactResult(content, artefact.ContentType, filename, artefact.Sha256);
    }
}

// ---- Verify the canonical hash on demand ----

public sealed record VerifyReportHashQuery(Guid Id) : IQuery<ReportHashVerificationDto>;

public sealed class VerifyReportHashQueryHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser,
    IFileStorage storage, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : IQueryHandler<VerifyReportHashQuery, ReportHashVerificationDto>
{
    public async Task<ReportHashVerificationDto> Handle(VerifyReportHashQuery query, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Report", query.Id);
        await ReportAccess.EnsureCanReadAsync(report, audits, currentUser.UserId, permissions, cancellationToken);

        if (report.Sha256Hash is null)
        {
            throw new DomainException("report.not_completed", "The report has no canonical hash to verify yet.");
        }

        // The canonical, hashed artefact is the HTML one (always produced).
        var artefacts = ReportMappings.ParseArtefacts(report.ProducedArtefactsJson);
        var canonical = artefacts.FirstOrDefault(a => a.Format == "html") ?? artefacts.FirstOrDefault()
            ?? throw new DomainException("report.no_artefacts", "The report has no produced artefact to verify.");

        var content = await storage.ReadAsync(canonical.FileKey, cancellationToken);
        var recomputed = Convert.ToHexStringLower(SHA256.HashData(content));
        var match = string.Equals(recomputed, report.Sha256Hash, StringComparison.OrdinalIgnoreCase);

        if (!match)
        {
            report.RaiseHashMismatch(report.Sha256Hash, recomputed, currentUser.UserId ?? Guid.Empty);
            audit.Record(AuditEventTypes.ReportHashMismatch, AuditTargetTypes.Report, report.Id,
                payload: new { expected = report.Sha256Hash, actual = recomputed });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ReportHashVerificationDto(report.Sha256Hash, recomputed, match);
    }
}

// ---- Distribution log (cursor) ----

public sealed record ListReportDistributionsQuery(Guid Id, string? Cursor, int? Limit) : IQuery<CursorPage<ReportDistributionDto>>;

public sealed class ListReportDistributionsQueryHandler(
    IReportRepository reports, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListReportDistributionsQuery, CursorPage<ReportDistributionDto>>
{
    public async Task<CursorPage<ReportDistributionDto>> Handle(ListReportDistributionsQuery query, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Report", query.Id);
        await ReportAccess.EnsureCanReadAsync(report, audits, currentUser.UserId, permissions, cancellationToken);

        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await reports.ListDistributionsAsync(query.Id, page, cancellationToken);
        return new CursorPage<ReportDistributionDto>(result.Items.Select(d => d.ToDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}

// ---- Templates (ViewConfig) ----

public sealed record ListReportTemplatesQuery : IQuery<IReadOnlyList<ReportTemplateDto>>;

public sealed class ListReportTemplatesQueryHandler(IReportTemplateRepository templates)
    : IQueryHandler<ListReportTemplatesQuery, IReadOnlyList<ReportTemplateDto>>
{
    public async Task<IReadOnlyList<ReportTemplateDto>> Handle(ListReportTemplatesQuery query, CancellationToken cancellationToken)
    {
        var items = await templates.ListAsync(cancellationToken);
        return items.Select(t => t.ToDto()).ToArray();
    }
}
