using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Identity;

namespace AI.ProfilePhotoMaker.API.Data;

public static class SeedAdminRole
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        const string adminRole = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
            if (!roleResult.Succeeded)
            {
                logger.LogWarning("Failed to create Admin role: {Errors}", string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                return;
            }
        }

        var initialAdminEmail = configuration["AdminSettings:InitialAdminEmail"];
        if (string.IsNullOrWhiteSpace(initialAdminEmail))
        {
            logger.LogInformation("AdminSettings:InitialAdminEmail not set; skipping admin assignment");
            return;
        }

        var user = await userManager.FindByEmailAsync(initialAdminEmail);
        if (user == null)
        {
            logger.LogInformation("Initial admin email not found in user store: {Email}", LoggingSanitizer.SanitizeId(initialAdminEmail));
            return;
        }

        if (await userManager.IsInRoleAsync(user, adminRole))
        {
            return;
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, adminRole);
        if (!addRoleResult.Succeeded)
        {
            logger.LogWarning("Failed to assign Admin role to {Email}: {Errors}",
                LoggingSanitizer.SanitizeId(initialAdminEmail),
                string.Join("; ", addRoleResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Admin role assigned to {Email}", LoggingSanitizer.SanitizeId(initialAdminEmail));
    }
}
