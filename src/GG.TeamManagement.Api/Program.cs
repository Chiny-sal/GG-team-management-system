using System.Text;
using System.Text.Json.Serialization;
using GG.TeamManagement.Api.Hubs;
using GG.TeamManagement.Api.Middleware;
using GG.TeamManagement.Api.Realtime;
using GG.TeamManagement.Api.Security;
using GG.TeamManagement.Api.Telegram;
using GG.TeamManagement.Application;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Infrastructure;
using GG.TeamManagement.Infrastructure.Identity;
using GG.TeamManagement.Infrastructure.Jobs;
using GG.TeamManagement.Infrastructure.Persistence;
using GG.TeamManagement.Infrastructure.Persistence.Seed;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    Console.Error.WriteLine($"[Startup] UnhandledException (IsTerminating={args.IsTerminating}): {args.ExceptionObject}");
    Console.Error.Flush();
};
TaskScheduler.UnobservedTaskException += (_, args) =>
{
    Console.Error.WriteLine($"[Startup] UnobservedTaskException: {args.Exception}");
    Console.Error.Flush();
};

var builder = WebApplication.CreateBuilder(args);
// CreateBuilder already loads environment variables after appsettings; add them again
// so shell-set values (SUPABASE_CONNECTION_STRING, JWT_KEY, …) always win over JSON placeholders.
builder.Configuration.AddEnvironmentVariables();
AppEnvironment.EnsureRequired(builder.Configuration, AppEnvironment.RequiredForRuntime);

var apiUrl = AppEnvironment.GetApiUrl(builder.Configuration);
// PORT (Render) wins, then API_URL. Do not also set applicationUrl / ASPNETCORE_URLS.
builder.WebHost.UseUrls(apiUrl);

builder.Services.AddDataProtection()
    .SetApplicationName("GG.TeamManagement")
    .AddKeyManagementOptions(options =>
    {
        options.XmlRepository = new InMemoryXmlRepository();
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IActivityFeedNotifier, ActivityFeedNotifier>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramBotNotifier>();

builder.Services.AddControllers(options =>
    {
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.Filters.Add(new AuthorizeFilter(policy));
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var jwtKey = AppEnvironment.Require(builder.Configuration, AppEnvironment.JwtKey);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = AppEnvironment.Optional(builder.Configuration, AppEnvironment.JwtIssuer) ?? "GG.TeamManagement",
            ValidAudience = AppEnvironment.Optional(builder.Configuration, AppEnvironment.JwtAudience) ?? "GG.TeamManagement",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.LeadOrOfficeManagement, policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Lead")
            || string.Equals(
                context.User.FindFirst(AuthClaims.IsOfficeManagement)?.Value,
                "true",
                StringComparison.OrdinalIgnoreCase)));
});

var frontendOrigins = AppEnvironment.GetFrontendOrigins(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();
var startupLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

void StartupStep(string message)
{
    startupLog.LogInformation("{Message}", message);
    Console.WriteLine($"[Startup] {message}");
    Console.Out.Flush();
}

void StartupFail(Exception ex, string message)
{
    startupLog.LogCritical(ex, "{Message}", message);
    Console.Error.WriteLine($"[Startup] {message}: {ex}");
    Console.Error.Flush();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

try
{
    StartupStep("Hangfire: registering dashboard at /hangfire");
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireLeadDashboardFilter()]
    });
    StartupStep("Hangfire: dashboard registered");
}
catch (Exception ex)
{
    StartupFail(ex, "Hangfire: dashboard registration failed");
    throw;
}

app.MapControllers();
app.MapHub<ActivityFeedHub>("/hubs/activity-feed");
app.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var connected = await db.Database.CanConnectAsync(cancellationToken);
    var payload = new
    {
        status = connected ? "Healthy" : "Unhealthy",
        database = connected ? "connected" : "disconnected"
    };
    return connected
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var displayed = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : apiUrl;
    startupLog.LogInformation("API listening on: {Urls}", displayed);
});

// Seed before Kestrel/Hangfire start so /api/auth/login cannot run against an empty
// Identity store or compete with Hangfire for pooler connections.
using (var scope = app.Services.CreateScope())
{
    // Do not call Database.MigrateAsync() here. Supabase's transaction pooler (port 6543)
    // does not reliably support the migration-history lock/check EF runs during Migrate().
    // Apply schema only with: dotnet ef database update (use the session pooler, port 5432).
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!await db.Database.CanConnectAsync())
    {
        startupLog.LogError("Database: disconnected");
        throw new InvalidOperationException(
            "Database: disconnected. Check SUPABASE_CONNECTION_STRING (runtime uses the transaction pooler, port 6543).");
    }

    startupLog.LogInformation("Database: connected");
    Console.WriteLine("[Startup] Database: connected");
    Console.Out.Flush();

    try
    {
        StartupStep("Database: seeding Identity and reference data");
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var seedLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
        await DatabaseSeeder.SeedAsync(db, userManager, roleManager, app.Configuration, seedLog);
        StartupStep("Database: seed complete");
    }
    catch (Exception ex)
    {
        StartupFail(ex, "Database: seed failed");
        throw;
    }
}

try
{
    StartupStep("Data Protection: initializing in-memory key ring");
    var protector = app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("startup-probe");
    _ = protector.Unprotect(protector.Protect("ok"));
    StartupStep("Data Protection: ready (in-memory, not persisted to disk)");
}
catch (Exception ex)
{
    StartupFail(ex, "Data Protection: failed to initialize");
    throw;
}

try
{
    StartupStep("Host: starting Kestrel and Hangfire server");
    await app.StartAsync();
    StartupStep("Host: started");
}
catch (Exception ex)
{
    StartupFail(ex, "Host: StartAsync failed (Kestrel and/or Hangfire server)");
    throw;
}

try
{
    HangfireJobRegistrar.RegisterRecurringJobs(startupLog);
    StartupStep("Hangfire: running");
}
catch (Exception ex)
{
    StartupFail(ex, "Hangfire: recurring job registration failed");
    throw;
}

try
{
    StartupStep("Telegram: registering webhook");
    var telegramStatus = await TelegramWebhookSetup.TryRegisterAsync(app.Configuration, startupLog);
    startupLog.LogInformation("Telegram webhook: {Status}", telegramStatus);
    Console.WriteLine($"[Startup] Telegram webhook: {telegramStatus}");
    Console.Out.Flush();
}
catch (Exception ex)
{
    StartupFail(ex, "Telegram: webhook registration threw");
    throw;
}

await app.WaitForShutdownAsync();

internal sealed class HangfireLeadDashboardFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var http = ((Hangfire.Dashboard.AspNetCoreDashboardContext)context).HttpContext;
        return http.User.Identity?.IsAuthenticated == true && http.User.IsInRole("Lead");
    }
}
