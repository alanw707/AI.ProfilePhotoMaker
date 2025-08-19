using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using FluentAssertions;
using NBomber.Contracts;
using NBomber.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AI.ProfilePhotoMaker.API.Tests.Performance;

[Collection("LoadTesting")]
public class LoadTestingScenarios : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly IUserProfileRepository _repository;
    private List<UserProfile> _testUsers = new();

    public LoadTestingScenarios(ITestOutputHelper output)
    {
        _output = output;
        _repository = _serviceProvider.GetRequiredService<IUserProfileRepository>();
    }

    [Fact]
    public async Task LoadTest_UserProfileRepository_Should_Support200ConcurrentUsers()
    {
        // Arrange - Create test data for load testing
        _output.WriteLine("Setting up load test data...");
        _testUsers = await CreateTestDataAsync(userCount: 50, maxImagesPerUser: 200);
        
        var userIds = _testUsers.Select(u => u.UserId).ToArray();
        var random = new Random(42);

        // Create scenarios for different operations
        var lightProfileScenario = Scenario.Create("light_profile_load", async context =>
        {
            var userId = userIds[random.Next(userIds.Length)];
            var profile = await _repository.GetByUserIdLightAsync(userId);
            
            return profile != null ? Response.Ok() : Response.Fail("Profile not found");
        })
        .WithWeight(40) // 40% of requests
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromMinutes(2))
        );

        var profileStatsScenario = Scenario.Create("profile_stats", async context =>
        {
            var userId = userIds[random.Next(userIds.Length)];
            var stats = await _repository.GetUserProfileStatsAsync(userId);
            
            return stats != null ? Response.Ok() : Response.Fail("Stats not found");
        })
        .WithWeight(30) // 30% of requests
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 15, during: TimeSpan.FromMinutes(2))
        );

        var paginatedImagesScenario = Scenario.Create("paginated_images", async context =>
        {
            var userId = userIds[random.Next(userIds.Length)];
            var page = random.Next(1, 5);
            var pageSize = random.Next(10, 50);
            
            var images = await _repository.GetUserImagesPagedAsync(userId, page, pageSize);
            
            return images != null ? Response.Ok() : Response.Fail("Images not found");
        })
        .WithWeight(20) // 20% of requests
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromMinutes(2))
        );

        var countOperationsScenario = Scenario.Create("count_operations", async context =>
        {
            var userId = userIds[random.Next(userIds.Length)];
            
            // Mix of count operations
            var operations = new Func<Task<object>>[]
            {
                async () => await _repository.GetUserImageCountAsync(userId),
                async () => await _repository.GetUserOriginalUploadCountAsync(userId),
                async () => await _repository.HasProcessedImagesAsync(userId)
            };
            
            var operation = operations[random.Next(operations.Length)];
            var result = await operation();
            
            return Response.Ok();
        })
        .WithWeight(10) // 10% of requests
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromMinutes(2))
        );

        // Act - Run load test
        _output.WriteLine("Starting load test with 200+ concurrent users simulation...");
        
        var nbomberConfig = NBomberRunner
            .RegisterScenarios(
                lightProfileScenario,
                profileStatsScenario,
                paginatedImagesScenario,
                countOperationsScenario)
            .WithReportFolder("load_test_reports")
            .WithReportFileName($"repository_load_test_{DateTime.Now:yyyyMMdd_HHmmss}")
            .Build();

        var stats = NBomberRunner.Run(nbomberConfig);

        // Assert - Validate load test results
        _output.WriteLine("Load test completed. Analyzing results...");
        
        foreach (var scenarioStats in stats.AllScenarioStats)
        {
            _output.WriteLine($"\nScenario: {scenarioStats.ScenarioName}");
            _output.WriteLine($"- Requests: {scenarioStats.AllOkCount} OK, {scenarioStats.AllFailCount} Failed");
            _output.WriteLine($"- Success Rate: {(scenarioStats.AllOkCount / (double)(scenarioStats.AllOkCount + scenarioStats.AllFailCount) * 100):F1}%");
            _output.WriteLine($"- Average Response Time: {scenarioStats.Ok.Mean:F1}ms");
            _output.WriteLine($"- 95th Percentile: {scenarioStats.Ok.Percentile95:F1}ms");
            _output.WriteLine($"- 99th Percentile: {scenarioStats.Ok.Percentile99:F1}ms");
            
            // Assert performance targets
            scenarioStats.AllFailCount.Should().Be(0, $"All requests in {scenarioStats.ScenarioName} should succeed");
            scenarioStats.Ok.Mean.Should().BeLessThan(ApiResponseThreshold.TotalMilliseconds, 
                $"Average response time for {scenarioStats.ScenarioName} should meet targets");
            scenarioStats.Ok.Percentile95.Should().BeLessThan(500, 
                $"95th percentile for {scenarioStats.ScenarioName} should be under 500ms");
        }

        // Overall system performance validation
        var totalRequests = stats.AllScenarioStats.Sum(s => s.AllOkCount + s.AllFailCount);
        var totalSuccessful = stats.AllScenarioStats.Sum(s => s.AllOkCount);
        var overallSuccessRate = (totalSuccessful / (double)totalRequests * 100);
        
        _output.WriteLine($"\nOverall Load Test Results:");
        _output.WriteLine($"- Total Requests: {totalRequests}");
        _output.WriteLine($"- Success Rate: {overallSuccessRate:F1}%");
        _output.WriteLine($"- Duration: {stats.AllScenarioStats.First().Duration}");
        
        // Assert overall targets
        overallSuccessRate.Should().BeGreaterThan(99.0, "Overall success rate should be above 99%");
        totalSuccessful.Should().BeGreaterThan(200, "Should handle at least 200 successful concurrent operations");
    }

    [Fact] 
    public async Task StressTest_HighVolumeOperations_Should_MaintainPerformance()
    {
        // Arrange - Create larger dataset for stress testing
        _output.WriteLine("Setting up stress test data...");
        _testUsers = await CreateTestDataAsync(userCount: 20, maxImagesPerUser: 1000);
        
        var userIds = _testUsers.Select(u => u.UserId).ToArray();
        var random = new Random(42);

        // Create high-intensity scenario
        var highIntensityScenario = Scenario.Create("high_intensity_operations", async context =>
        {
            var userId = userIds[random.Next(userIds.Length)];
            
            // Simulate complex operations in sequence
            try
            {
                // 1. Get profile stats (complex aggregation)
                var stats = await _repository.GetUserProfileStatsAsync(userId);
                if (stats == null) return Response.Fail("Stats failed");

                // 2. Get paginated images (with filtering)
                var images = await _repository.GetUserImagesPagedAsync(userId, 1, 20);
                if (images == null) return Response.Fail("Pagination failed");

                // 3. Get count operations
                var totalCount = await _repository.GetUserImageCountAsync(userId);
                if (totalCount < 0) return Response.Fail("Count failed");

                return Response.Ok();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Operation failed for user {userId}: {ex.Message}");
                return Response.Fail($"Exception: {ex.Message}");
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromMinutes(3)) // High load
        );

        // Act - Run stress test
        _output.WriteLine("Starting stress test with high-volume operations...");
        
        var nbomberConfig = NBomberRunner
            .RegisterScenarios(highIntensityScenario)
            .WithReportFolder("stress_test_reports")
            .WithReportFileName($"repository_stress_test_{DateTime.Now:yyyyMMdd_HHmmss}")
            .Build();

        var stats = NBomberRunner.Run(nbomberConfig);

        // Assert - Validate stress test results
        var scenarioStats = stats.AllScenarioStats.First();
        
        _output.WriteLine($"\nStress Test Results:");
        _output.WriteLine($"- Total Requests: {scenarioStats.AllOkCount + scenarioStats.AllFailCount}");
        _output.WriteLine($"- Successful Requests: {scenarioStats.AllOkCount}");
        _output.WriteLine($"- Failed Requests: {scenarioStats.AllFailCount}");
        _output.WriteLine($"- Success Rate: {(scenarioStats.AllOkCount / (double)(scenarioStats.AllOkCount + scenarioStats.AllFailCount) * 100):F1}%");
        _output.WriteLine($"- Average Response Time: {scenarioStats.Ok.Mean:F1}ms");
        _output.WriteLine($"- Maximum Response Time: {scenarioStats.Ok.Max:F1}ms");
        _output.WriteLine($"- 99th Percentile: {scenarioStats.Ok.Percentile99:F1}ms");

        // Assert stress test targets
        var successRate = (scenarioStats.AllOkCount / (double)(scenarioStats.AllOkCount + scenarioStats.AllFailCount) * 100);
        successRate.Should().BeGreaterThan(95.0, "Success rate should remain above 95% under stress");
        scenarioStats.Ok.Mean.Should().BeLessThan(500, "Average response time should remain reasonable under stress");
        scenarioStats.Ok.Percentile99.Should().BeLessThan(1000, "99th percentile should remain under 1 second");
    }

    [Fact]
    public async Task MemoryLeakTest_ContinuousOperations_Should_NotLeakMemory()
    {
        // Arrange
        _output.WriteLine("Setting up memory leak test...");
        _testUsers = await CreateTestDataAsync(userCount: 10, maxImagesPerUser: 100);
        
        var userIds = _testUsers.Select(u => u.UserId).ToArray();
        var random = new Random(42);
        var memoryMeasurements = new List<long>();

        // Create continuous memory monitoring scenario
        var memoryTestScenario = Scenario.Create("memory_leak_test", async context =>
        {
            var userId = userIds[random.Next(userIds.Length)];
            
            // Perform various operations
            var profile = await _repository.GetByUserIdLightAsync(userId);
            var stats = await _repository.GetUserProfileStatsAsync(userId);
            var images = await _repository.GetUserImagesPagedAsync(userId, 1, 10);
            
            // Measure memory every 100 requests
            if (context.InvocationNumber % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var currentMemory = GC.GetTotalMemory(false);
                memoryMeasurements.Add(currentMemory);
                
                _output.WriteLine($"Memory at request {context.InvocationNumber}: {currentMemory / 1024 / 1024:F1}MB");
            }

            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 5, during: TimeSpan.FromMinutes(5)) // Constant load for 5 minutes
        );

        // Act - Run memory leak test
        var initialMemory = GC.GetTotalMemory(true);
        _output.WriteLine($"Initial memory: {initialMemory / 1024 / 1024:F1}MB");
        
        var nbomberConfig = NBomberRunner
            .RegisterScenarios(memoryTestScenario)
            .WithReportFolder("memory_test_reports")
            .WithReportFileName($"repository_memory_test_{DateTime.Now:yyyyMMdd_HHmmss}")
            .Build();

        var stats = NBomberRunner.Run(nbomberConfig);

        var finalMemory = GC.GetTotalMemory(true);
        _output.WriteLine($"Final memory: {finalMemory / 1024 / 1024:F1}MB");

        // Assert - Validate no significant memory leaks
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreasePercentage = (memoryIncrease / (double)initialMemory * 100);
        
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024:F1}MB ({memoryIncreasePercentage:F1}%)");
        
        // Memory should not increase by more than 50% for continuous operations
        memoryIncreasePercentage.Should().BeLessThan(50, "Memory usage should not increase significantly during continuous operations");
        
        // Analyze memory trend
        if (memoryMeasurements.Count > 5)
        {
            var firstMeasurements = memoryMeasurements.Take(3).Average();
            var lastMeasurements = memoryMeasurements.TakeLast(3).Average();
            var trendIncrease = ((lastMeasurements - firstMeasurements) / firstMeasurements * 100);
            
            _output.WriteLine($"Memory trend increase: {trendIncrease:F1}%");
            trendIncrease.Should().BeLessThan(30, "Memory trend should not show significant growth over time");
        }

        var scenarioStats = stats.AllScenarioStats.First();
        _output.WriteLine($"Memory test completed: {scenarioStats.AllOkCount} operations, {scenarioStats.Ok.Mean:F1}ms average response time");
    }

    public override void Dispose()
    {
        try
        {
            // Clean up test data
            if (_testUsers.Any())
            {
                var userIds = _testUsers.Select(u => u.Id).ToList();
                var imagesToDelete = _context.ProcessedImages.Where(i => userIds.Contains(i.UserProfileId)).ToList();
                var usersToDelete = _context.UserProfiles.Where(u => userIds.Contains(u.Id)).ToList();
                
                if (imagesToDelete.Any())
                {
                    _context.ProcessedImages.RemoveRange(imagesToDelete);
                }
                
                if (usersToDelete.Any())
                {
                    _context.UserProfiles.RemoveRange(usersToDelete);
                }
                
                _context.SaveChanges();
                _output.WriteLine($"Cleaned up {usersToDelete.Count} test users and {imagesToDelete.Count} test images");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Error during load test cleanup: {ex.Message}");
        }
        
        base.Dispose();
    }
}