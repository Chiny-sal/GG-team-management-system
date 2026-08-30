using Microsoft.Extensions.Configuration;

namespace GG.TeamManagement.Infrastructure.Persistence;

public static class ConfigurationHelper
{
    public static string GetSupabaseConnectionString(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString("SUPABASE_CONNECTION_STRING")
                    ?? configuration["SUPABASE_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(value) || value.Contains("<<", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "SUPABASE_CONNECTION_STRING is not configured. Set ConnectionStrings:SUPABASE_CONNECTION_STRING or the SUPABASE_CONNECTION_STRING environment variable.");
        }

        return value;
    }
}
