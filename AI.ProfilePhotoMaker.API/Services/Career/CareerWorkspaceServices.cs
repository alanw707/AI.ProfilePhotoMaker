using System.Security.Claims;
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

/// <summary>
/// Reads <c>Features:CareerWorkspace</c> (off unless explicitly true) and the rollout audience.
/// <see cref="IsEnabled"/> is the system switch: background work runs whenever it is on.
/// <see cref="IsEnabledFor"/> decides one account: <c>Features:CareerWorkspaceAudience</c> is
/// <c>Everyone</c> (the default) or <c>Allowlist</c>, which admits only the emails listed in
/// <c>Features:CareerWorkspaceAllowedEmails</c> (separated by ';' or ','). Any other audience fails closed.
/// </summary>
public interface ICareerFeatureGate
{
    bool IsEnabled { get; }

    /// <summary>True when the public (signed-out) config may advertise career to everyone.</summary>
    bool IsOpenToEveryone { get; }

    bool IsEnabledFor(ClaimsPrincipal user);
}

public sealed class CareerFeatureGate : ICareerFeatureGate
{
    public const string ConfigKey = "Features:CareerWorkspace";
    public const string AudienceKey = "Features:CareerWorkspaceAudience";
    public const string AllowedEmailsKey = "Features:CareerWorkspaceAllowedEmails";
    public const string EveryoneAudience = "Everyone";
    public const string AllowlistAudience = "Allowlist";

    private readonly IConfiguration _configuration;

    public CareerFeatureGate(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // Read per request so a configuration reload takes effect without a restart.
    public bool IsEnabled => _configuration.GetValue<bool?>(ConfigKey) ?? false;

    private string Audience
    {
        get
        {
            var value = _configuration[AudienceKey];
            return string.IsNullOrWhiteSpace(value) ? EveryoneAudience : value.Trim();
        }
    }

    public bool IsOpenToEveryone => IsEnabled && string.Equals(Audience, EveryoneAudience, StringComparison.OrdinalIgnoreCase);

    public bool IsEnabledFor(ClaimsPrincipal user)
    {
        if (!IsEnabled)
        {
            return false;
        }
        if (string.Equals(Audience, EveryoneAudience, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (!string.Equals(Audience, AllowlistAudience, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var email = user.FindFirstValue(ClaimTypes.Email)?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return false;
        }
        var allowed = (_configuration[AllowedEmailsKey] ?? "")
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowed.Contains(email, StringComparer.OrdinalIgnoreCase);
    }
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
        if (_gate.IsEnabledFor(context.HttpContext.User))
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

        // Privacy (#392, ADR 0020). The startup replay is Program.cs-only so the test host never runs it.
        services.AddOptions<CareerPrivacyOptions>().BindConfiguration(CareerPrivacyOptions.SectionName);
        services.AddScoped<ICareerPrivacyExporter, CareerPrivacyExporter>();
        services.AddScoped<ICareerPrivacyService, CareerPrivacyService>();
        services.AddScoped<ICareerTombstoneReplayer, CareerTombstoneReplayer>();
        services.AddScoped<CareerReplayGate>();
        services.AddScoped<CareerReplayGuardFilter>();
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

        // Usage controls (#395, ADR 0022). The reaper loop is Program.cs-only; tests call it directly.
        services.AddOptions<CareerUsagePolicy>().BindConfiguration(CareerUsagePolicy.SectionName);
        services.AddSingleton<ICareerOperatorControls, CareerOperatorControls>();
        services.AddScoped<ICareerReservationReaper, CareerReservationReaper>();
        services.AddScoped<ICareerUsageReportService, CareerUsageReportService>();
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
        services.AddScoped<ICareerJourneyService, CareerJourneyService>();

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
