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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IActivityFeedNotifier, ActivityFeedNotifier>();
builder.Services.AddHostedService<TelegramWebhookSetupHostedService>();

builder.Services.AddControllers(options =>
    {
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.Filters.Add(new AuthorizeFilter(policy));
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var jwtKey = builder.Configuration["Jwt:Key"] ?? builder.Configuration["JWT_KEY"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Contains("<<", StringComparison.Ordinal))
    throw new InvalidOperationException("JWT_KEY / Jwt:Key is not configured.");

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
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "GG.TeamManagement",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "GG.TeamManagement",
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

var frontendOrigin = builder.Configuration["FRONTEND_ORIGIN"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireLeadDashboardFilter()]
});

app.MapControllers();
app.MapHub<ActivityFeedHub>("/hubs/activity-feed");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
    await DatabaseSeeder.SeedAsync(db, userManager, roleManager, app.Configuration, logger);

    HangfireJobRegistrar.RegisterRecurringJobs();
}

app.Run();

internal sealed class HangfireLeadDashboardFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var http = ((Hangfire.Dashboard.AspNetCoreDashboardContext)context).HttpContext;
        return http.User.Identity?.IsAuthenticated == true && http.User.IsInRole("Lead");
    }
}
