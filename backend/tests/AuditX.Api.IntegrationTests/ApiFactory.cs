using AuditX.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Boots the real API against SQL Server and Redis test containers, using the Development identity
/// provider (seeded users) so the full auth/authorisation stack is exercised end-to-end. Requires Docker.
///
/// Fieldwork normally starts only once the whole audit team has declared independence (the institution setting
/// defaults on). Most flows here are about something else and start fieldwork without that step, so this factory turns
/// the setting off once the database is seeded; <see cref="StrictIndependenceApiFactory"/> keeps it on for the tests of
/// the independence gate itself.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly IContainer _redis = new ContainerBuilder("redis:7-alpine")
        .WithPortBinding(6379, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
        .Build();

    /// <summary>Whether fieldwork waits for the team's independence declarations in this factory's database.</summary>
    protected virtual bool RequireIndependenceDeclaration => false;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        await _redis.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
        await _redis.DisposeAsync();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _sql.GetConnectionString(),
            ["Redis:ConnectionString"] = $"localhost:{_redis.GetMappedPublicPort(6379)}",
            ["Identity:Provider"] = "Development",
            ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes-0123456789",
            ["Database:MigrateOnStartup"] = "true",
        }));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Starting the host migrates and seeds the database; the policy is set on the seeded settings row after that.
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = db.InstitutionSettings.Single();
        settings.SetRequireIndependenceDeclaration(RequireIndependenceDeclaration);
        db.SaveChanges();
        return host;
    }
}

/// <summary>An <see cref="ApiFactory"/> whose institution requires independence declarations before fieldwork (the default).</summary>
public sealed class StrictIndependenceApiFactory : ApiFactory
{
    protected override bool RequireIndependenceDeclaration => true;
}
