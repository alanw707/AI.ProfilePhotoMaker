using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Environments;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AutoFixture;
using Xunit;
using Xunit.Abstractions;

namespace AI.ProfilePhotoMaker.API.Tests.Performance;

public class BenchmarkComparisonRunner
{
    private readonly ITestOutputHelper _output;

    public BenchmarkComparisonRunner(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RunBenchmarkComparisons()
    {
        _output.WriteLine("Starting benchmark comparisons between optimized and unoptimized methods...");
        
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithRuntime(CoreRuntime.Net80))
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        var summary = BenchmarkRunner.Run<RepositoryMethodBenchmarks>(config);
        
        // Output results to test output
        _output.WriteLine("\nBenchmark Results Summary:");
        foreach (var report in summary.Reports)
        {
            _output.WriteLine($"Method: {report.BenchmarkCase.Descriptor.WorkloadMethod.Name}");
            _output.WriteLine($"  Mean: {report.ResultStatistics?.Mean:F2} ns");
            _output.WriteLine($"  Median: {report.ResultStatistics?.Median:F2} ns");
            _output.WriteLine($"  Allocated: {report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase):F0} bytes");
        }
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class RepositoryMethodBenchmarks : IDisposable
{
    private ApplicationDbContext _context = null!;
    private IUserProfileRepository _repository = null!;
    private IServiceProvider _serviceProvider = null!;
    private List<UserProfile> _testUsers = new();
    private readonly IFixture _fixture;

    public RepositoryMethodBenchmarks()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        // Setup services
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemory($"BenchmarkDb_{Guid.NewGuid()}"));
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        _repository = _serviceProvider.GetRequiredService<IUserProfileRepository>();

        // Create test data for benchmarking
        await CreateBenchmarkTestData();
    }

