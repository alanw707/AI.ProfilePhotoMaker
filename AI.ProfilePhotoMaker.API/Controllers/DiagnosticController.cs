using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AI.ProfilePhotoMaker.API.Data;

namespace AI.ProfilePhotoMaker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DiagnosticController> _logger;

        public DiagnosticController(ApplicationDbContext context, ILogger<DiagnosticController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("run-migrations")]
        public async Task<IActionResult> RunMigrations()
        {
            try
            {
                _logger.LogCritical("🚨 MANUAL MIGRATION: Starting database migration from API endpoint");
                
                // Check database connection
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogCritical("Database connection status: {CanConnect}", canConnect);
                
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                // Run migrations
                _logger.LogCritical("🚨 MANUAL MIGRATION: Executing database.Migrate()");
                await _context.Database.MigrateAsync();
                _logger.LogCritical("✅ MANUAL MIGRATION: Migrations completed successfully");

                // Verify tables exist
                var creditPackageCount = await _context.CreditPackages.CountAsync();
                var userProfileCount = await _context.UserProfiles.CountAsync();
                var styleCount = await _context.Styles.CountAsync();

                var result = new
                {
                    success = true,
                    message = "Migrations completed successfully",
                    tables = new
                    {
                        creditPackages = creditPackageCount,
                        userProfiles = userProfileCount,
                        styles = styleCount
                    }
                };

                _logger.LogCritical("Migration result: {@Result}", result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ MANUAL MIGRATION FAILED: {Message}", ex.Message);
                _logger.LogCritical("Stack trace: {StackTrace}", ex.StackTrace);
                
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("reset-database")]
        public async Task<IActionResult> ResetDatabase()
        {
            try
            {
                _logger.LogCritical("🚨 DATABASE RESET: Starting database reset (drop and recreate)");
                
                // Check database connection
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogCritical("Database connection status: {CanConnect}", canConnect);
                
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                // Drop and recreate database
                _logger.LogCritical("🚨 DATABASE RESET: Dropping existing database...");
                await _context.Database.EnsureDeletedAsync();
                _logger.LogCritical("✅ DATABASE RESET: Database dropped successfully");

                _logger.LogCritical("🚨 DATABASE RESET: Creating fresh database with all tables...");
                await _context.Database.EnsureCreatedAsync();
                _logger.LogCritical("✅ DATABASE RESET: Database created successfully");

                // Verify tables exist
                var creditPackageCount = await _context.CreditPackages.CountAsync();
                var userProfileCount = await _context.UserProfiles.CountAsync();
                var styleCount = await _context.Styles.CountAsync();

                var result = new
                {
                    success = true,
                    message = "Database reset completed successfully",
                    tables = new
                    {
                        creditPackages = creditPackageCount,
                        userProfiles = userProfileCount,
                        styles = styleCount
                    }
                };

                _logger.LogCritical("Database reset result: {@Result}", result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ DATABASE RESET FAILED: {Message}", ex.Message);
                _logger.LogCritical("Stack trace: {StackTrace}", ex.StackTrace);
                
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("create-tables-sql")]
        public async Task<IActionResult> CreateTablesWithRawSql()
        {
            try
            {
                _logger.LogCritical("🚨 RAW SQL: Creating database tables with raw SQL commands");
                
                // Check database connection
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogCritical("Database connection status: {CanConnect}", canConnect);
                
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                // Create minimal essential tables with raw SQL
                var sqlCommands = new[]
                {
                    // Essential Identity tables (simplified)
                    @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUsers' AND xtype='U')
                      CREATE TABLE AspNetUsers (
                          Id nvarchar(450) NOT NULL PRIMARY KEY,
                          UserName nvarchar(256) NULL,
                          NormalizedUserName nvarchar(256) NULL,
                          Email nvarchar(256) NULL,
                          NormalizedEmail nvarchar(256) NULL,
                          EmailConfirmed bit NOT NULL,
                          PasswordHash nvarchar(max) NULL,
                          SecurityStamp nvarchar(max) NULL,
                          ConcurrencyStamp nvarchar(max) NULL,
                          PhoneNumber nvarchar(max) NULL,
                          PhoneNumberConfirmed bit NOT NULL,
                          TwoFactorEnabled bit NOT NULL,
                          LockoutEnd datetimeoffset(7) NULL,
                          LockoutEnabled bit NOT NULL,
                          AccessFailedCount int NOT NULL,
                          FirstName nvarchar(max) NOT NULL DEFAULT '',
                          LastName nvarchar(max) NOT NULL DEFAULT '',
                          CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
                      )",
                    
                    // CreditPackages table with all required columns
                    @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CreditPackages' AND xtype='U')
                      CREATE TABLE CreditPackages (
                          Id int IDENTITY(1,1) PRIMARY KEY,
                          Name nvarchar(max) NOT NULL,
                          Credits int NOT NULL,
                          Price decimal(18,2) NOT NULL,
                          Description nvarchar(500) NOT NULL DEFAULT '',
                          DisplayOrder int NOT NULL DEFAULT 0,
                          BonusCredits int NOT NULL DEFAULT 0,
                          StripeProductId nvarchar(max) NULL,
                          StripePriceId nvarchar(max) NULL,
                          IsActive bit NOT NULL DEFAULT 1,
                          CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
                          UpdatedAt datetime2 NULL
                      )",
                    
                    // Add missing columns to existing CreditPackages table
                    @"IF EXISTS (SELECT * FROM sysobjects WHERE name='CreditPackages' AND xtype='U')
                      BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'Description')
                          ALTER TABLE CreditPackages ADD Description nvarchar(500) NOT NULL DEFAULT ''
                        
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'DisplayOrder')
                          ALTER TABLE CreditPackages ADD DisplayOrder int NOT NULL DEFAULT 0
                        
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'BonusCredits')
                          ALTER TABLE CreditPackages ADD BonusCredits int NOT NULL DEFAULT 0
                        
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'StripeProductId')
                          ALTER TABLE CreditPackages ADD StripeProductId nvarchar(max) NULL
                        
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'StripePriceId')
                          ALTER TABLE CreditPackages ADD StripePriceId nvarchar(max) NULL
                        
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'UpdatedAt')
                          ALTER TABLE CreditPackages ADD UpdatedAt datetime2 NULL
                      END",
                    
                    // UserProfiles table
                    @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserProfiles' AND xtype='U')
                      CREATE TABLE UserProfiles (
                          Id int IDENTITY(1,1) PRIMARY KEY,
                          UserId nvarchar(450) NOT NULL,
                          Credits int NOT NULL DEFAULT 0,
                          CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
                          FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)
                      )",
                    
                    // Styles table
                    @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Styles' AND xtype='U')
                      CREATE TABLE Styles (
                          Id int IDENTITY(1,1) PRIMARY KEY,
                          Name nvarchar(max) NOT NULL,
                          Category nvarchar(max) NOT NULL DEFAULT '',
                          Description nvarchar(max) NOT NULL DEFAULT '',
                          IsActive bit NOT NULL DEFAULT 1,
                          CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
                      )",
                    
                    // Seed basic credit packages
                    @"IF NOT EXISTS (SELECT 1 FROM CreditPackages)
                      INSERT INTO CreditPackages (Name, Credits, Price, IsActive) VALUES 
                      ('Starter Pack', 10, 9.99, 1),
                      ('Standard Pack', 25, 19.99, 1),
                      ('Premium Pack', 50, 34.99, 1)",
                    
                    // Seed basic styles
                    @"IF NOT EXISTS (SELECT 1 FROM Styles)
                      INSERT INTO Styles (Name, Category, Description, IsActive) VALUES 
                      ('professional', 'Business', 'Professional business headshot style', 1),
                      ('casual', 'Lifestyle', 'Casual everyday portrait style', 1),
                      ('artistic', 'Creative', 'Artistic and creative portrait style', 1)"
                };

                foreach (var sql in sqlCommands)
                {
                    _logger.LogCritical("Executing SQL: {Command}", sql.Substring(0, Math.Min(100, sql.Length)) + "...");
                    await _context.Database.ExecuteSqlRawAsync(sql);
                }

                _logger.LogCritical("✅ RAW SQL: All tables created successfully");

                // Verify tables exist
                var creditPackageCount = await _context.CreditPackages.CountAsync();
                var userProfileCount = await _context.UserProfiles.CountAsync();
                var styleCount = await _context.Styles.CountAsync();

                var result = new
                {
                    success = true,
                    message = "Database tables created with raw SQL",
                    tables = new
                    {
                        creditPackages = creditPackageCount,
                        userProfiles = userProfileCount,
                        styles = styleCount
                    }
                };

                _logger.LogCritical("Raw SQL result: {@Result}", result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ RAW SQL FAILED: {Message}", ex.Message);
                _logger.LogCritical("Stack trace: {StackTrace}", ex.StackTrace);
                
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("fix-schema")]
        public async Task<IActionResult> FixMissingColumns()
        {
            try
            {
                _logger.LogCritical("🚨 SCHEMA FIX: Adding missing columns to CreditPackages table");
                
                // Check database connection
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogCritical("Database connection status: {CanConnect}", canConnect);
                
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                // Add missing columns to CreditPackages table
                var sqlCommands = new[]
                {
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'Description')
                      ALTER TABLE CreditPackages ADD Description nvarchar(500) NOT NULL DEFAULT ''",
                    
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'DisplayOrder')
                      ALTER TABLE CreditPackages ADD DisplayOrder int NOT NULL DEFAULT 0",
                    
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'BonusCredits')
                      ALTER TABLE CreditPackages ADD BonusCredits int NOT NULL DEFAULT 0",
                    
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'StripeProductId')
                      ALTER TABLE CreditPackages ADD StripeProductId nvarchar(max) NULL",
                    
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'StripePriceId')
                      ALTER TABLE CreditPackages ADD StripePriceId nvarchar(max) NULL",
                    
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'UpdatedAt')
                      ALTER TABLE CreditPackages ADD UpdatedAt datetime2 NULL"
                };

                foreach (var sql in sqlCommands)
                {
                    _logger.LogCritical("Executing schema fix: {Command}", sql.Substring(0, Math.Min(80, sql.Length)) + "...");
                    await _context.Database.ExecuteSqlRawAsync(sql);
                }

                _logger.LogCritical("✅ SCHEMA FIX: All missing columns added successfully");

                // Test the fix by trying to query CreditPackages
                try
                {
                    var creditPackageCount = await _context.CreditPackages.CountAsync();
                    _logger.LogCritical("✅ SCHEMA FIX: CreditPackages query successful, count: {Count}", creditPackageCount);
                    
                    return Ok(new
                    {
                        success = true,
                        message = "Missing columns added successfully",
                        creditPackagesCount = creditPackageCount
                    });
                }
                catch (Exception queryEx)
                {
                    _logger.LogCritical("❌ SCHEMA FIX: Query test failed: {Message}", queryEx.Message);
                    return StatusCode(500, new { error = $"Schema fix applied but query still fails: {queryEx.Message}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ SCHEMA FIX FAILED: {Message}", ex.Message);
                _logger.LogCritical("Stack trace: {StackTrace}", ex.StackTrace);
                
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("database-status")]
        public async Task<IActionResult> GetDatabaseStatus()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                
                if (!canConnect)
                {
                    return Ok(new { canConnect = false, tables = "Cannot connect to check tables" });
                }

                var tables = new Dictionary<string, object>();
                
                try
                {
                    tables["creditPackages"] = await _context.CreditPackages.CountAsync();
                }
                catch (Exception ex)
                {
                    tables["creditPackages"] = $"Error: {ex.Message}";
                }

                try
                {
                    tables["userProfiles"] = await _context.UserProfiles.CountAsync();
                }
                catch (Exception ex)
                {
                    tables["userProfiles"] = $"Error: {ex.Message}";
                }

                try
                {
                    tables["styles"] = await _context.Styles.CountAsync();
                }
                catch (Exception ex)
                {
                    tables["styles"] = $"Error: {ex.Message}";
                }

                return Ok(new { canConnect = true, tables });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}