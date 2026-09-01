using Microsoft.Extensions.Configuration;

namespace GG.TeamManagement.Infrastructure.Persistence;

/// <summary>
/// Single source of truth for environment variables. Every secret and connection
/// value is a flat name (e.g. JWT_KEY), never a nested key (e.g. Jwt:Key).
/// </summary>
public static class AppEnvironment
{
    public const string SupabaseConnectionString = "SUPABASE_CONNECTION_STRING";
    public const string JwtKey = "JWT_KEY";
    public const string TelegramBotToken = "TELEGRAM_BOT_TOKEN";
    public const string PublicApiUrl = "PUBLIC_API_URL";
    public const string SeedDefaultPassword = "SEED_DEFAULT_PASSWORD";
    public const string TelegramWebhookSecret = "TELEGRAM_WEBHOOK_SECRET";
    public const string FrontendOrigin = "FRONTEND_ORIGIN";
    public const string ApiUrl = "API_URL";
    public const string JwtIssuer = "JWT_ISSUER";
    public const string JwtAudience = "JWT_AUDIENCE";
    public const string JwtExpiryMinutes = "JWT_EXPIRY_MINUTES";

    public static readonly string[] RequiredForRuntime =
    [
        SupabaseConnectionString,
        JwtKey
    ];

    public const string DefaultApiUrl = "http://localhost:7223";

    public static string GetApiUrl(IConfiguration configuration) =>
        Optional(configuration, ApiUrl) ?? DefaultApiUrl;

    public static string[] GetFrontendOrigins(IConfiguration configuration)
    {
        var origins = new List<string> { "http://localhost:3000", "http://127.0.0.1:3000" };
        var configured = Optional(configuration, FrontendOrigin);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            origins.AddRange(configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return origins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static void EnsureRequired(IConfiguration configuration, params string[] names)
    {
        var missing = names
            .Where(name => Read(configuration, name) is null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (missing.Count == 0) return;

        throw new InvalidOperationException(
            "Missing required environment variable(s): "
            + string.Join(", ", missing)
            + ". Set each as a flat environment variable with that exact name (not a nested appsettings key).");
    }

    public static string Require(IConfiguration configuration, string name) =>
        Read(configuration, name)
        ?? throw new InvalidOperationException(
            $"Missing required environment variable: {name}. Set it as a flat environment variable with that exact name.");

    public static string? Optional(IConfiguration configuration, string name) =>
        Read(configuration, name);

    public static string? Read(IConfiguration configuration, string name)
    {
        foreach (var candidate in new[]
                 {
                     Environment.GetEnvironmentVariable(name),
                     configuration[name]
                 })
        {
            if (!string.IsNullOrWhiteSpace(candidate)
                && !candidate.Contains("<<", StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
}
