using System.IO;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AudioProcessor.Infrastructure.Persistence
{
    /// <summary>
    /// Design-time factory used exclusively by EF Core CLI tools (migrations, db update).
    /// Loads the .env file from /infrastructure to resolve the connection string.
    /// </summary>
    public class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            string envPath = ResolveEnvFilePath();
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
            }

            string connectionString = System.Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                ?? throw new System.InvalidOperationException(
                    "DB_CONNECTION_STRING is not set. Ensure infrastructure/.env exists with a valid connection string.");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }

        private static string ResolveEnvFilePath()
        {
            // Walk up from the Infrastructure project directory to find infrastructure/.env
            string currentDir = Directory.GetCurrentDirectory();

            // When invoked by EF CLI, cwd is typically the Infrastructure project directory
            // Go up to workspace root (odeoia) and then into infrastructure/
            DirectoryInfo? dir = new DirectoryInfo(currentDir);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "infrastructure", ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }

            return Path.Combine(currentDir, "infrastructure", ".env");
        }
    }
}
