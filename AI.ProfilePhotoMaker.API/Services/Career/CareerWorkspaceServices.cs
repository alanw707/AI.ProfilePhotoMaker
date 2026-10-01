using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Reads <c>Features:CareerWorkspace</c>; off unless explicitly set to true.</summary>
public interface ICareerFeatureGate
{
    bool IsEnabled { get; }
}

public sealed class CareerFeatureGate : ICareerFeatureGate
{
    public const string ConfigKey = "Features:CareerWorkspace";

    private readonly IConfiguration _configuration;

    public CareerFeatureGate(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // Read per request so a configuration reload takes effect without a restart.
    public bool IsEnabled => _configuration.GetValue<bool?>(ConfigKey) ?? false;
}

/// <summary>
/// Refuses career endpoints when the flag is off. A resource filter runs after
/// authentication/authorization but before model binding and validation, so a
/// disabled workspace always answers 403 <c>CareerWorkspaceDisabled</c> (spec
/// #376) regardless of the request body.
/// </summary>
public sealed class RequireCareerWorkspaceFilter : IResourceFilter
{
    private readonly ICareerFeatureGate _gate;

    public RequireCareerWorkspaceFilter(ICareerFeatureGate gate)
    {
        _gate = gate;
    }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (_gate.IsEnabled)
        {
            return;
        }

        context.Result = new ObjectResult(new
        {
            success = false,
            error = new
            {
                code = CareerErrorCodes.Disabled,
                message = "The career workspace is not available for this account.",
                correlationId = context.HttpContext.TraceIdentifier
            }
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}

/// <summary>
/// Removes an owner's private career data. Every private career entity must be
/// listed in <see cref="CoveredEntityTypes"/> and deleted here; a test compares
/// this list with the EF model so new entities cannot be forgotten. #392 wires
/// this to user-facing export/deletion controls.
/// </summary>
public interface ICareerPrivateDataService
{
    Task DeleteAllForOwnerAsync(string ownerId, CancellationToken ct = default);
}

public sealed class CareerPrivateDataService : ICareerPrivateDataService
{
    public static readonly IReadOnlySet<Type> CoveredEntityTypes = new HashSet<Type>
    {
        typeof(CareerProfile),
        typeof(CareerProfileVersion),
        typeof(CareerGoal),
        typeof(CareerGoalVersion)
    };

    private readonly ApplicationDbContext _db;

    public CareerPrivateDataService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task DeleteAllForOwnerAsync(string ownerId, CancellationToken ct = default)
    {
        // Versions are removed explicitly rather than relying on cascades, so the
        // behaviour is the same on providers that do not enforce foreign keys.
        _db.CareerProfileVersions.RemoveRange(await _db.CareerProfileVersions.Where(v => v.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerGoalVersions.RemoveRange(await _db.CareerGoalVersions.Where(v => v.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerProfiles.RemoveRange(await _db.CareerProfiles.Where(p => p.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerGoals.RemoveRange(await _db.CareerGoals.Where(g => g.OwnerId == ownerId).ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
    }
}

public static class CareerWorkspaceServiceCollectionExtensions
{
    /// <summary>Registers career workspace services. Used by Program.cs and the test host.</summary>
    public static IServiceCollection AddCareerWorkspace(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICareerFeatureGate, CareerFeatureGate>();
        services.AddScoped<RequireCareerWorkspaceFilter>();
        services.AddScoped<ICareerProfileService, CareerProfileService>();
        services.AddScoped<ICareerPrivateDataService, CareerPrivateDataService>();
        return services;
    }
}
