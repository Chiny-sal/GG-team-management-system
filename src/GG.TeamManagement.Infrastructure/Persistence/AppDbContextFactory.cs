using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GG.TeamManagement.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveApiBasePath())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ConfigurationHelper.GetSupabaseConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSupabaseNpgsql(connectionString);
        var options = optionsBuilder.Options;

        return new AppDbContext(options);
    }

    private static string ResolveApiBasePath()
    {
        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            cwd,
            Path.Combine(cwd, "src", "GG.TeamManagement.Api"),
            Path.GetFullPath(Path.Combine(cwd, "..", "GG.TeamManagement.Api")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GG.TeamManagement.Api"))
        };

        return candidates.FirstOrDefault(dir => File.Exists(Path.Combine(dir, "appsettings.json")))
               ?? cwd;
    }
}
