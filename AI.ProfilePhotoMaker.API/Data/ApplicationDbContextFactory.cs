using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AI.ProfilePhotoMaker.API.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext to support Entity Framework tooling
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Build configuration to read from appsettings files
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Get connection string from configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        // Determine database provider based on connection string
        if (IsAzureSqlServer(connectionString))
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else
        {
            // Default to SQLite for development
            optionsBuilder.UseSqlite(connectionString ?? "Data Source=aiprofilemaker.db");
        }
        
        return new ApplicationDbContext(optionsBuilder.Options);
    }
    
    private static bool IsAzureSqlServer(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return false;
            
        return connectionString.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Server=tcp:", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Authentication=Active Directory", StringComparison.OrdinalIgnoreCase);
    }
}