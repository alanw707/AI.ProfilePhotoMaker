using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AI.ProfilePhotoMaker.API.Tests.Performance;

public abstract class PerformanceTestBase : IDisposable
{
    protected readonly IFixture _fixture;
    protected readonly ApplicationDbContext _context;
    protected readonly IServiceProvider _serviceProvider;
    protected readonly ILogger<PerformanceTestBase> _logger;
    
    // Performance thresholds (configurable)
    protected readonly TimeSpan SimpleQueryThreshold = TimeSpan.FromMilliseconds(100);
    protected readonly TimeSpan ComplexQueryThreshold = TimeSpan.FromMilliseconds(150);
    protected readonly TimeSpan ApiResponseThreshold = TimeSpan.FromMilliseconds(200);
    protected readonly long MaxMemoryUsageBytes = 160 * 1024 * 1024; // 160MB
    protected readonly int ExpectedMemoryReduction = 50; // 50% reduction minimum

    protected PerformanceTestBase()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        // Setup services and database context
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        
        _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        _logger = _serviceProvider.GetRequiredService<ILogger<PerformanceTestBase>>();

        // Ensure database is created and seeded
        InitializeDatabase();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Use SQL Server for realistic performance testing
        var connectionString = GetConnectionString();
        
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure();
                sqlOptions.CommandTimeout(30);
            }));

        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Configure test configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
    }

    protected virtual string GetConnectionString()
    {
        // Use test database - separate from development database
        return "Server=(localdb)\\mssqllocaldb;Database=AI.ProfilePhotoMaker.Performance.Tests;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
    }

    protected virtual void InitializeDatabase()
    {
        try
        {
            // Ensure database exists and is migrated
            _context.Database.EnsureCreated();
            
            // Clear existing test data
            _context.ProcessedImages.RemoveRange(_context.ProcessedImages);
            _context.UserProfiles.RemoveRange(_context.UserProfiles);
            _context.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize test database, falling back to in-memory");
            // Fallback to in-memory database for CI/CD environments
            UseFallbackInMemoryDatabase();
        }
    }

    private void UseFallbackInMemoryDatabase()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var newServiceProvider = services.BuildServiceProvider();
        var newContext = newServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Replace the current context
        _context?.Dispose();
        typeof(PerformanceTestBase).GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(this, newContext);
        
        if (_serviceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }
        typeof(PerformanceTestBase).GetField("_serviceProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(this, newServiceProvider);
    }

    /// <summary>
    /// Creates test users with varying numbers of images for performance testing
    /// </summary>
    protected async Task<List<UserProfile>> CreateTestDataAsync(int userCount = 10, int maxImagesPerUser = 500)
    {
        var users = new List<UserProfile>();
        var random = new Random(42); // Fixed seed for reproducible results

        for (int i = 0; i < userCount; i++)
        {
            var userId = $"test-user-{i + 1}";
            var user = new UserProfile
            {
                UserId = userId,
                FirstName = _fixture.Create<string>(),
                LastName = _fixture.Create<string>(),
                Gender = _fixture.Create<string>(),
                Ethnicity = _fixture.Create<string>(),
                Credits = _fixture.Create<int>(),
                PurchasedCredits = _fixture.Create<int>(),
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
            };

            _context.UserProfiles.Add(user);
            await _context.SaveChangesAsync();

            // Create varying numbers of images (some users with many, some with few)
            var imageCount = i < 2 ? maxImagesPerUser : random.Next(5, 100);
            var images = new List<ProcessedImage>();

            for (int j = 0; j < imageCount; j++)
            {
                var isOriginal = j < imageCount * 0.3; // 30% original uploads
                var image = new ProcessedImage
                {
                    UserProfileId = user.Id,
                    OriginalImageUrl = _fixture.Create<string>(),
                    ProcessedImageUrl = _fixture.Create<string>(),
                    Style = GetRandomStyle(random),
                    IsOriginalUpload = isOriginal,
                    IsGenerated = !isOriginal,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 180))
                };
                images.Add(image);
            }

            if (images.Any())
            {
                _context.ProcessedImages.AddRange(images);
                await _context.SaveChangesAsync();
            }

            user.ProcessedImages = images;
            users.Add(user);
        }

        _logger.LogInformation("Created {UserCount} test users with total {ImageCount} images", 
            userCount, users.Sum(u => u.ProcessedImages.Count));

        return users;
    }

    private static string GetRandomStyle(Random random)
    {
        var styles = new[] { "corporate", "executive", "consultant", "linkedin", "legal", "medical", "author", "entrepreneur", "startup", "tech-professional" };
        return styles[random.Next(styles.Length)];
    }

    /// <summary>
    /// Measures execution time of a function
    /// </summary>
    protected async Task<(T Result, TimeSpan ElapsedTime, long MemoryBefore, long MemoryAfter)> MeasurePerformanceAsync<T>(Func<Task<T>> operation)
    {
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        var result = await operation();

        stopwatch.Stop();
        var memoryAfter = GC.GetTotalMemory(false);

        return (result, stopwatch.Elapsed, memoryBefore, memoryAfter);
    }

    /// <summary>
    /// Measures execution time of a synchronous function
    /// </summary>
    protected (T Result, TimeSpan ElapsedTime, long MemoryBefore, long MemoryAfter) MeasurePerformance<T>(Func<T> operation)
    {
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        var result = operation();

        stopwatch.Stop();
        var memoryAfter = GC.GetTotalMemory(false);

        return (result, stopwatch.Elapsed, memoryBefore, memoryAfter);
    }

    /// <summary>
    /// Measures average performance over multiple runs
    /// </summary>
    protected async Task<(TimeSpan AverageTime, long AverageMemoryIncrease, int SuccessfulRuns)> MeasureAveragePerformanceAsync<T>(
        Func<Task<T>> operation, 
        int runs = 10,
        string operationName = "Operation")
    {
        var times = new List<TimeSpan>();
        var memoryIncreases = new List<long>();
        var successfulRuns = 0;

        _logger.LogInformation("Starting performance measurement for {Operation} over {Runs} runs", operationName, runs);

        for (int i = 0; i < runs; i++)
        {
            try
            {
                var (result, elapsed, memoryBefore, memoryAfter) = await MeasurePerformanceAsync(operation);
                times.Add(elapsed);
                memoryIncreases.Add(memoryAfter - memoryBefore);
                successfulRuns++;

                if (i % (runs / 10) == 0) // Log every 10%
                {
                    _logger.LogInformation("Completed {Current}/{Total} runs, last run: {Time}ms", 
                        i + 1, runs, elapsed.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Run {Run} failed for {Operation}", i + 1, operationName);
            }
        }

        var averageTime = TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
        var averageMemoryIncrease = (long)memoryIncreases.Average();

        _logger.LogInformation("Performance results for {Operation}: Average time: {Time}ms, Average memory increase: {Memory} bytes, Success rate: {SuccessRate}%",
            operationName, averageTime.TotalMilliseconds, averageMemoryIncrease, (successfulRuns * 100.0 / runs));

        return (averageTime, averageMemoryIncrease, successfulRuns);
    }

    /// <summary>
    /// Validates that performance metrics meet the defined targets
    /// </summary>
    protected void AssertPerformanceTargets(
        TimeSpan actualTime, 
        TimeSpan expectedTime, 
        long memoryIncrease = 0,
        string operationName = "Operation")
    {
        _logger.LogInformation("Performance validation for {Operation}: {ActualTime}ms vs {ExpectedTime}ms threshold, Memory increase: {Memory} bytes",
            operationName, actualTime.TotalMilliseconds, expectedTime.TotalMilliseconds, memoryIncrease);

        if (actualTime > expectedTime)
        {
            throw new Exception($"{operationName} performance target not met: {actualTime.TotalMilliseconds}ms > {expectedTime.TotalMilliseconds}ms threshold");
        }

        if (memoryIncrease > MaxMemoryUsageBytes)
        {
            throw new Exception($"{operationName} memory usage target not met: {memoryIncrease} bytes > {MaxMemoryUsageBytes} bytes threshold");
        }
    }

    public virtual void Dispose()
    {
        try
        {
            // Clean up test data
            _context?.ProcessedImages.RemoveRange(_context.ProcessedImages);
            _context?.UserProfiles.RemoveRange(_context.UserProfiles);
            _context?.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during test cleanup");
        }

        _context?.Dispose();
        if (_serviceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }
    }
}

/// <summary>
/// Performance test results for reporting and tracking
/// </summary>
public class PerformanceTestResult
{
    public string TestName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public TimeSpan Threshold { get; set; }
    public long MemoryUsed { get; set; }
    public bool PassedTimeTarget { get; set; }
    public bool PassedMemoryTarget { get; set; }
    public int DataSetSize { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime TestDateTime { get; set; } = DateTime.UtcNow;
}