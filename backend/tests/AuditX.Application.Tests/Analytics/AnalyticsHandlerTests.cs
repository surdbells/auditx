using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Analytics.Queries;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Analytics;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using NSubstitute;

namespace AuditX.Application.Tests.Analytics;

public sealed class PerformanceScorecardsQueryHandlerTests
{
    private static PerformanceScorecardDto Card(Guid lead) => new(lead, 5, 4, 12.0, 9, 7, 8.0);

    private static (PerformanceScorecardsQueryHandler handler, IPermissionResolver perms) Build(
        Guid caller, bool hasView, bool hasCia, IReadOnlyList<PerformanceScorecardDto> data)
    {
        var analytics = Substitute.For<IAnalyticsQueryService>();
        analytics.PerformanceScorecardsAsync(Arg.Any<CancellationToken>()).Returns(data);

        var perms = Substitute.For<IPermissionResolver>();
        perms.HasPermissionAsync(caller, PermissionKeys.PerformanceAnalyticsView, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(hasView);
        perms.HasPermissionAsync(caller, PermissionKeys.Cia, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(hasCia);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(caller);

        return (new PerformanceScorecardsQueryHandler(analytics, perms, currentUser), perms);
    }

    [Fact]
    public async Task Without_the_view_permission_it_is_forbidden()
    {
        var caller = Guid.NewGuid();
        var (handler, _) = Build(caller, hasView: false, hasCia: false, [Card(caller)]);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new PerformanceScorecardsQuery(), default));
    }

    [Fact]
    public async Task A_viewer_without_cia_cannot_see_their_own_scorecard_but_sees_peers()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        var (handler, _) = Build(caller, hasView: true, hasCia: false, [Card(caller), Card(peer)]);

        var result = await handler.Handle(new PerformanceScorecardsQuery(), default);

        Assert.DoesNotContain(result, c => c.AuditLeadUserId == caller);
        Assert.Contains(result, c => c.AuditLeadUserId == peer);
    }

    [Fact]
    public async Task A_cia_viewer_sees_every_scorecard_including_their_own()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        var (handler, _) = Build(caller, hasView: true, hasCia: true, [Card(caller), Card(peer)]);

        var result = await handler.Handle(new PerformanceScorecardsQuery(), default);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.AuditLeadUserId == caller);
    }
}

public sealed class ListDashboardsQueryHandlerTests
{
    private static Dashboard Gated(string slug, string? permission) =>
        Dashboard.Create(slug, slug, null, permission, isSystemDefault: true);

    [Fact]
    public async Task Permission_gated_dashboards_are_hidden_unless_the_caller_holds_the_gate()
    {
        var caller = Guid.NewGuid();

        var dashboards = Substitute.For<IDashboardRepository>();
        dashboards.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<Dashboard>
        {
            Gated("exception_portfolio", null),                 // open to everyone
            Gated("sanctions_consistency", PermissionKeys.Cia), // CIA only
        });

        var perms = Substitute.For<IPermissionResolver>();
        perms.HasPermissionAsync(caller, PermissionKeys.Cia, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(false);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(caller);

        var handler = new ListDashboardsQueryHandler(dashboards, perms, currentUser);
        var visible = await handler.Handle(new ListDashboardsQuery(), default);

        Assert.Contains(visible, d => d.Slug == "exception_portfolio");
        Assert.DoesNotContain(visible, d => d.Slug == "sanctions_consistency");
    }

    [Fact]
    public async Task A_cia_holder_sees_the_gated_dashboard()
    {
        var caller = Guid.NewGuid();

        var dashboards = Substitute.For<IDashboardRepository>();
        dashboards.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<Dashboard>
        {
            Gated("sanctions_consistency", PermissionKeys.Cia),
        });

        var perms = Substitute.For<IPermissionResolver>();
        perms.HasPermissionAsync(caller, PermissionKeys.Cia, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(true);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(caller);

        var handler = new ListDashboardsQueryHandler(dashboards, perms, currentUser);
        var visible = await handler.Handle(new ListDashboardsQuery(), default);

        Assert.Contains(visible, d => d.Slug == "sanctions_consistency");
    }
}

public sealed class AnalyticsPermissionCatalogueTests
{
    [Fact]
    public void Sensitive_query_access_is_catalogued_but_granted_to_no_built_in_role()
    {
        Assert.Contains(PermissionCatalogue.All, p => p.Key == PermissionKeys.SensitiveQueryAccess);
        Assert.DoesNotContain(BuiltInRoles.All, r => r.Permissions.Contains(PermissionKeys.SensitiveQueryAccess));
    }

    [Fact]
    public void Performance_analytics_view_is_granted_to_at_least_one_role()
    {
        // Regression for the review finding: the scorecards feature must be reachable by someone.
        Assert.Contains(BuiltInRoles.All, r => r.Permissions.Contains(PermissionKeys.PerformanceAnalyticsView));
    }
}
