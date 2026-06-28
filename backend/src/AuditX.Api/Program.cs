using System.Threading.RateLimiting;
using AuditX.Api.Authentication;
using AuditX.Api.BackgroundJobs;
using AuditX.Api.Identity;
using AuditX.Api.Middleware;
using AuditX.Api.OpenApi;
using AuditX.Api.Startup;
using AuditX.Application;
using AuditX.Application.Abstractions;
using AuditX.Infrastructure;
using AuditX.Infrastructure.Options;
using AuditX.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.AddControllers();

// Model-binding / malformed-body failures return the SAME RFC 7807 problem+json shape the exception middleware
// produces (request_id + error_code + field_errors), so the API's error contract is uniform end-to-end.
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var fieldErrors = context.ModelState
        .Where(kv => kv.Value is { Errors.Count: > 0 })
        .ToDictionary(
            kv => kv.Key,
            kv => kv.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());

    var problem = new ProblemDetails
    {
        Title = "Validation failed",
        Status = StatusCodes.Status422UnprocessableEntity,
        Detail = "One or more fields are invalid.",
    };
    problem.Extensions["request_id"] = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    problem.Extensions["error_code"] = "validation_failed";
    problem.Extensions["field_errors"] = fieldErrors;
    return new ObjectResult(problem) { StatusCode = StatusCodes.Status422UnprocessableEntity, ContentTypes = { "application/problem+json" } };
});

builder.Services.AddAuditXOpenApi();
builder.Services.AddHttpContextAccessor();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

builder.Services.AddApiAuthentication(builder.Configuration);

// Persist + name the DataProtection key ring so DataProtection-encrypted integration credentials (M14) survive
// restarts and decrypt across instances. On Windows Server the default already persists to the profile/registry;
// in containers / multi-instance set DataProtection:KeyRingPath to a shared, backed-up location (see the runbook).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("AuditX");
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
}

// Rate limiting (anti-brute-force on auth + a global per-IP DoS backstop). Applied to the pipeline only outside
// Development so the integration suite (many rapid logins from one host) is unaffected.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 10;        // 10 sign-in attempts / minute / client IP
        o.QueueLimit = 0;
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromSeconds(1), PermitLimit = 50, QueueLimit = 0 }));
});

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
builder.Services.AddScoped<AcPackGenerationJob>();
builder.Services.AddScoped<AcPackRecurringGenerationJob>();
builder.Services.AddScoped<AuditX.Application.Abstractions.Reports.IReportGenerationQueue, HangfireReportGenerationQueue>();
builder.Services.AddScoped<AuditX.Application.Abstractions.Ac.IAcPackGenerationQueue, HangfireAcPackGenerationQueue>();

var app = builder.Build();

// Fail fast if a non-Development environment would boot with an insecure configuration (dev identity provider,
// placeholder/short JWT signing key, or no connection string).
ProductionSafetyGuard.Validate(app.Services, app.Environment, app.Configuration);

// Transport + browser hardening outside Development (HTTPS/HSTS terminate at the reverse proxy in most deployments,
// but enforce here too as defence-in-depth). Security-response headers are always applied.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();

// Rate limiting only outside Development (keeps the integration suite's rapid logins unthrottled).
if (!app.Environment.IsDevelopment())
{
    app.UseRateLimiter();
}

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

// Quarterly auto-generation of the audit-committee pack (M13). Runs at 02:00 on the 1st of Jan/Apr/Jul/Oct.
recurringJobs.AddOrUpdate<AcPackRecurringGenerationJob>(
    AcPackRecurringGenerationJob.RecurringJobId,
    job => job.RunAsync(CancellationToken.None),
    "0 2 1 1,4,7,10 *");

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
