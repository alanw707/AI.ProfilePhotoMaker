using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AI.ProfilePhotoMaker.API.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext to support Entity Framework tooling
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Use SQL Server connection string for design-time operations
        var connectionString = "Server=tcp:sql-apm-1754278427.database.windows.net,1433;Initial Catalog=aiprofilemaker;User ID=sqladmin;Password=TempPassword123!;Encrypt=True;";
        
        optionsBuilder.UseSqlServer(connectionString);
        
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}