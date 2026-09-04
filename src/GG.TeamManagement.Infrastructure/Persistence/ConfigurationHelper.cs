using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GG.TeamManagement.Infrastructure.Persistence;

public static class ConfigurationHelper
{
    public const int CommandTimeoutSeconds = 20;
    public const int ConnectionTimeoutSeconds = 15;
    public const int MaxPoolSize = 8;

    public static string GetSupabaseConnectionString(IConfiguration configuration)
    {
        AppEnvironment.EnsureRequired(configuration, AppEnvironment.SupabaseConnectionString);
        return ApplyPoolerSettings(AppEnvironment.Require(configuration, AppEnvironment.SupabaseConnectionString));
    }

    public static void UseSupabaseNpgsql(this DbContextOptionsBuilder options, string connectionString)
    {
        var connection = ApplyPoolerSettings(connectionString);
        options.UseNpgsql(connection, npgsql =>
        {
            npgsql.CommandTimeout(CommandTimeoutSeconds);
            // Do not EnableRetryOnFailure here. Identity and Hangfire open their own
            // transactions (incompatible with EF's retry strategy), and retries turn a
            // pooler stall into minutes of a pending /api/auth/login request.
        });
    }

    /// <summary>
    /// Supabase transaction pooler (port 6543 / PgBouncer) does not support prepared
    /// statements or connection reset. Without these settings Npgsql queries hang until
    /// Command Timeout while a simple connectivity check still succeeds.
    /// </summary>
    public static string ApplyPoolerSettings(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            CommandTimeout = CommandTimeoutSeconds,
            Timeout = ConnectionTimeoutSeconds,
            MaxAutoPrepare = 0,
            NoResetOnClose = true,
            Multiplexing = false,
            MaxPoolSize = MaxPoolSize,
            SslMode = SslMode.Require
        };
        return builder.ConnectionString;
    }

    public static string WithCommandTimeout(string connectionString) => ApplyPoolerSettings(connectionString);
}
