using AuditX.Api.Authentication;
using AuditX.Api.BackgroundJobs;
using AuditX.Api.Identity;
using AuditX.Api.Middleware;
using AuditX.Application;
using AuditX.Application.Abstractions;
using AuditX.Infrastructure;
using AuditX.Infrastructure.Options;
using AuditX.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

builder.Services.AddApiAuthentication(builder.Configuration);

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (corsOrigins.Length > 0)
    {
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<DelegationExpiryJob>();
builder.Services.AddScoped<WebhookRetryJob>();
builder.Services.AddScoped<AuditAutoStartJob>();
builder.Services.AddScoped<NotificationRetryJob>();
builder.Services.AddScoped<ReportGenerationJob>();
builder.Services.AddScoped<RecurrenceClusterScanJob>();
builder.Services.AddScoped<AuditX.Application.Abstractions.Reports.IReportGenerationQueue, HangfireReportGenerationQueue>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<SessionSlidingMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Ensure the database + schema exist BEFORE registering recurring jobs: Hangfire's SQL storage connects
// (and provisions its own schema) the moment a job is registered, so the database must already be there.
await InitialiseDatabaseAsync(app);

// Register recurring jobs through the DI-resolved manager (not the static RecurringJob facade, which
// depends on JobStorage.Current and is not initialised under the test host / before the server starts).
var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();

// Hourly delegation expiry (US-M1-028).
recurringJobs.AddOrUpdate<DelegationExpiryJob>(
    DelegationExpiryJob.RecurringJobId,
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly);

// Webhook retry sweep every 5 minutes (US-M14-010).
recurringJobs.AddOrUpdate<WebhookRetryJob>(
    WebhookRetryJob.RecurringJobId,
    job => job.RunAsync(CancellationToken.None),
    "*/5 * * * *");

// Hourly auto-start of planned audits whose start date has arrived (US-M4-011).
recurringJobs.AddOrUpdate<AuditAutoStartJob>(
    AuditAutoStartJob.RecurringJobId,
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly);

// Notification retry sweep every minute (US-M10-005).
recurringJobs.AddOrUpdate<NotificationRetryJob>(
    NotificationRetryJob.RecurringJobId,
    job => job.RunAsync(CancellationToken.None),
    "* * * * *");

// Daily exception-recurrence detection scan (US-M9 G6).
recurringJobs.AddOrUpdate<RecurrenceClusterScanJob>(
    RecurrenceClusterScanJob.RecurringJobId,
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily);

app.Run();
return;

static async Task InitialiseDatabaseAsync(WebApplication app)
{
    // Apply migrations and seed on startup in development; production applies migrations through the
    // controlled release process unless Database:MigrateOnStartup is explicitly enabled.
    if (!app.Environment.IsDevelopment() && !app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var identityOptions = scope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value;
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync(seedDevelopmentUsers: identityOptions.UseDevelopmentProvider);
}

/// <summary>Exposed so the integration-test host (WebApplicationFactory) can reference the API assembly.</summary>
public partial class Program;
