using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Boots the real API against SQL Server and Redis test containers, using the Development identity
/// provider (seeded users) so the full auth/authorisation stack is exercised end-to-end. Requires Docker.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly IContainer _redis = new ContainerBuilder("redis:7-alpine")
        .WithPortBinding(6379, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
        .Build();

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
}
