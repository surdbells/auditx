using System.Security.Cryptography;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Ac.Dtos;
using AuditX.Application.Ac.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Domain.Ac;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Application.Ac.Queries;

internal static class AcPackStatusParsing
{
    public static AcPackStatus? Parse(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        foreach (var candidate in Enum.GetValues<AcPackStatus>())
        {
            if (string.Equals(candidate.ToString(), status, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Common.Enums.EnumExtensions.ToSnake(candidate), status, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new DomainException("ac_pack.invalid_status", $"Unknown AC pack status '{status}'.");
    }
}

// ---- List packs (ViewACPacks; offset + ?status filter) ----

public sealed record ListAcPacksQuery(string? Status, int? Page, int? PageSize) : IQuery<PagedResult<AcPackListItemDto>>;

public sealed class ListAcPacksQueryHandler(IAcPackRepository packs, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListAcPacksQuery, PagedResult<AcPackListItemDto>>
{
    public async Task<PagedResult<AcPackListItemDto>> Handle(ListAcPacksQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var status = AcPackStatusParsing.Parse(query.Status);

        // AC members (not CIA) only ever see approved/distributed packs — filtered in the repo query BEFORE pagination
        // so the page size + total are correct (the pack is hidden pre-approval).
        var isCia = await permissions.HasPermissionAsync(userId, PermissionKeys.Cia, cancellationToken: cancellationToken);
        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await packs.ListAsync(status, approvedOnly: !isCia, page, cancellationToken);

        return result.Map(p => p.ToListDto());
    }
}

// ---- Get a single pack (ViewACPacks; AC sees only approved/distributed in-handler) ----

public sealed record GetAcPackQuery(Guid Id) : IQuery<AcPackDto>;

public sealed class GetAcPackQueryHandler(IAcPackRepository packs, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<GetAcPackQuery, AcPackDto>
{
    public async Task<AcPackDto> Handle(GetAcPackQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var pack = await packs.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("AC pack", query.Id);
        await EnsureCanReadAsync(pack, userId, permissions, cancellationToken);
        return pack.ToDto();
    }

    /// <summary>An AC member can only read an approved/distributed pack; the CIA can read any (incl. pending/failed).</summary>
    internal static async Task EnsureCanReadAsync(AcPack pack, Guid userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (pack.Status is AcPackStatus.Approved or AcPackStatus.Distributed)
        {
            return;
        }

        if (await permissions.HasPermissionAsync(userId, PermissionKeys.Cia, cancellationToken: cancellationToken))
        {
            return;
        }

        // Do not leak existence of a not-yet-approved pack to a non-CIA member.
        throw new NotFoundException("AC pack", pack.Id);
    }
}

// ---- Get the pack analytics from the immutable snapshot (ACMember; restricted-filter per requester) ----

public sealed record GetAcPackAnalyticsQuery(Guid Id) : IQuery<AcPackAnalyticsDto>;

public sealed class GetAcPackAnalyticsQueryHandler(
    IAcPackRepository packs, IFindingVisibilityRestrictionRepository restrictions,
    IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<GetAcPackAnalyticsQuery, AcPackAnalyticsDto>
{
    public async Task<AcPackAnalyticsDto> Handle(GetAcPackAnalyticsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var pack = await packs.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("AC pack", query.Id);
        await GetAcPackQueryHandler.EnsureCanReadAsync(pack, userId, permissions, cancellationToken);

        var composition = AcMappings.ParseComposition(pack.ContentSnapshotJson)
            ?? throw new DomainException("ac_pack.not_completed", "The AC pack has no content snapshot yet.");

        var allowLists = await AcVisibility.LoadExceptionAllowListsAsync(restrictions, cancellationToken);
        return composition.ToAnalyticsDto(userId, allowLists);
    }
}

// ---- Download an artefact (verify-on-read → Critical mismatch alert → 500) ----

public sealed record DownloadAcPackArtefactQuery(Guid Id, string Format) : IQuery<AcPackArtefactResult>;

public sealed class DownloadAcPackArtefactQueryHandler(
    IAcPackRepository packs, IFindingVisibilityRestrictionRepository restrictions,
    Abstractions.Ac.IAcPackRenderer renderer, IPermissionResolver permissions, ICurrentUser currentUser,
    IFileStorage storage, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : IQueryHandler<DownloadAcPackArtefactQuery, AcPackArtefactResult>
{
    public async Task<AcPackArtefactResult> Handle(DownloadAcPackArtefactQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var pack = await packs.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("AC pack", query.Id);
        await GetAcPackQueryHandler.EnsureCanReadAsync(pack, userId, permissions, cancellationToken);

        var format = (query.Format ?? string.Empty).Trim().ToLowerInvariant();
        var filename = $"ac-pack-v{pack.VersionNumber}.{(format == "docx" ? "docx" : "html")}";

        // The CIA reads the canonical, sealed blob (with verify-on-read integrity). Any other AC member gets a
        // per-requester REDACTED re-render from the immutable snapshot, so restricted-finding titles they are not
        // allow-listed for never reach the downloadable artefact (the stored blob carries the full list). FR-M13-009.
        var isCia = await permissions.HasPermissionAsync(userId, PermissionKeys.Cia, cancellationToken: cancellationToken);
        if (!isCia)
        {
            var composition = AcMappings.ParseComposition(pack.ContentSnapshotJson)
                ?? throw new NotFoundException("AC pack artefact", $"{query.Id}/{format}");
            if (!renderer.CanRender(format))
            {
                throw new NotFoundException("AC pack artefact", $"{query.Id}/{format}");
            }

            var allowLists = await AcVisibility.LoadExceptionAllowListsAsync(restrictions, cancellationToken);
            var redacted = AcVisibility.RedactForRequester(composition, userId, allowLists);
            var rendered = renderer.Render(format, new Abstractions.Ac.AcPackRenderContext(redacted, pack.CiaSupplementaryText));
            return new AcPackArtefactResult(rendered.Content, rendered.ContentType, filename, rendered.Sha256Hash);
        }

        var artefacts = AcMappings.ParseArtefacts(pack.ProducedArtefactsJson);
        var artefact = artefacts.FirstOrDefault(a => string.Equals(a.Format, format, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("AC pack artefact", $"{query.Id}/{format}");

        var content = await storage.ReadAsync(artefact.FileKey, cancellationToken);

        // Verify-on-read: recompute SHA-256 over the stored bytes; on a mismatch raise the Critical
        // ac_pack_hash_mismatch alert (M10 + trail) then fail the read with a 500 (mirrors ReportsController).
        var recomputed = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!string.Equals(recomputed, artefact.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            pack.RaiseHashMismatch(artefact.Sha256, recomputed, userId);
            audit.Record(AuditEventTypes.AcPackHashMismatch, AuditTargetTypes.AcPack, pack.Id,
                payload: new { expected = artefact.Sha256, actual = recomputed, format });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AcPackIntegrityException();
        }

        return new AcPackArtefactResult(content, artefact.ContentType, filename, artefact.Sha256);
    }
}

// ---- Distribution log (offset; ViewACPacks) ----

public sealed record ListAcPackDistributionsQuery(Guid Id, int? Page, int? PageSize) : IQuery<PagedResult<AcPackDistributionDto>>;

public sealed class ListAcPackDistributionsQueryHandler(
    IAcPackRepository packs, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListAcPackDistributionsQuery, PagedResult<AcPackDistributionDto>>
{
    public async Task<PagedResult<AcPackDistributionDto>> Handle(ListAcPackDistributionsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var pack = await packs.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("AC pack", query.Id);
        await GetAcPackQueryHandler.EnsureCanReadAsync(pack, userId, permissions, cancellationToken);

        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await packs.ListDistributionsAsync(query.Id, page, cancellationToken);
        return result.Map(d => d.ToDto());
    }
}
