using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Administration;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Administration.Dtos;
using AuditX.Application.Administration.Mapping;
using AuditX.Application.Common.Messaging;

namespace AuditX.Application.Administration.Queries;

public sealed record GetBankSettingsQuery : IQuery<BankSettingsDto>;

public sealed class GetBankSettingsQueryHandler(IBankSettingsRepository settings)
    : IQueryHandler<GetBankSettingsQuery, BankSettingsDto>
{
    public async Task<BankSettingsDto> Handle(GetBankSettingsQuery query, CancellationToken cancellationToken)
        => (await settings.GetAsync(cancellationToken)).ToDto();
}

/// <summary>Public branding (name, colours, logo/icon) — served anonymously so the shell can theme pre-auth.</summary>
public sealed record GetBrandingQuery : IQuery<BrandingDto>;

public sealed class GetBrandingQueryHandler(IBankSettingsRepository settings)
    : IQueryHandler<GetBrandingQuery, BrandingDto>
{
    public async Task<BrandingDto> Handle(GetBrandingQuery query, CancellationToken cancellationToken)
    {
        var bank = await settings.GetAsync(cancellationToken);
        return new BrandingDto(
            bank.BankDisplayName, bank.PrimaryColor, bank.AccentColor, bank.LogoDataUri, bank.IconDataUri,
            bank.ShowOverview, bank.ShowWalkthrough);
    }
}

public sealed record GetSupportChannelStatusQuery : IQuery<SupportChannelStatusDto>;

public sealed class GetSupportChannelStatusQueryHandler(IAdministrationRepository admin, IClock clock)
    : IQueryHandler<GetSupportChannelStatusQuery, SupportChannelStatusDto>
{
    public async Task<SupportChannelStatusDto> Handle(GetSupportChannelStatusQuery query, CancellationToken cancellationToken)
        => (await admin.GetLatestSupportSessionAsync(cancellationToken)).ToStatusDto(clock.UtcNow);
}

public sealed record ListReleasesQuery : IQuery<IReadOnlyList<ReleaseInstallDto>>;

public sealed class ListReleasesQueryHandler(IAdministrationRepository admin)
    : IQueryHandler<ListReleasesQuery, IReadOnlyList<ReleaseInstallDto>>
{
    public async Task<IReadOnlyList<ReleaseInstallDto>> Handle(ListReleasesQuery query, CancellationToken cancellationToken)
        => (await admin.GetReleasesAsync(50, cancellationToken)).Select(r => r.ToDto()).ToArray();
}

public sealed record ListRestoreDrillsQuery : IQuery<IReadOnlyList<RestoreDrillDto>>;

public sealed class ListRestoreDrillsQueryHandler(IAdministrationRepository admin)
    : IQueryHandler<ListRestoreDrillsQuery, IReadOnlyList<RestoreDrillDto>>
{
    public async Task<IReadOnlyList<RestoreDrillDto>> Handle(ListRestoreDrillsQuery query, CancellationToken cancellationToken)
        => (await admin.GetRestoreDrillsAsync(50, cancellationToken)).Select(d => d.ToDto()).ToArray();
}

public sealed record GetSystemHealthQuery : IQuery<SystemHealthDto>;

public sealed class GetSystemHealthQueryHandler(ISystemMetricsProvider metrics, IClock clock)
    : IQueryHandler<GetSystemHealthQuery, SystemHealthDto>
{
    public async Task<SystemHealthDto> Handle(GetSystemHealthQuery query, CancellationToken cancellationToken)
    {
        var m = await metrics.GetAsync(cancellationToken);
        return new SystemHealthDto("healthy", m.ActiveUserCount, m.TotalUserCount, m.TemplateCount, m.IntegrationCount, clock.UtcNow);
    }
}
