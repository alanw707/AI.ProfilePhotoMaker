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
                    
                    // Seed comprehensive styles
                    @"DELETE FROM Styles; 
                      INSERT INTO Styles (Name, Category, Description, IsActive) VALUES 
                      ('professional', 'Business', 'Professional business headshot style', 1),
                      ('casual', 'Lifestyle', 'Casual everyday portrait style', 1),
                      ('artistic', 'Creative', 'Artistic and creative portrait style', 1),
                      ('corporate', 'Business', 'Corporate executive professional style', 1),
                      ('executive', 'Business', 'Senior executive leadership style', 1),
                      ('consultant', 'Business', 'Professional consultant style', 1),
                      ('linkedin', 'Business', 'LinkedIn profile optimized style', 1),
                      ('legal', 'Business', 'Legal professional style', 1),
                      ('medical', 'Business', 'Healthcare professional style', 1),
                      ('academic', 'Business', 'Academic and educational style', 1),
                      ('entrepreneur', 'Business', 'Startup entrepreneur style', 1),
                      ('startup', 'Business', 'Startup culture style', 1),
                      ('tech-professional', 'Business', 'Technology professional style', 1),
                      ('influencer', 'Creative', 'Social media influencer style', 1),
                      ('digital-nomad', 'Lifestyle', 'Remote worker style', 1),
                      ('creative', 'Creative', 'Creative professional style', 1),
                      ('edgy-urban', 'Creative', 'Modern urban style', 1),
                      ('glamour', 'Creative', 'Glamorous style', 1),
                      ('fitness', 'Lifestyle', 'Health and fitness style', 1),
                      ('spiritual', 'Lifestyle', 'Spiritual and wellness style', 1),
                      ('author', 'Creative', 'Literary author style', 1)"
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

        [HttpPost("add-missing-columns")]
        public async Task<IActionResult> AddMissingColumns()
        {
            try
            {
                _logger.LogCritical("🚨 COLUMN FIX: Adding missing columns to CreditPackages table");
                
                // Check database connection
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogCritical("Database connection status: {CanConnect}", canConnect);
                
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                // Add missing columns one by one with individual error handling
                var commands = new[]
                {
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'Description') ALTER TABLE CreditPackages ADD Description nvarchar(500) NOT NULL DEFAULT ''",
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'DisplayOrder') ALTER TABLE CreditPackages ADD DisplayOrder int NOT NULL DEFAULT 0",
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'BonusCredits') ALTER TABLE CreditPackages ADD BonusCredits int NOT NULL DEFAULT 0",
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'StripeProductId') ALTER TABLE CreditPackages ADD StripeProductId nvarchar(max) NULL",
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'StripePriceId') ALTER TABLE CreditPackages ADD StripePriceId nvarchar(max) NULL",
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE CreditPackages ADD UpdatedAt datetime2 NULL"
                };

                var results = new List<string>();
                foreach (var command in commands)
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(command);
                        results.Add($"SUCCESS: {command.Substring(command.IndexOf("'") + 1, command.IndexOf("'", command.IndexOf("'") + 1) - command.IndexOf("'") - 1)}");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"ERROR: {ex.Message}");
                        _logger.LogCritical("Failed to execute: {Command}, Error: {Error}", command, ex.Message);
                    }
                }

                // Test the fix by trying to query CreditPackages
                try
                {
                    var creditPackageCount = await _context.CreditPackages.CountAsync();
                    _logger.LogCritical("✅ COLUMN FIX: CreditPackages query successful, count: {Count}", creditPackageCount);
                    
                    return Ok(new
                    {
                        success = true,
                        message = "Column addition attempts completed",
                        results = results,
                        creditPackagesCount = creditPackageCount
                    });
                }
                catch (Exception queryEx)
                {
                    _logger.LogCritical("❌ COLUMN FIX: Query test failed: {Message}", queryEx.Message);
                    return Ok(new
                    {
                        success = false,
                        message = "Columns added but query still fails",
                        results = results,
                        error = queryEx.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ COLUMN FIX FAILED: {Message}", ex.Message);
                _logger.LogCritical("Stack trace: {StackTrace}", ex.StackTrace);
                
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("migrate-descriptions")]
        public async Task<IActionResult> MigrateDescriptions()
        {
            try
            {
                _logger.LogCritical("🚨 DATA MIGRATION: Starting description migration for CreditPackages and Styles");
                
                // Check database connection
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogCritical("Database connection status: {CanConnect}", canConnect);
                
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                var results = new List<string>();

                // Step 1: Update CreditPackages descriptions
                _logger.LogCritical("🚨 DATA MIGRATION: Updating CreditPackages descriptions...");
                
                var creditPackageUpdates = new[]
                {
                    @"UPDATE CreditPackages 
                      SET Description = 'Perfect for trying out custom training and styled generations',
                          DisplayOrder = 1,
                          BonusCredits = 0,
                          UpdatedAt = GETUTCDATE()
                      WHERE Name LIKE '%Starter%' AND (Description = '' OR Description IS NULL)",
                    
                    @"UPDATE CreditPackages 
                      SET Description = 'Most popular - great for professionals',
                          DisplayOrder = 2,
                          BonusCredits = 30,
                          UpdatedAt = GETUTCDATE()
                      WHERE Name LIKE '%Professional%' AND (Description = '' OR Description IS NULL)",
                    
                    @"UPDATE CreditPackages 
                      SET Description = 'Best value for content creators and businesses',
                          DisplayOrder = 3,
                          BonusCredits = 100,
                          UpdatedAt = GETUTCDATE()
                      WHERE Name LIKE '%Studio%' AND (Description = '' OR Description IS NULL)"
                };

                foreach (var sql in creditPackageUpdates)
                {
                    try
                    {
                        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(sql);
                        results.Add($"CreditPackages update executed successfully");
                        _logger.LogCritical("CreditPackages update executed: {Sql}", sql.Substring(0, Math.Min(50, sql.Length)) + "...");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"CreditPackages update failed: {ex.Message}");
                        _logger.LogCritical("❌ CreditPackages update failed: {Error}", ex.Message);
                    }
                }

                // Step 2: Add missing columns to Styles table if needed
                _logger.LogCritical("🚨 DATA MIGRATION: Adding missing Styles columns...");
                
                var styleColumnUpdates = new[]
                {
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Styles' AND COLUMN_NAME = 'PromptTemplate')
                      ALTER TABLE Styles ADD PromptTemplate nvarchar(max) NULL",
                    
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Styles' AND COLUMN_NAME = 'NegativePromptTemplate')
                      ALTER TABLE Styles ADD NegativePromptTemplate nvarchar(max) NULL",
                    
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Styles' AND COLUMN_NAME = 'UpdatedAt')
                      ALTER TABLE Styles ADD UpdatedAt datetime2 NULL"
                };

                foreach (var sql in styleColumnUpdates)
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(sql);
                        results.Add("Styles column update executed successfully");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"Styles column update failed: {ex.Message}");
                        _logger.LogCritical("❌ Styles column update failed: {Error}", ex.Message);
                    }
                }

                // Step 3: Update key Styles with rich descriptions
                _logger.LogCritical("🚨 DATA MIGRATION: Updating Styles descriptions...");
                
                var styleUpdates = new[]
                {
                    @"UPDATE Styles 
                      SET Description = 'Professional studio portrait in formal business attire with clean background',
                          PromptTemplate = 'Professional studio portrait of a {gender} in formal business attire, clean background, confident expression, corporate office lighting, sharp focus',
                          NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual clothing, inappropriate attire',
                          UpdatedAt = GETUTCDATE()
                      WHERE Name = 'corporate'",
                    
                    @"UPDATE Styles 
                      SET Description = 'Professional LinkedIn-style headshot with confident and warm expression',
                          PromptTemplate = 'Professional LinkedIn-style headshot of a {gender}, neutral background, confident and warm smile, clean business-casual attire, high clarity',
                          NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, full body shot, distracting background',
                          UpdatedAt = GETUTCDATE()
                      WHERE Name = 'linkedin'",
                    
                    @"UPDATE Styles 
                      SET Description = 'Natural lifestyle photo in everyday clothing with warm lighting',
                          PromptTemplate = 'Natural lifestyle photo of a {gender} in everyday clothing, warm lighting, soft expression, home or park background, candid feel',
                          NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, formal business attire',
                          UpdatedAt = GETUTCDATE()
                      WHERE Name = 'casual'",
                    
                    @"UPDATE Styles 
                      SET Description = 'Fine art portrait with dramatic lighting and stylized clothing',
                          PromptTemplate = 'Fine art portrait of a {gender} in dramatic lighting, stylized clothing, moody background, painterly composition, thoughtful gaze',
                          NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, plain background, conventional lighting',
                          UpdatedAt = GETUTCDATE()
                      WHERE Name = 'artistic'",
                    
                    @"UPDATE Styles 
                      SET Description = 'Modern startup founder portrait in co-working space with confident energy',
                          PromptTemplate = 'Modern portrait of a {gender} startup founder in a co-working space or minimalist office, tech-savvy outfit, confident energy, natural lighting',
                          NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated office, traditional formal attire',
                          UpdatedAt = GETUTCDATE()
                      WHERE Name = 'entrepreneur'"
                };

                foreach (var sql in styleUpdates)
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(sql);
                        results.Add("Styles description update executed successfully");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"Styles description update failed: {ex.Message}");
                        _logger.LogCritical("❌ Styles description update failed: {Error}", ex.Message);
                    }
                }

                _logger.LogCritical("✅ DATA MIGRATION: Migration completed");

                // Verify the results
                var creditPackageCount = await _context.CreditPackages.CountAsync();
                var styleCount = await _context.Styles.CountAsync();
                
                // Get sample data to verify descriptions
                var samplePackage = await _context.CreditPackages.FirstOrDefaultAsync();
                var sampleStyle = await _context.Styles.FirstOrDefaultAsync();

                var result = new
                {
                    success = true,
                    message = "Data migration completed",
                    results = results,
                    verification = new
                    {
                        creditPackagesCount = creditPackageCount,
                        stylesCount = styleCount,
                        samplePackageDescription = samplePackage?.Description ?? "No package found",
                        sampleStyleDescription = sampleStyle?.Description ?? "No style found"
                    }
                };

                _logger.LogCritical("Migration result: {@Result}", result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ DATA MIGRATION FAILED: {Message}", ex.Message);
                _logger.LogCritical("Stack trace: {StackTrace}", ex.StackTrace);
                
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("inspect-tables")]
        public async Task<IActionResult> InspectTables()
        {
            try
            {
                _logger.LogCritical("🔍 INSPECT: Examining database table structures and data");
                
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                var result = new Dictionary<string, object>();
                
                // Check CreditPackages table structure
                try
                {
                    var creditPackageColumns = await _context.Database.SqlQueryRaw<string>(
                        @"SELECT COLUMN_NAME + ' (' + DATA_TYPE + 
                          CASE WHEN IS_NULLABLE = 'YES' THEN ', NULL' ELSE ', NOT NULL' END + ')' as ColumnInfo
                          FROM INFORMATION_SCHEMA.COLUMNS 
                          WHERE TABLE_NAME = 'CreditPackages'
                          ORDER BY ORDINAL_POSITION").ToListAsync();
                    
                    result["creditPackageColumns"] = creditPackageColumns;
                }
                catch (Exception ex)
                {
                    result["creditPackageColumns"] = $"Error: {ex.Message}";
                }

                // Check Styles table structure  
                try
                {
                    var styleColumns = await _context.Database.SqlQueryRaw<string>(
                        @"SELECT COLUMN_NAME + ' (' + DATA_TYPE + 
                          CASE WHEN IS_NULLABLE = 'YES' THEN ', NULL' ELSE ', NOT NULL' END + ')' as ColumnInfo
                          FROM INFORMATION_SCHEMA.COLUMNS 
                          WHERE TABLE_NAME = 'Styles'
                          ORDER BY ORDINAL_POSITION").ToListAsync();
                    
                    result["styleColumns"] = styleColumns;
                }
                catch (Exception ex)
                {
                    result["styleColumns"] = $"Error: {ex.Message}";
                }

                // Get sample data
                try
                {
                    var creditPackages = await _context.CreditPackages.Take(3).Select(cp => new {
                        cp.Id,
                        cp.Name,
                        cp.Credits,
                        cp.Price,
                        Description = cp.Description ?? "NULL",
                        DisplayOrder = (int?)null, // Will show if column exists
                        BonusCredits = (int?)null  // Will show if column exists
                    }).ToListAsync();
                    
                    result["sampleCreditPackages"] = creditPackages;
                }
                catch (Exception ex)
                {
                    result["sampleCreditPackages"] = $"Error: {ex.Message}";
                }

                try
                {
                    var styles = await _context.Styles.Take(3).Select(s => new {
                        s.Id,
                        s.Name,
                        Description = s.Description ?? "NULL",
                        s.IsActive
                    }).ToListAsync();
                    
                    result["sampleStyles"] = styles;
                }
                catch (Exception ex)
                {
                    result["sampleStyles"] = $"Error: {ex.Message}";
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ INSPECT FAILED: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("fix-packages-simple")]
        public async Task<IActionResult> FixPackagesSimple()
        {
            try
            {
                _logger.LogCritical("🚨 SIMPLE FIX: Adding missing columns and updating CreditPackages");
                
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                var results = new List<string>();

                // Step 1: Add missing columns to CreditPackages
                var columnCommands = new[]
                {
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'Description') ALTER TABLE CreditPackages ADD Description nvarchar(500) NULL",
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'DisplayOrder') ALTER TABLE CreditPackages ADD DisplayOrder int NULL",
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'BonusCredits') ALTER TABLE CreditPackages ADD BonusCredits int NULL",
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE CreditPackages ADD UpdatedAt datetime2 NULL"
                };

                foreach (var sql in columnCommands)
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(sql);
                        results.Add($"Column command executed: SUCCESS");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"Column command failed: {ex.Message}");
                    }
                }

                // Step 2: Update specific packages by ID
                var updateCommands = new[]
                {
                    "UPDATE CreditPackages SET Description = 'Perfect for trying out custom training and styled generations', DisplayOrder = 1, BonusCredits = 0 WHERE Id = 1",
                    "UPDATE CreditPackages SET Description = 'Most popular - great for professionals', DisplayOrder = 2, BonusCredits = 30 WHERE Id = 2", 
                    "UPDATE CreditPackages SET Description = 'Best value for content creators and businesses', DisplayOrder = 3, BonusCredits = 100 WHERE Id = 3"
                };

                foreach (var sql in updateCommands)
                {
                    try
                    {
                        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(sql);
                        results.Add($"Update executed: {rowsAffected} rows affected");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"Update failed: {ex.Message}");
                    }
                }

                return Ok(new { success = true, message = "Simple fix completed", results = results });
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ SIMPLE FIX FAILED: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("update-credit-descriptions")]
        public async Task<IActionResult> UpdateCreditDescriptions()
        {
            try
            {
                _logger.LogCritical("🚨 CREDIT DESCRIPTION UPDATE: Starting simple description updates");
                
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                var results = new List<string>();

                // Execute the 3 simple UPDATE statements as requested (updated to match actual package names)
                var updateCommands = new[]
                {
                    "UPDATE CreditPackages SET Description = 'Perfect for trying out our service' WHERE Name LIKE '%Starter%'",
                    "UPDATE CreditPackages SET Description = 'Great value for regular users' WHERE Name LIKE '%Professional%'",
                    "UPDATE CreditPackages SET Description = 'Best value with bonus credits' WHERE Name LIKE '%Studio%'"
                };

                foreach (var sql in updateCommands)
                {
                    try
                    {
                        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(sql);
                        results.Add($"✅ {sql} → {rowsAffected} rows updated");
                        _logger.LogCritical("SQL executed: {Command} → {RowsAffected} rows", sql, rowsAffected);
                    }
                    catch (Exception ex)
                    {
                        results.Add($"❌ {sql} → Error: {ex.Message}");
                        _logger.LogCritical("❌ SQL failed: {Command} → {Error}", sql, ex.Message);
                    }
                }

                // Verify the updates by querying the packages
                try
                {
                    var packages = await _context.CreditPackages
                        .Select(p => new { p.Id, p.Name, p.Description })
                        .ToListAsync();
                    
                    return Ok(new 
                    { 
                        success = true, 
                        message = "Description updates completed", 
                        updateResults = results,
                        verificationData = packages
                    });
                }
                catch (Exception ex)
                {
                    return Ok(new 
                    { 
                        success = false, 
                        message = "Updates executed but verification failed", 
                        updateResults = results,
                        verificationError = ex.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ CREDIT DESCRIPTION UPDATE FAILED: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("populate-missing-styles")]
        public async Task<IActionResult> PopulateMissingStyles()
        {
            try
            {
                _logger.LogCritical("🚨 STYLE POPULATION: Adding missing styles to database");
                
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return BadRequest("Cannot connect to database");
                }

                var results = new List<string>();
                var currentStyleCount = await _context.Styles.CountAsync();
                _logger.LogCritical("Current style count: {Count}", currentStyleCount);

                // Define all 20+ professional styles with rich descriptions and templates
                var stylesToAdd = new[]
                {
                    new { Name = "corporate", Description = "Professional corporate headshot with formal business attire", PromptTemplate = "Professional corporate headshot of a {gender} in formal business attire, clean neutral background, confident expression, studio lighting, sharp focus", NegativePromptTemplate = "casual clothing, informal attire, distracting background, poor lighting, blurry" },
                    new { Name = "executive", Description = "Executive-level portrait for C-suite professionals", PromptTemplate = "Executive portrait of a {gender} in premium business attire, sophisticated lighting, authoritative presence, luxury office background", NegativePromptTemplate = "casual wear, unprofessional background, poor posture, distracting elements" },
                    new { Name = "consultant", Description = "Professional consultant style for client-facing roles", PromptTemplate = "Professional consultant portrait of a {gender} in smart business-casual attire, approachable yet professional expression, modern office setting", NegativePromptTemplate = "overly casual clothing, unprofessional setting, intimidating expression" },
                    new { Name = "linkedin", Description = "Optimized LinkedIn profile photo with warm professional appeal", PromptTemplate = "LinkedIn-optimized headshot of a {gender}, business-casual attire, warm smile, neutral background, natural lighting", NegativePromptTemplate = "formal suit, cold expression, cluttered background, harsh lighting" },
                    new { Name = "legal", Description = "Legal professional portrait conveying trust and authority", PromptTemplate = "Legal professional portrait of a {gender} in formal dark suit, confident expression, traditional office background, authoritative presence", NegativePromptTemplate = "casual attire, informal setting, unprofessional appearance, distracting background" },
                    new { Name = "medical", Description = "Healthcare professional portrait with approachable demeanor", PromptTemplate = "Medical professional portrait of a {gender} in white coat or professional attire, warm caring expression, clinical background", NegativePromptTemplate = "unprofessional attire, harsh expression, inappropriate background, distracting elements" },
                    new { Name = "academic", Description = "Academic professional for university and research settings", PromptTemplate = "Academic professional portrait of a {gender} in smart casual attire, thoughtful expression, university or library background", NegativePromptTemplate = "overly formal suit, intimidating expression, distracting background, unprofessional setting" },
                    new { Name = "entrepreneur", Description = "Modern entrepreneur portrait with innovative energy", PromptTemplate = "Entrepreneur portrait of a {gender} in contemporary business attire, confident innovative expression, modern workspace background", NegativePromptTemplate = "traditional formal wear, conservative setting, rigid posture, outdated styling" },
                    new { Name = "startup", Description = "Startup professional with approachable tech-savvy appeal", PromptTemplate = "Startup professional portrait of a {gender} in modern casual-professional attire, energetic expression, contemporary office setting", NegativePromptTemplate = "formal traditional suit, corporate setting, rigid expression, outdated styling" },
                    new { Name = "tech-professional", Description = "Technology sector professional with modern appeal", PromptTemplate = "Tech professional portrait of a {gender} in smart casual attire, innovative expression, modern tech office background", NegativePromptTemplate = "formal business suit, traditional office, conservative styling, outdated appearance" },
                    new { Name = "influencer", Description = "Social media influencer style with engaging personality", PromptTemplate = "Influencer portrait of a {gender} in trendy stylish attire, charismatic engaging expression, contemporary lifestyle background", NegativePromptTemplate = "formal business wear, rigid posture, corporate setting, boring expression" },
                    new { Name = "digital-nomad", Description = "Digital nomad professional with location independence appeal", PromptTemplate = "Digital nomad portrait of a {gender} in casual professional attire, relaxed confident expression, co-working space or cafe background", NegativePromptTemplate = "formal suit, rigid corporate setting, stiff posture, traditional office background" },
                    new { Name = "creative", Description = "Creative professional with artistic flair", PromptTemplate = "Creative professional portrait of a {gender} in stylish artistic attire, expressive personality, creative studio background", NegativePromptTemplate = "boring business suit, corporate setting, rigid expression, conservative styling" },
                    new { Name = "edgy-urban", Description = "Modern urban professional with contemporary edge", PromptTemplate = "Urban professional portrait of a {gender} in contemporary edgy attire, confident modern expression, urban cityscape background", NegativePromptTemplate = "conservative business wear, suburban setting, traditional styling, outdated appearance" },
                    new { Name = "glamour", Description = "Glamorous professional portrait for high-end industries", PromptTemplate = "Glamour professional portrait of a {gender} in elegant upscale attire, sophisticated expression, luxury setting background", NegativePromptTemplate = "casual wear, simple background, plain styling, understated appearance" },
                    new { Name = "fitness", Description = "Health and fitness professional with active lifestyle appeal", PromptTemplate = "Fitness professional portrait of a {gender} in athletic-casual attire, healthy energetic expression, gym or outdoor setting", NegativePromptTemplate = "formal business wear, sedentary setting, low energy expression, indoor office" },
                    new { Name = "spiritual", Description = "Wellness and spiritual professional with calming presence", PromptTemplate = "Spiritual wellness portrait of a {gender} in comfortable natural attire, serene peaceful expression, nature or zen setting", NegativePromptTemplate = "formal business suit, corporate setting, stressed expression, artificial background" },
                    new { Name = "artistic", Description = "Fine art portrait with dramatic creative lighting", PromptTemplate = "Fine art portrait of a {gender} with dramatic artistic lighting, creative expression, painterly composition", NegativePromptTemplate = "commercial lighting, plain background, conventional styling, basic composition" },
                    new { Name = "casual", Description = "Natural lifestyle portrait with everyday appeal", PromptTemplate = "Casual lifestyle portrait of a {gender} in comfortable everyday attire, natural relaxed expression, home or outdoor setting", NegativePromptTemplate = "formal business wear, corporate setting, stiff posture, artificial expression" },
                    new { Name = "professional", Description = "Classic professional headshot for general business use", PromptTemplate = "Professional headshot of a {gender} in business attire, confident professional expression, neutral studio background", NegativePromptTemplate = "casual wear, informal setting, unprofessional appearance, distracting background" }
                };

                var addedCount = 0;
                var skippedCount = 0;

                foreach (var styleData in stylesToAdd)
                {
                    try
                    {
                        // Check if style already exists
                        var existingStyle = await _context.Styles
                            .FirstOrDefaultAsync(s => s.Name.ToLower() == styleData.Name.ToLower());

                        if (existingStyle != null)
                        {
                            skippedCount++;
                            results.Add($"SKIPPED: Style '{styleData.Name}' already exists (ID: {existingStyle.Id})");
                            continue;
                        }

                        // Add new style
                        var newStyle = new Models.Style
                        {
                            Name = styleData.Name,
                            Description = styleData.Description,
                            PromptTemplate = styleData.PromptTemplate,
                            NegativePromptTemplate = styleData.NegativePromptTemplate,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Styles.Add(newStyle);
                        await _context.SaveChangesAsync();

                        addedCount++;
                        results.Add($"✅ ADDED: Style '{styleData.Name}' (ID: {newStyle.Id})");
                        _logger.LogCritical("Added style: {StyleName} with ID: {StyleId}", styleData.Name, newStyle.Id);
                    }
                    catch (Exception ex)
                    {
                        results.Add($"❌ ERROR adding '{styleData.Name}': {ex.Message}");
                        _logger.LogCritical("Failed to add style {StyleName}: {Error}", styleData.Name, ex.Message);
                    }
                }

                var finalStyleCount = await _context.Styles.CountAsync();

                var result = new
                {
                    success = true,
                    message = $"Style population completed. Added {addedCount} new styles, skipped {skippedCount} existing styles.",
                    statistics = new
                    {
                        initialCount = currentStyleCount,
                        finalCount = finalStyleCount,
                        addedCount = addedCount,
                        skippedCount = skippedCount,
                        totalTargetStyles = stylesToAdd.Length
                    },
                    details = results
                };

                _logger.LogCritical("Style population result: {@Result}", result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("❌ STYLE POPULATION FAILED: {Message}", ex.Message);
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