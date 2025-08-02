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