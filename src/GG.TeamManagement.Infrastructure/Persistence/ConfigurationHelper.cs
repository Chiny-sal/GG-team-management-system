using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GG.TeamManagement.Infrastructure.Persistence;

public static class ConfigurationHelper
{
    public const int CommandTimeoutSeconds = 60;

    public static string GetSupabaseConnectionString(IConfiguration configuration)
    {
        AppEnvironment.EnsureRequired(configuration, AppEnvironment.SupabaseConnectionString);
        return WithCommandTimeout(AppEnvironment.Require(configuration, AppEnvironment.SupabaseConnectionString));
    }

    public static void UseSupabaseNpgsql(this DbContextOptionsBuilder options, string connectionString)
    {
        var connection = WithCommandTimeout(connectionString);
        options.UseNpgsql(connection, npgsql =>
        {
            npgsql.CommandTimeout(CommandTimeoutSeconds);
            npgsql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        });
    }

    /// <summary>
    /// Pooler first-query latency can exceed Npgsql's 30s default. Put Command Timeout
    /// on the connection string so EF, Hangfire, and any other Npgsql client share it.
    /// </summary>
    public static string WithCommandTimeout(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.CommandTimeout < CommandTimeoutSeconds)
            builder.CommandTimeout = CommandTimeoutSeconds;
        return builder.ConnectionString;
    }
}
