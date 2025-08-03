using System.Diagnostics;
using System.Net;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.Health;

/// <summary>
/// External dependencies health check service implementation
/// </summary>
public class DependencyHealthService : IDependencyHealthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DependencyHealthService> _logger;

    // Define the external dependencies we want to monitor
    private readonly Dictionary<string, DependencyConfig> _dependencies;

    public DependencyHealthService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DependencyHealthService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        // Configure timeout for dependency checks
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        // Initialize dependency configurations
        _dependencies = InitializeDependencies();
    }

    public async Task<Dictionary<string, DependencyStatusDto>> CheckDependenciesAsync()
    {
        var results = new Dictionary<string, DependencyStatusDto>();

        // Check all dependencies in parallel
        var tasks = _dependencies.Select(async dep =>
        {
            var status = await CheckDependencyAsync(dep.Key);
            return new KeyValuePair<string, DependencyStatusDto>(dep.Key, status);
        });

        var completedTasks = await Task.WhenAll(tasks);
        
        foreach (var result in completedTasks)
        {
            results[result.Key] = result.Value;
        }

        return results;
    }

    public async Task<DependencyStatusDto> CheckDependencyAsync(string dependencyName)
    {
        if (!_dependencies.TryGetValue(dependencyName, out var config))
        {
            return new DependencyStatusDto
            {
                Name = dependencyName,
                Status = "Unknown",
                Error = "Dependency not configured"
            };
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            switch (config.Type)
            {
                case DependencyType.Http:
                    return await CheckHttpDependencyAsync(config, stopwatch);
                
                case DependencyType.Service:
                    return await CheckServiceDependencyAsync(config, stopwatch);
                
                default:
                    stopwatch.Stop();
                    return new DependencyStatusDto
                    {
                        Name = dependencyName,
                        Status = "Unknown",
                        ResponseTime = stopwatch.ElapsedMilliseconds,
                        Error = "Unknown dependency type"
                    };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Failed to check dependency {DependencyName}", dependencyName);
            
            return new DependencyStatusDto
            {
                Name = dependencyName,
                Status = "Unhealthy",
                ResponseTime = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private Dictionary<string, DependencyConfig> InitializeDependencies()
    {
        var dependencies = new Dictionary<string, DependencyConfig>();

        // Replicate API
        var replicateApiToken = _configuration["Replicate:ApiToken"];
        if (!string.IsNullOrEmpty(replicateApiToken))
        {
            dependencies["replicate"] = new DependencyConfig
            {
                Name = "Replicate API",
                Type = DependencyType.Http,
                Url = "https://api.replicate.com/v1/models",
                Required = true,
                Headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Token {replicateApiToken}"
                }
            };
        }

        // Stripe API (if configured)
        var stripeApiKey = _configuration["Stripe:SecretKey"];
        if (!string.IsNullOrEmpty(stripeApiKey))
        {
            dependencies["stripe"] = new DependencyConfig
            {
                Name = "Stripe API",
                Type = DependencyType.Http,
                Url = "https://api.stripe.com/v1/account",
                Required = false,
                Headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {stripeApiKey}"
                }
            };
        }

        // Google OAuth (if configured)
        var googleClientId = _configuration["Authentication:Google:ClientId"];
        if (!string.IsNullOrEmpty(googleClientId))
        {
            dependencies["google-oauth"] = new DependencyConfig
            {
                Name = "Google OAuth",
                Type = DependencyType.Http,
                Url = "https://www.googleapis.com/oauth2/v2/userinfo",
                Required = false,
                Method = "GET"
            };
        }

        // Azure Blob Storage (if configured)
        var azureStorageConnectionString = _configuration.GetConnectionString("AzureStorage") ?? 
                                         _configuration["AzureStorage:ConnectionString"];
        if (!string.IsNullOrEmpty(azureStorageConnectionString) && 
            !azureStorageConnectionString.Contains("UseDevelopmentStorage=true"))
        {
            // Extract account name from connection string for health check
            try
            {
                if (azureStorageConnectionString.Contains("AccountName="))
                {
                    var accountName = azureStorageConnectionString.Split("AccountName=")[1].Split(';')[0];
                    dependencies["azure-storage"] = new DependencyConfig
                    {
                        Name = "Azure Blob Storage",
                        Type = DependencyType.Service,
                        Url = $"https://{accountName}.blob.core.windows.net",
                        Required = true,
                        Method = "HEAD"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse Azure Storage connection string for health check");
            }
        }

        return dependencies;
    }

    private async Task<DependencyStatusDto> CheckHttpDependencyAsync(DependencyConfig config, Stopwatch stopwatch)
    {
        try
        {
            using var request = new HttpRequestMessage(
                config.Method.ToUpper() switch
                {
                    "POST" => HttpMethod.Post,
                    "PUT" => HttpMethod.Put,
                    "DELETE" => HttpMethod.Delete,
                    "HEAD" => HttpMethod.Head,
                    _ => HttpMethod.Get
                }, 
                config.Url);

            // Add headers
            foreach (var header in config.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            using var response = await _httpClient.SendAsync(request);
            stopwatch.Stop();

            var isHealthy = response.IsSuccessStatusCode || 
                           response.StatusCode == HttpStatusCode.Unauthorized || // Expected for some APIs without proper auth
                           response.StatusCode == HttpStatusCode.Forbidden;     // Expected for some protected endpoints

            return new DependencyStatusDto
            {
                Name = config.Name,
                Status = isHealthy ? "Healthy" : "Unhealthy",
                ResponseTime = stopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["statusDescription"] = response.StatusCode.ToString(),
                    ["url"] = config.Url,
                    ["method"] = config.Method
                },
                Error = isHealthy ? null : $"HTTP {(int)response.StatusCode} {response.StatusCode}"
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            return new DependencyStatusDto
            {
                Name = config.Name,
                Status = "Unhealthy",
                ResponseTime = stopwatch.ElapsedMilliseconds,
                Error = "Request timeout"
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new DependencyStatusDto
            {
                Name = config.Name,
                Status = "Unhealthy",
                ResponseTime = stopwatch.ElapsedMilliseconds,
                Error = $"HTTP request failed: {ex.Message}"
            };
        }
    }

    private async Task<DependencyStatusDto> CheckServiceDependencyAsync(DependencyConfig config, Stopwatch stopwatch)
    {
        // For service dependencies, we'll do a simple HTTP head request to check availability
        return await CheckHttpDependencyAsync(config, stopwatch);
    }

    private class DependencyConfig
    {
        public string Name { get; set; } = string.Empty;
        public DependencyType Type { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public bool Required { get; set; } = true;
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    private enum DependencyType
    {
        Http,
        Service
    }
}