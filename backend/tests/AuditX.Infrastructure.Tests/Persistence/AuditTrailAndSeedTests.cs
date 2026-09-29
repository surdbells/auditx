using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace AuditX.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the persistence layer against a real SQL Server (Testcontainers). Verifies
/// the seed and the database-enforced append-only audit trail. Requires Docker.
/// </summary>
public sealed class AuditTrailAndSeedTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync() => await _sql.StartAsync();

    public async Task DisposeAsync() => await _sql.DisposeAsync();

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_sql.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Seed_creates_the_four_builtin_roles_and_default_gates()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        await new DbSeeder(db, new AuditX.Infrastructure.Identity.Pbkdf2PasswordHasher(), NullLogger<DbSeeder>.Instance).SeedAsync(seedDevelopmentUsers: false);

        Assert.Equal(4, await db.Roles.CountAsync(r => r.IsBuiltIn));
        Assert.True(await db.MakerCheckerGates.AnyAsync(g => g.ActionType == "role_permission_change" && g.IsEnabled));
        Assert.True(await db.InstitutionSettings.AnyAsync());
    }

    [Fact]
    public async Task Audit_trail_rejects_update_and_delete()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var entry = AuditTrailEntry.Create("test_event", "test", Guid.NewGuid(), ActorType.System, null, DateTimeOffset.UtcNow);
        db.AuditTrail.Add(entry);
        await db.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE audit_trail SET event_type = 'tampered' WHERE id = {0}", entry.Id));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM audit_trail WHERE id = {0}", entry.Id));

        // The original row is intact.
        var stored = await db.AuditTrail.AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Equal("test_event", stored.EventType);
    }
}
