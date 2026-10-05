using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career.Export;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        typeof(CareerGoalVersion),
        typeof(ResumeDocument),
        typeof(CareerProfileProposal),
        typeof(CareerProfileProposalItem),
        typeof(CareerPhotoSelection),
        typeof(CareerAgentRun),
        typeof(CareerAgentStep),
        typeof(CareerAllowance),
        typeof(CareerOccupationMatch),
        typeof(CareerMarketBrief),
        typeof(CareerPayAnalysis),
        typeof(CareerRoadmap),
        typeof(CareerRoadmapTaskProgress),
        typeof(CareerRoadmapReplan),
        typeof(CareerMaterial),
        typeof(CareerMaterialVersion),
        typeof(CareerMaterialProposal),
        typeof(CareerExport)
    };

    private readonly ApplicationDbContext _db;
    private readonly IStorageService _storage;

    public CareerPrivateDataService(ApplicationDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task DeleteAllForOwnerAsync(string ownerId, CancellationToken ct = default)
    {
        // Versions are removed explicitly rather than relying on cascades, so the
        // behaviour is the same on providers that do not enforce foreign keys.
        _db.CareerProfileVersions.RemoveRange(await _db.CareerProfileVersions.Where(v => v.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerGoalVersions.RemoveRange(await _db.CareerGoalVersions.Where(v => v.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerAgentSteps.RemoveRange(await _db.CareerAgentSteps.Where(s => s.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerAgentRuns.RemoveRange(await _db.CareerAgentRuns.Where(r => r.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerAllowances.RemoveRange(await _db.CareerAllowances.Where(a => a.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerOccupationMatches.RemoveRange(await _db.CareerOccupationMatches.Where(m => m.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerMarketBriefs.RemoveRange(await _db.CareerMarketBriefs.Where(b => b.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerPayAnalyses.RemoveRange(await _db.CareerPayAnalyses.Where(b => b.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerRoadmapTaskProgress.RemoveRange(await _db.CareerRoadmapTaskProgress.Where(p => p.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerExports.RemoveRange(await _db.CareerExports.Where(e => e.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerMaterialProposals.RemoveRange(await _db.CareerMaterialProposals.Where(p => p.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerMaterialVersions.RemoveRange(await _db.CareerMaterialVersions.Where(v => v.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerMaterials.RemoveRange(await _db.CareerMaterials.Where(m => m.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerRoadmapReplans.RemoveRange(await _db.CareerRoadmapReplans.Where(r => r.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerRoadmaps.RemoveRange(await _db.CareerRoadmaps.Where(r => r.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerProfileProposalItems.RemoveRange(await _db.CareerProfileProposalItems.Where(i => i.OwnerId == ownerId).ToListAsync(ct));
        _db.CareerProfileProposals.RemoveRange(await _db.CareerProfileProposals.Where(p => p.OwnerId == ownerId).ToListAsync(ct));

        // Raw files go first: a row without its file is recoverable, a file without its row is not.
        var resumes = await _db.CareerResumeDocuments.Where(d => d.OwnerId == ownerId).ToListAsync(ct);
        foreach (var resume in resumes)
        {
            await _storage.DeleteImageAsync(resume.StorageKey);
        }
        _db.CareerResumeDocuments.RemoveRange(resumes);

        // Only the choice is removed; the photo itself belongs to the photo workspace.
        _db.CareerPhotoSelections.RemoveRange(await _db.CareerPhotoSelections.Where(s => s.OwnerId == ownerId).ToListAsync(ct));

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
        services.AddScoped<ICareerPhotoService, CareerPhotoService>();

        // Resume import (#379). The scanner is deliberately NOT registered here: only
        // Program.cs adds the placeholder, and only outside production, so uploads
        // fail closed until a real scanner is chosen. The purge job is Program.cs-only
        // too, so the test host never runs it.
        services.TryAddSingleton<IResumeParser, DependencyFreeResumeParser>();
        services.AddScoped<IResumeImportService, ResumeImportService>();
        services.AddScoped<ICareerProposalService, CareerProposalService>();

        // Agent runtime (#380). No ICareerTextModel is registered here: Program.cs adds the
        // fake outside production, so with none registered runs fail closed (503). The
        // worker is Program.cs-only too, so the test host drives the runner directly.
        services.AddOptions<CareerAgentOptions>().BindConfiguration(CareerAgentOptions.SectionName);
        services.AddScoped<ICareerAgentRunService, CareerAgentRunService>();
        // Built by hand because the model is optional: occupation matching runs without one.
        services.AddScoped<ICareerAgentRunner>(sp => new CareerAgentRunner(
            sp.GetRequiredService<ApplicationDbContext>(),
            sp.GetService<ICareerTextModel>(),
            sp.GetRequiredService<IOptions<CareerAgentOptions>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<CareerAgentRunner>>(),
            sp.GetService<IOccupationReference>(),
            sp.GetService<IMarketReference>(),
            sp.GetRequiredService<IPayObservationSource>()));

        // Occupation matches (#381). The reference is a singleton so the snapshot is parsed and
        // indexed once; tests replace it to cover an unavailable snapshot.
        services.TryAddSingleton<IOccupationReference>(sp =>
            new EmbeddedOccupationReference(EmbeddedOccupationReference.OpenEmbeddedSnapshot, EmbeddedOccupationReference.ExpectedSha256,
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<EmbeddedOccupationReference>()));
        services.AddScoped<ICareerOccupationService, CareerOccupationService>();

        // Market briefs (#382). The BLS reference is a singleton too, parsed once into compact tables.
        services.AddOptions<CareerMarketOptions>().BindConfiguration(CareerMarketOptions.SectionName);
        services.TryAddSingleton<IMarketReference>(sp =>
            new EmbeddedMarketReference(EmbeddedMarketReference.OpenEmbeddedSnapshot, EmbeddedMarketReference.ExpectedSha256,
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<EmbeddedMarketReference>()));
        services.AddScoped<ICareerMarketService, CareerMarketService>();
        services.AddScoped<ICareerMarketComparisonService, MarketComparisonService>();
        services.TryAddSingleton<IPayObservationSource, NoQualifiedPayObservationSource>();
        services.AddScoped<ICareerPayService, CareerPayService>();
        services.AddScoped<ICareerRoadmapService, CareerRoadmapService>();
        services.AddScoped<ICareerMaterialService, CareerMaterialService>();

        // Exports (#390, ADR 0019): renderers sit behind IMaterialExportRenderer.
        services.AddSingleton<IMaterialExportRenderer, PdfMaterialRenderer>();
        services.AddSingleton<IMaterialExportRenderer, DocxMaterialRenderer>();
        services.AddScoped<ICareerExportService, CareerExportService>();
        services.AddScoped<ICareerRoadmapTrackingService>(sp => new CareerRoadmapTrackingService(
            sp.GetRequiredService<ApplicationDbContext>(),
            sp.GetRequiredService<ICareerRoadmapService>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetService<IMarketReference>()));

        // Job observations (#386, ADR 0015). Nothing is persisted. With no USAJobs key the default source
        // reports an honest unavailable state; tests replace IJobObservationSource with a fake.
        services.AddOptions<UsaJobsOptions>().BindConfiguration(UsaJobsOptions.SectionName);
        services.AddHttpClient<UsaJobsObservationSource>()
            .ConfigurePrimaryHttpMessageHandler(UsaJobsObservationSource.CreateHandler);
        services.AddScoped<IJobObservationSource>(sp =>
        {
            var usaJobs = sp.GetRequiredService<UsaJobsObservationSource>();
            return usaJobs.IsConfigured ? usaJobs : new NoJobObservationSource();
        });
        services.AddScoped<IJobObservationService, JobObservationService>();
        return services;
    }
}
