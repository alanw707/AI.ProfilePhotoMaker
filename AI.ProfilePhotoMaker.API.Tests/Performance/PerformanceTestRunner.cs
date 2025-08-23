using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AI.ProfilePhotoMaker.API.Tests.Performance;

[Collection("PerformanceRunner")]
public class PerformanceTestRunner : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly IUserProfileRepository _repository;
    private readonly List<PerformanceTestResult> _testResults = new();

    public PerformanceTestRunner(ITestOutputHelper output)
    {
        _output = output;
        _repository = _serviceProvider.GetRequiredService<IUserProfileRepository>();
    }

    [Fact]
    public async Task ExecuteFullPerformanceTestSuite()
    {
        _output.WriteLine("=== AI Profile Photo Maker - Database Performance Test Suite ===");
        _output.WriteLine($"Started: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _output.WriteLine("");

        try
        {
            // Setup test data
            await SetupPerformanceTestData();

            // Execute all performance tests
            await RunQueryPerformanceTests();
            await RunMemoryUsageTests();
            await RunPaginationTests();
            await RunIndexEffectivenessTests();
            await RunConcurrentAccessTests();
            await RunN1QueryValidationTests();

            // Generate comprehensive report
            await GeneratePerformanceReport();

            // Validate overall performance targets
            ValidateOverallPerformanceTargets();

            _output.WriteLine("");
            _output.WriteLine("=== Performance Test Suite Completed Successfully ===");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Performance test suite failed: {ex.Message}");
            _output.WriteLine(ex.StackTrace);
            throw;
        }
    }

    #region Test Data Setup

    private async Task SetupPerformanceTestData()
    {
        _output.WriteLine("Setting up comprehensive performance test data...");

        // Create varied test scenarios
        var testScenarios = new[]
        {
            (users: 3, maxImages: 1000, description: "Heavy users (1000+ images each)"),
            (users: 10, maxImages: 200, description: "Medium users (100-200 images each)"),
            (users: 20, maxImages: 50, description: "Light users (20-50 images each)"),
            (users: 5, maxImages: 10, description: "New users (5-10 images each)")
        };

        int totalUsers = 0;
        int totalImages = 0;

        foreach (var (users, maxImages, description) in testScenarios)
        {
            var batchUsers = await CreateTestDataAsync(users, maxImages);
            totalUsers += batchUsers.Count;
            totalImages += batchUsers.Sum(u => u.ProcessedImages.Count);

            _output.WriteLine($"  Created {description}: {batchUsers.Count} users, {batchUsers.Sum(u => u.ProcessedImages.Count)} images");
        }

        _output.WriteLine($"Total test data: {totalUsers} users, {totalImages} images");
        _output.WriteLine("");
    }

    #endregion

    #region Query Performance Tests

    private async Task RunQueryPerformanceTests()
    {
        _output.WriteLine("=== Query Performance Tests ===");

        var testUsers = await _context.UserProfiles.Include(u => u.ProcessedImages).ToListAsync();
        var heavyUser = testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();
        var mediumUser = testUsers.Where(u => u.ProcessedImages.Count > 50 && u.ProcessedImages.Count < 500).First();

        // Test core repository methods
        var queryTests = new Dictionary<string, Func<Task<object?>>>
        {
            ["GetByUserIdLightAsync"] = async () => await _repository.GetByUserIdLightAsync(heavyUser.UserId),
            ["GetUserProfileStatsAsync"] = async () => await _repository.GetUserProfileStatsAsync(heavyUser.UserId),
            ["GetProfileWithRecentImagesAsync"] = async () => await _repository.GetProfileWithRecentImagesAsync(heavyUser.UserId, 10),
            ["GetUserImagesPagedAsync"] = async () => await _repository.GetUserImagesPagedAsync(heavyUser.UserId, 1, 20),
            ["GetUserImagesByStyleAsync"] = async () => await _repository.GetUserImagesByStyleAsync(heavyUser.UserId, "corporate", 1, 20),
            ["GetUserImageCountAsync"] = async () => await _repository.GetUserImageCountAsync(mediumUser.UserId),
            ["GetUserOriginalUploadCountAsync"] = async () => await _repository.GetUserOriginalUploadCountAsync(mediumUser.UserId),
            ["HasProcessedImagesAsync"] = async () => await _repository.HasProcessedImagesAsync(mediumUser.UserId)
        };

        foreach (var (methodName, method) in queryTests)
        {
            var (averageTime, averageMemory, successfulRuns) = await MeasureAveragePerformanceAsync(
                method, runs: 15, operationName: methodName);

            var expectedThreshold = methodName.Contains("Count") || methodName.Contains("Has")
                ? SimpleQueryThreshold
                : ComplexQueryThreshold;

            var result = new PerformanceTestResult
            {
                TestName = "QueryPerformanceTest",
                Operation = methodName,
                ExecutionTime = averageTime,
                Threshold = expectedThreshold,
                MemoryUsed = averageMemory,
                PassedTimeTarget = averageTime <= expectedThreshold,
                PassedMemoryTarget = averageMemory <= MaxMemoryUsageBytes,
                DataSetSize = heavyUser.ProcessedImages.Count,
                Notes = $"Tested with user having {(methodName.Contains("medium") ? mediumUser.ProcessedImages.Count : heavyUser.ProcessedImages.Count)} images"
            };

            _testResults.Add(result);

            var status = result.PassedTimeTarget ? "✓ PASS" : "✗ FAIL";
            _output.WriteLine($"  {methodName}: {averageTime.TotalMilliseconds:F1}ms (target: {expectedThreshold.TotalMilliseconds}ms) {status}");
        }

        _output.WriteLine("");
    }

    #endregion

    #region Memory Usage Tests

    private async Task RunMemoryUsageTests()
    {
        _output.WriteLine("=== Memory Usage Tests ===");

        var testUsers = await _context.UserProfiles.Include(u => u.ProcessedImages).ToListAsync();
        var heavyUser = testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();

        // Compare memory usage of different approaches
        var memoryTests = new Dictionary<string, Func<Task<object?>>>
        {
            ["EagerLoading (GetByUserIdAsync)"] = async () => await _repository.GetByUserIdAsync(heavyUser.UserId),
            ["OptimizedLight (GetByUserIdLightAsync)"] = async () => await _repository.GetByUserIdLightAsync(heavyUser.UserId),
            ["OptimizedStats (GetUserProfileStatsAsync)"] = async () => await _repository.GetUserProfileStatsAsync(heavyUser.UserId),
            ["OptimizedPagination (GetUserImagesPagedAsync)"] = async () => await _repository.GetUserImagesPagedAsync(heavyUser.UserId, 1, 20)
        };

        var memoryResults = new Dictionary<string, (TimeSpan Time, long Memory)>();

        foreach (var (testName, test) in memoryTests)
        {
            var (result, elapsed, memoryBefore, memoryAfter) = await MeasurePerformanceAsync(test);
            var memoryUsed = memoryAfter - memoryBefore;
            memoryResults[testName] = (elapsed, memoryUsed);

            var status = memoryUsed <= MaxMemoryUsageBytes ? "✓ PASS" : "✗ FAIL";
            _output.WriteLine($"  {testName}: {memoryUsed / 1024:F0}KB, {elapsed.TotalMilliseconds:F1}ms {status}");
        }

        // Calculate memory improvements
        if (memoryResults.ContainsKey("EagerLoading (GetByUserIdAsync)") &&
            memoryResults.ContainsKey("OptimizedLight (GetByUserIdLightAsync)"))
        {
            var eagerMemory = memoryResults["EagerLoading (GetByUserIdAsync)"].Memory;
            var lightMemory = memoryResults["OptimizedLight (GetByUserIdLightAsync)"].Memory;

            if (eagerMemory > 0)
            {
                var memoryReduction = ((eagerMemory - lightMemory) / (double)eagerMemory * 100);
                _output.WriteLine($"  Memory reduction: {memoryReduction:F1}% improvement");

                _testResults.Add(new PerformanceTestResult
                {
                    TestName = "MemoryUsageTest",
                    Operation = "Memory Reduction (Light vs Eager)",
                    ExecutionTime = TimeSpan.Zero,
                    MemoryUsed = eagerMemory - lightMemory,
                    PassedMemoryTarget = memoryReduction >= ExpectedMemoryReduction,
                    DataSetSize = heavyUser.ProcessedImages.Count,
                    Notes = $"{memoryReduction:F1}% memory reduction achieved"
                });
            }
        }

        _output.WriteLine("");
    }

    #endregion

    #region Pagination Tests

    private async Task RunPaginationTests()
    {
        _output.WriteLine("=== Pagination Performance Tests ===");

        var testUsers = await _context.UserProfiles.Include(u => u.ProcessedImages).ToListAsync();
        var heavyUser = testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();

        var pageSizes = new[] { 10, 20, 50, 100 };

        foreach (var pageSize in pageSizes)
        {
            var (averageTime, averageMemory, successfulRuns) = await MeasureAveragePerformanceAsync(
                async () => await _repository.GetUserImagesPagedAsync(heavyUser.UserId, 1, pageSize),
                runs: 10,
                $"Pagination-PageSize{pageSize}");

            var result = new PerformanceTestResult
            {
                TestName = "PaginationTest",
                Operation = $"PageSize {pageSize}",
                ExecutionTime = averageTime,
                Threshold = SimpleQueryThreshold,
                MemoryUsed = averageMemory,
                PassedTimeTarget = averageTime <= SimpleQueryThreshold,
                PassedMemoryTarget = averageMemory <= MaxMemoryUsageBytes,
                DataSetSize = heavyUser.ProcessedImages.Count,
                Notes = $"Pagination with {pageSize} items per page"
            };

            _testResults.Add(result);

            var status = result.PassedTimeTarget ? "✓ PASS" : "✗ FAIL";
            _output.WriteLine($"  Page size {pageSize}: {averageTime.TotalMilliseconds:F1}ms {status}");
        }

        _output.WriteLine("");
    }

    #endregion

    #region Index Effectiveness Tests

    private async Task RunIndexEffectivenessTests()
    {
        _output.WriteLine("=== Database Index Effectiveness Tests ===");

        var testUsers = await _context.UserProfiles.Include(u => u.ProcessedImages).ToListAsync();
        var testUser = testUsers.First(u => u.ProcessedImages.Count > 50);

        // Test queries that should benefit from indexes
        var indexTests = new Dictionary<string, Func<Task<object>>>
        {
            ["UserProfile by UserId Index"] = async () => await _repository.GetByUserIdLightAsync(testUser.UserId),
            ["Images by UserProfileId Index"] = async () => await _repository.GetUserImageCountAsync(testUser.UserId),
            ["Images Paginated with CreatedAt Index"] = async () => await _repository.GetUserImagesPagedAsync(testUser.UserId, 1, 10),
            ["Images by Type with Composite Index"] = async () => await _repository.GetUserOriginalUploadCountAsync(testUser.UserId),
            ["Images by Style with Composite Index"] = async () => await _repository.GetUserImagesByStyleAsync(testUser.UserId, "corporate", 1, 10)
        };

        foreach (var (indexTest, test) in indexTests)
        {
            var (averageTime, averageMemory, successfulRuns) = await MeasureAveragePerformanceAsync(
                test, runs: 20, indexTest);

            var result = new PerformanceTestResult
            {
                TestName = "IndexEffectivenessTest",
                Operation = indexTest,
                ExecutionTime = averageTime,
                Threshold = SimpleQueryThreshold,
                MemoryUsed = averageMemory,
                PassedTimeTarget = averageTime <= SimpleQueryThreshold,
                PassedMemoryTarget = averageMemory <= MaxMemoryUsageBytes,
                DataSetSize = testUser.ProcessedImages.Count,
                Notes = "Database index optimization test"
            };

            _testResults.Add(result);

            var status = result.PassedTimeTarget ? "✓ PASS" : "✗ FAIL";
            _output.WriteLine($"  {indexTest}: {averageTime.TotalMilliseconds:F1}ms {status}");
        }

        _output.WriteLine("");
    }

    #endregion

    #region Concurrent Access Tests

    private async Task RunConcurrentAccessTests()
    {
        _output.WriteLine("=== Concurrent Access Tests ===");

        var testUsers = await _context.UserProfiles.Include(u => u.ProcessedImages).ToListAsync();
        var concurrentUsers = testUsers.Take(10).ToList();

        // Test concurrent operations
        var concurrentTasks = concurrentUsers.Select(async user =>
        {
            try
            {
                var (result, elapsed, _, _) = await MeasurePerformanceAsync(async () =>
                {
                    var stats = await _repository.GetUserProfileStatsAsync(user.UserId);
                    var images = await _repository.GetUserImagesPagedAsync(user.UserId, 1, 10);
                    return new { stats, images };
                });

                return (user.UserId, elapsed, true, string.Empty);
            }
            catch (Exception ex)
            {
                return (user.UserId, TimeSpan.Zero, false, ex.Message);
            }
        }).ToList();

        var results = await Task.WhenAll(concurrentTasks);
        var successful = results.Count(r => r.Item3);
        var failed = results.Length - successful;

        if (successful > 0)
        {
            var averageTime = TimeSpan.FromTicks((long)results.Where(r => r.Item3).Select(r => r.Item2.Ticks).Average());
            var maxTime = results.Where(r => r.Item3).Max(r => r.Item2);

            var concurrentResult = new PerformanceTestResult
            {
                TestName = "ConcurrentAccessTest",
                Operation = $"Concurrent Operations ({concurrentUsers.Count} users)",
                ExecutionTime = averageTime,
                Threshold = ApiResponseThreshold,
                PassedTimeTarget = averageTime <= ApiResponseThreshold && maxTime <= TimeSpan.FromMilliseconds(500),
                DataSetSize = concurrentUsers.Count,
                Notes = $"Success: {successful}/{results.Length}, Max time: {maxTime.TotalMilliseconds:F1}ms"
            };

            _testResults.Add(concurrentResult);

            var status = concurrentResult.PassedTimeTarget ? "✓ PASS" : "✗ FAIL";
            _output.WriteLine($"  Concurrent access: {successful}/{results.Length} successful, avg {averageTime.TotalMilliseconds:F1}ms {status}");
        }

        if (failed > 0)
        {
            _output.WriteLine($"  Failed operations: {failed}");
            foreach (var failure in results.Where(r => !r.Item3))
            {
                _output.WriteLine($"    {failure.Item1}: {failure.Item4}");
            }
        }

        _output.WriteLine("");
    }

    #endregion

    #region N+1 Query Validation Tests

    private async Task RunN1QueryValidationTests()
    {
        _output.WriteLine("=== N+1 Query Validation Tests ===");

        var testUsers = await _context.UserProfiles.Include(u => u.ProcessedImages).ToListAsync();
        var heavyUser = testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();

        // Compare optimized vs non-optimized methods
        var (eagerResult, eagerTime, eagerMemoryBefore, eagerMemoryAfter) = await MeasurePerformanceAsync(
            async () => await _repository.GetByUserIdAsync(heavyUser.UserId));

        var (lightResult, lightTime, lightMemoryBefore, lightMemoryAfter) = await MeasurePerformanceAsync(
            async () => await _repository.GetByUserIdLightAsync(heavyUser.UserId));

        var eagerMemory = eagerMemoryAfter - eagerMemoryBefore;
        var lightMemory = lightMemoryAfter - lightMemoryBefore;

        _output.WriteLine($"  Eager Loading (N+1 risk): {eagerTime.TotalMilliseconds:F1}ms, {eagerMemory / 1024:F0}KB");
        _output.WriteLine($"  Optimized Light: {lightTime.TotalMilliseconds:F1}ms, {lightMemory / 1024:F0}KB");

        if (eagerTime.TotalMilliseconds > 0 && lightTime.TotalMilliseconds > 0)
        {
            var timeImprovement = ((eagerTime.TotalMilliseconds - lightTime.TotalMilliseconds) / eagerTime.TotalMilliseconds * 100);
            var memoryImprovement = eagerMemory > 0 ? ((eagerMemory - lightMemory) / (double)eagerMemory * 100) : 0;

            _output.WriteLine($"  Performance improvement: {timeImprovement:F1}% time, {memoryImprovement:F1}% memory");

            var n1ValidationResult = new PerformanceTestResult
            {
                TestName = "N1QueryValidationTest",
                Operation = "Optimized vs Eager Loading",
                ExecutionTime = lightTime,
                MemoryUsed = lightMemory,
                PassedTimeTarget = timeImprovement > 10, // At least 10% improvement
                PassedMemoryTarget = memoryImprovement > 10,
                DataSetSize = heavyUser.ProcessedImages.Count,
                Notes = $"Time: {timeImprovement:F1}% faster, Memory: {memoryImprovement:F1}% less"
            };

            _testResults.Add(n1ValidationResult);

            var status = n1ValidationResult.PassedTimeTarget && n1ValidationResult.PassedMemoryTarget ? "✓ PASS" : "✗ FAIL";
            _output.WriteLine($"  N+1 Query Prevention: {status}");
        }

        _output.WriteLine("");
    }

    #endregion

    #region Report Generation

    private async Task GeneratePerformanceReport()
    {
        _output.WriteLine("=== Performance Test Summary ===");

        var totalTests = _testResults.Count;
        var passedTimeTargets = _testResults.Count(r => r.PassedTimeTarget);
        var passedMemoryTargets = _testResults.Count(r => r.PassedMemoryTarget);
        var overallPassed = _testResults.Count(r => r.PassedTimeTarget && r.PassedMemoryTarget);

        _output.WriteLine($"Total tests executed: {totalTests}");
        _output.WriteLine($"Time targets met: {passedTimeTargets}/{totalTests} ({(passedTimeTargets / (double)totalTests * 100):F1}%)");
        _output.WriteLine($"Memory targets met: {passedMemoryTargets}/{totalTests} ({(passedMemoryTargets / (double)totalTests * 100):F1}%)");
        _output.WriteLine($"Overall success rate: {overallPassed}/{totalTests} ({(overallPassed / (double)totalTests * 100):F1}%)");

        // Performance targets summary
        _output.WriteLine("");
        _output.WriteLine("Performance Target Compliance:");
        _output.WriteLine($"  Simple queries: ≤{SimpleQueryThreshold.TotalMilliseconds}ms");
        _output.WriteLine($"  Complex queries: ≤{ComplexQueryThreshold.TotalMilliseconds}ms");
        _output.WriteLine($"  API responses: ≤{ApiResponseThreshold.TotalMilliseconds}ms");
        _output.WriteLine($"  Memory usage: ≤{MaxMemoryUsageBytes / 1024 / 1024}MB");

        // Failed tests detail
        var failedTests = _testResults.Where(r => !r.PassedTimeTarget || !r.PassedMemoryTarget).ToList();
        if (failedTests.Any())
        {
            _output.WriteLine("");
            _output.WriteLine("Failed Tests:");
            foreach (var test in failedTests)
            {
                _output.WriteLine($"  {test.TestName} - {test.Operation}:");
                _output.WriteLine($"    Time: {test.ExecutionTime.TotalMilliseconds:F1}ms (target: {test.Threshold.TotalMilliseconds}ms)");
                _output.WriteLine($"    Memory: {test.MemoryUsed / 1024:F0}KB");
                _output.WriteLine($"    Notes: {test.Notes}");
            }
        }

        // Save detailed report to file
        await SaveDetailedReport();

        _output.WriteLine("");
    }

    private async Task SaveDetailedReport()
    {
        try
        {
            var reportData = new
            {
                TestSummary = new
                {
                    TestDateTime = DateTime.UtcNow,
                    TotalTests = _testResults.Count,
                    PassedTests = _testResults.Count(r => r.PassedTimeTarget && r.PassedMemoryTarget),
                    FailedTests = _testResults.Count(r => !r.PassedTimeTarget || !r.PassedMemoryTarget),
                    SuccessRate = (_testResults.Count(r => r.PassedTimeTarget && r.PassedMemoryTarget) / (double)_testResults.Count * 100)
                },
                PerformanceTargets = new
                {
                    SimpleQueryThresholdMs = SimpleQueryThreshold.TotalMilliseconds,
                    ComplexQueryThresholdMs = ComplexQueryThreshold.TotalMilliseconds,
                    ApiResponseThresholdMs = ApiResponseThreshold.TotalMilliseconds,
                    MaxMemoryUsageMB = MaxMemoryUsageBytes / 1024 / 1024,
                    ExpectedMemoryReductionPercent = ExpectedMemoryReduction
                },
                TestResults = _testResults.Select(r => new
                {
                    TestName = r.TestName,
                    Operation = r.Operation,
                    ExecutionTimeMs = r.ExecutionTime.TotalMilliseconds,
                    ThresholdMs = r.Threshold.TotalMilliseconds,
                    MemoryUsedBytes = r.MemoryUsed,
                    PassedTimeTarget = r.PassedTimeTarget,
                    PassedMemoryTarget = r.PassedMemoryTarget,
                    DataSetSize = r.DataSetSize,
                    Notes = r.Notes,
                    TestDateTime = r.TestDateTime
                })
            };

            var json = JsonSerializer.Serialize(reportData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Ensure ClaudeDocs directory exists
            var claudeDocsPath = "/home/alanw/projects/AI.ProfilePhotoMaker/ClaudeDocs/Analysis/Performance";
            Directory.CreateDirectory(claudeDocsPath);

            var fileName = $"database-performance-test-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            var filePath = Path.Combine(claudeDocsPath, fileName);

            await File.WriteAllTextAsync(filePath, json);
            _output.WriteLine($"Detailed performance report saved to: {filePath}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not save detailed report: {ex.Message}");
        }
    }

    #endregion

    #region Overall Validation

    private void ValidateOverallPerformanceTargets()
    {
        var overallSuccessRate = (_testResults.Count(r => r.PassedTimeTarget && r.PassedMemoryTarget) / (double)_testResults.Count * 100);

        // Assert that at least 85% of tests pass (allowing for some variation in test environments)
        overallSuccessRate.Should().BeGreaterThan(85.0,
            "At least 85% of performance tests should meet their targets");

        // Assert that no critical operations fail
        var criticalOperations = new[] { "GetByUserIdLightAsync", "GetUserProfileStatsAsync", "GetUserImagesPagedAsync" };
        var criticalResults = _testResults.Where(r => criticalOperations.Any(op => r.Operation.Contains(op))).ToList();

        if (criticalResults.Any())
        {
            var criticalSuccessRate = (criticalResults.Count(r => r.PassedTimeTarget && r.PassedMemoryTarget) / (double)criticalResults.Count * 100);
            criticalSuccessRate.Should().Be(100.0,
                "All critical database operations should meet performance targets");
        }

        // Assert that memory improvements are achieved
        var memoryComparisonResults = _testResults.Where(r => r.TestName.Contains("MemoryUsageTest")).ToList();
        if (memoryComparisonResults.Any())
        {
            memoryComparisonResults.Should().OnlyContain(r => r.PassedMemoryTarget,
                "Memory usage optimizations should achieve expected reductions");
        }
    }

    #endregion
}
