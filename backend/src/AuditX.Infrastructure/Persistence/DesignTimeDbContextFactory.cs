using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuditX.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations) so they do not need to boot the web host.
/// The connection string is only used for provider selection during scaffolding, not for a live
/// connection; it can be overridden with the <c>AUDITX_DB</c> environment variable.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AUDITX_DB")
            ?? "Server=localhost,1433;Database=auditx;User Id=sa;Password=Auditx_Local_Dev_123;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
