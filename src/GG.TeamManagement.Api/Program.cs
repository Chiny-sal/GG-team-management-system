using System.Text;
using System.Text.Json.Serialization;
using GG.TeamManagement.Api.Hubs;
using GG.TeamManagement.Api.Middleware;
using GG.TeamManagement.Api.Realtime;
using GG.TeamManagement.Api.Telegram;
using GG.TeamManagement.Application;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Infrastructure;
using GG.TeamManagement.Infrastructure.Identity;
using GG.TeamManagement.Infrastructure.Jobs;
using GG.TeamManagement.Infrastructure.Persistence;
using GG.TeamManagement.Infrastructure.Persistence.Seed;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
// CreateBuilder already loads environment variables after appsettings; add them again
// so shell-set values (SUPABASE_CONNECTION_STRING, JWT_KEY, …) always win over JSON placeholders.
builder.Configuration.AddEnvironmentVariables();
AppEnvironment.EnsureRequired(builder.Configuration, AppEnvironment.RequiredForRuntime);

var apiUrl = AppEnvironment.GetApiUrl(builder.Configuration);
// API_URL is the only Kestrel listen address. Do not also set applicationUrl / ASPNETCORE_URLS.
builder.WebHost.UseUrls(apiUrl);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IActivityFeedNotifier, ActivityFeedNotifier>();

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

builder.Services.AddAuthorization();

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

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireLeadDashboardFilter()]
});

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

var startupLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
app.Lifetime.ApplicationStarted.Register(() =>
{
    var displayed = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : apiUrl;
    startupLog.LogInformation("API listening on: {Urls}", displayed);
});

await app.StartAsync();

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

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var seedLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
    await DatabaseSeeder.SeedAsync(db, userManager, roleManager, app.Configuration, seedLog);

    HangfireJobRegistrar.RegisterRecurringJobs();
    startupLog.LogInformation("Hangfire: running");

    var telegramStatus = await TelegramWebhookSetup.TryRegisterAsync(app.Configuration, startupLog);
    startupLog.LogInformation("Telegram webhook: {Status}", telegramStatus);
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