    private async Task CreateBenchmarkTestData()
    {
        var random = new Random(42); // Fixed seed for consistent benchmarking

        // Create users with varying amounts of data
        for (int i = 0; i < 10; i++)
        {
            var userId = $"benchmark-user-{i + 1}";
            var user = new UserProfile
            {
                UserId = userId,
                FirstName = $"User{i}",
                LastName = $"Test{i}",
                Gender = "Test",
                Ethnicity = "Test",
                Credits = 100,
                PurchasedCredits = 50,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
            };

            _context.UserProfiles.Add(user);
            await _context.SaveChangesAsync();

            // Create different amounts of images for different users
            var imageCount = i < 2 ? 200 : (i < 5 ? 50 : 20); // Some heavy users, some light
            var images = new List<ProcessedImage>();

            for (int j = 0; j < imageCount; j++)
            {
                var isOriginal = j < imageCount * 0.3;
                var image = new ProcessedImage
                {
                    UserProfileId = user.Id,
                    OriginalImageUrl = $"original-{i}-{j}.jpg",
                    ProcessedImageUrl = $"processed-{i}-{j}.jpg",
                    Style = GetBenchmarkStyle(j),
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
            _testUsers.Add(user);
        }
    }

    private static string GetBenchmarkStyle(int index)
    {
        var styles = new[] { "corporate", "executive", "consultant", "linkedin" };
        return styles[index % styles.Length];
    }

    // User to benchmark against (user with many images)
    private UserProfile HeavyUser => _testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();
    private UserProfile LightUser => _testUsers.OrderBy(u => u.ProcessedImages.Count).First();

    #region Profile Loading Benchmarks

    [Benchmark(Baseline = true)]
    public async Task<UserProfile?> GetByUserIdAsync_EagerLoading()
    {
        return await _repository.GetByUserIdAsync(HeavyUser.UserId);
    }

    [Benchmark]
    public async Task<UserProfile?> GetByUserIdLightAsync_Optimized()
    {
        return await _repository.GetByUserIdLightAsync(HeavyUser.UserId);
    }

    [Benchmark]
    public async Task<Models.DTOs.UserProfileStatsDto?> GetUserProfileStatsAsync_Optimized()
    {
        return await _repository.GetUserProfileStatsAsync(HeavyUser.UserId);
    }

    [Benchmark]
    public async Task<Models.DTOs.UserProfileWithRecentImagesDto?> GetProfileWithRecentImagesAsync_Optimized()
    {
        return await _repository.GetProfileWithRecentImagesAsync(HeavyUser.UserId, 10);
    }

    #endregion

    #region Pagination Benchmarks

    [Benchmark]
    public async Task<Models.DTOs.PagedResult<Models.DTOs.ProcessedImageDto>> GetUserImagesPagedAsync_SmallPage()
    {
        return await _repository.GetUserImagesPagedAsync(HeavyUser.UserId, 1, 10);
    }

    [Benchmark]
    public async Task<Models.DTOs.PagedResult<Models.DTOs.ProcessedImageDto>> GetUserImagesPagedAsync_MediumPage()
    {
        return await _repository.GetUserImagesPagedAsync(HeavyUser.UserId, 1, 20);
    }

    [Benchmark]
    public async Task<Models.DTOs.PagedResult<Models.DTOs.ProcessedImageDto>> GetUserImagesPagedAsync_LargePage()
    {
        return await _repository.GetUserImagesPagedAsync(HeavyUser.UserId, 1, 50);
    }

    [Benchmark]
    public async Task<Models.DTOs.PagedResult<Models.DTOs.ProcessedImageDto>> GetUserImagesByStyleAsync_Filtered()
    {
        return await _repository.GetUserImagesByStyleAsync(HeavyUser.UserId, "corporate", 1, 20);
    }

    #endregion

    #region Count Operations Benchmarks

    [Benchmark]
    public async Task<int> GetUserImageCountAsync_Optimized()
    {
        return await _repository.GetUserImageCountAsync(HeavyUser.UserId);
    }

    [Benchmark]
    public async Task<int> GetUserOriginalUploadCountAsync_Optimized()
    {
        return await _repository.GetUserOriginalUploadCountAsync(HeavyUser.UserId);
    }

    [Benchmark]
    public async Task<int> GetUserGeneratedImageCountAsync_Optimized()
    {
        return await _repository.GetUserGeneratedImageCountAsync(HeavyUser.UserId);
    }

    [Benchmark]
    public async Task<bool> HasProcessedImagesAsync_Optimized()
    {
        return await _repository.HasProcessedImagesAsync(HeavyUser.UserId);
    }

    [Benchmark]
    public async Task<bool> HasOriginalUploadsAsync_Optimized()
    {
        return await _repository.HasOriginalUploadsAsync(HeavyUser.UserId, 5);
    }

    #endregion

    #region Bulk Operations Benchmarks

    [Benchmark]
    public async Task<List<ProcessedImage>> GetUserProcessedImagesAsync_FilteredGenerated()
    {
        return await _repository.GetUserProcessedImagesAsync(HeavyUser.UserId, includeGenerated: true, includeOriginal: false);
    }

    [Benchmark]
    public async Task<List<ProcessedImage>> GetUserProcessedImagesAsync_FilteredOriginal()
    {
        return await _repository.GetUserProcessedImagesAsync(HeavyUser.UserId, includeGenerated: false, includeOriginal: true);
    }

    #endregion

    #region Data Size Comparison Benchmarks

    [Benchmark]
    public async Task<Models.DTOs.UserProfileStatsDto?> GetUserProfileStatsAsync_HeavyUser()
    {
        return await _repository.GetUserProfileStatsAsync(HeavyUser.UserId);
    }

    [Benchmark]
    public async Task<Models.DTOs.UserProfileStatsDto?> GetUserProfileStatsAsync_LightUser()
    {
        return await _repository.GetUserProfileStatsAsync(LightUser.UserId);
    }

    [Benchmark]
    public async Task<Models.DTOs.PagedResult<Models.DTOs.ProcessedImageDto>> GetUserImagesPagedAsync_HeavyUser()
    {
        return await _repository.GetUserImagesPagedAsync(HeavyUser.UserId, 1, 20);
    }

    [Benchmark]
    public async Task<Models.DTOs.PagedResult<Models.DTOs.ProcessedImageDto>> GetUserImagesPagedAsync_LightUser()
    {
        return await _repository.GetUserImagesPagedAsync(LightUser.UserId, 1, 20);
    }

    #endregion

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Dispose();
    }

    public void Dispose()
    {
        try
        {
            _context?.ProcessedImages.RemoveRange(_context.ProcessedImages);
            _context?.UserProfiles.RemoveRange(_context.UserProfiles);
            _context?.SaveChanges();
        }
        catch
        {
            // Ignore cleanup errors during benchmarking
        }

        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// Simplified benchmark for quick performance comparisons
/// </summary>
[Collection("QuickBenchmark")]
public class QuickBenchmarkTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly IUserProfileRepository _repository;

    public QuickBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _repository = _serviceProvider.GetRequiredService<IUserProfileRepository>();
    }

    [Fact]
    public async Task QuickBenchmark_CompareOptimizedVsUnoptimizedMethods()
    {
        // Arrange
        var testUsers = await CreateTestDataAsync(5, 100);
        var heavyUser = testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();

        _output.WriteLine($"Running quick benchmark with user having {heavyUser.ProcessedImages.Count} images");

        // Test scenarios
        var benchmarks = new Dictionary<string, Func<Task<object>>>
        {
            ["GetByUserIdAsync (Eager Loading - SLOW)"] = async () => await _repository.GetByUserIdAsync(heavyUser.UserId),
            ["GetByUserIdLightAsync (Optimized - FAST)"] = async () => await _repository.GetByUserIdLightAsync(heavyUser.UserId),
            ["GetUserProfileStatsAsync (Aggregated - SMART)"] = async () => await _repository.GetUserProfileStatsAsync(heavyUser.UserId),
            ["GetUserImagesPagedAsync (Paginated - EFFICIENT)"] = async () => await _repository.GetUserImagesPagedAsync(heavyUser.UserId, 1, 20),
            ["GetUserImageCountAsync (Count Only - MINIMAL)"] = async () => await _repository.GetUserImageCountAsync(heavyUser.UserId)
        };

        var results = new List<(string Name, TimeSpan Time, long Memory, bool Success)>();

        // Run benchmarks
        foreach (var (name, operation) in benchmarks)
        {
            try
            {
                var (result, elapsed, memoryBefore, memoryAfter) = await MeasurePerformanceAsync(operation);
                var memoryUsed = memoryAfter - memoryBefore;
                results.Add((name, elapsed, memoryUsed, true));
                
                _output.WriteLine($"{name}: {elapsed.TotalMilliseconds:F2}ms, {memoryUsed} bytes");
            }
            catch (Exception ex)
            {
                results.Add((name, TimeSpan.Zero, 0, false));
                _output.WriteLine($"{name}: FAILED - {ex.Message}");
            }
        }

        // Analyze results
        var successfulResults = results.Where(r => r.Success).ToList();
        
        if (successfulResults.Any())
        {
            var fastestTime = successfulResults.Min(r => r.Time);
            var slowestTime = successfulResults.Max(r => r.Time);
            var lowestMemory = successfulResults.Min(r => r.Memory);
            var highestMemory = successfulResults.Max(r => r.Memory);

            _output.WriteLine($"\nBenchmark Analysis:");
            _output.WriteLine($"- Fastest operation: {fastestTime.TotalMilliseconds:F2}ms");
            _output.WriteLine($"- Slowest operation: {slowestTime.TotalMilliseconds:F2}ms");
            _output.WriteLine($"- Performance range: {(slowestTime.TotalMilliseconds / fastestTime.TotalMilliseconds):F1}x difference");
            _output.WriteLine($"- Memory range: {lowestMemory} to {highestMemory} bytes");

            // Assert that optimized methods are significantly faster
            var eagerLoadingResult = results.FirstOrDefault(r => r.Name.Contains("Eager Loading"));
            var optimizedResults = results.Where(r => r.Name.Contains("Optimized") || r.Name.Contains("FAST") || r.Name.Contains("SMART")).ToList();

            if (eagerLoadingResult.Success && optimizedResults.Any(r => r.Success))
            {
                var bestOptimizedTime = optimizedResults.Where(r => r.Success).Min(r => r.Time);
                var improvement = ((eagerLoadingResult.Time.TotalMilliseconds - bestOptimizedTime.TotalMilliseconds) 
                                 / eagerLoadingResult.Time.TotalMilliseconds * 100);
                
                _output.WriteLine($"- Performance improvement: {improvement:F1}% faster than eager loading");
                improvement.Should().BeGreaterThan(30, "Optimized methods should be at least 30% faster");
            }
        }

        // All benchmarks should complete successfully
        results.Count(r => r.Success).Should().Be(benchmarks.Count, "All benchmark operations should succeed");
    }
}