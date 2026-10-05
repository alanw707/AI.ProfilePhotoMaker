using System.Globalization;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Registers the career text model for the environment (ADR 0009).</summary>
public static class CareerTextModelRegistration
{
    /// <summary>
    /// A configured OpenAI model is used in every environment. Without one, Development,
    /// LocalDev and Testing get the offline fake, and production registers nothing, so
    /// starting a run answers 503 <c>CareerModelUnavailable</c>. A half-configured model
    /// stays off and logs why at startup.
    /// </summary>
    public static IServiceCollection AddCareerTextModel(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var agent = new CareerAgentOptions();
        configuration.GetSection(CareerAgentOptions.SectionName).Bind(agent);
        var options = OpenAICareerTextModelOptions.FromConfiguration(configuration, agent.LeaseSeconds, out var problem);
        if (options != null)
        {
            services.AddSingleton(options);
            // The adapter applies its own timeout inside the lease.
            services.AddHttpClient<OpenAICareerTextModel>(client => client.Timeout = Timeout.InfiniteTimeSpan);
            services.AddTransient<ICareerTextModel>(sp => sp.GetRequiredService<OpenAICareerTextModel>());
            return services;
        }

        if (problem != null)
        {
            services.AddHostedService(sp => new CareerTextModelConfigurationWarning(
                problem, sp.GetRequiredService<ILogger<CareerTextModelConfigurationWarning>>()));
        }
        if (environment.IsDevelopment() || environment.IsEnvironment("LocalDev") || environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<ICareerTextModel, FakeCareerTextModel>();
        }
        return services;
    }
}

/// <summary>Logs once at startup why a partly configured career model stayed off.</summary>
public sealed class CareerTextModelConfigurationWarning : IHostedService
{
    private readonly string _problem;
    private readonly ILogger<CareerTextModelConfigurationWarning> _logger;

    public CareerTextModelConfigurationWarning(string problem, ILogger<CareerTextModelConfigurationWarning> logger)
    {
        _problem = problem;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("{Problem}", _problem);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
