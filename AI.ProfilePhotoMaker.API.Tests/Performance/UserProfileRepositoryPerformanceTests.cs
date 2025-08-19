using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AI.ProfilePhotoMaker.API.Tests.Performance;

[Collection("Performance")]
public class UserProfileRepositoryPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly IUserProfileRepository _repository;
    private List<UserProfile> _testUsers = new();

    public UserProfileRepositoryPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _repository = _serviceProvider.GetRequiredService<IUserProfileRepository>();
    }

    [Fact]
    public async Task Setup_CreateTestData_ForPerformanceTesting()
    {
        _output.WriteLine("Setting up performance test data...");
        
        // Create test data - different user scenarios
        var scenarios = new[]
        {
            (users: 2, maxImages: 1000), // Heavy users with many images
            (users: 5, maxImages: 200),  // Medium users
            (users: 10, maxImages: 50),  // Light users
        };

        foreach (var (users, maxImages) in scenarios)
        {
            var batchUsers = await CreateTestDataAsync(users, maxImages);
            _testUsers.AddRange(batchUsers);
        }

        _output.WriteLine($"Created {_testUsers.Count} test users with {_testUsers.Sum(u => u.ProcessedImages.Count)} total images");
        
        // Verify test data was created successfully
        _testUsers.Should().NotBeEmpty();
        _testUsers.Should().HaveCountGreaterThan(15);
        _testUsers.Sum(u => u.ProcessedImages.Count).Should().BeGreaterThan(500);
    }

    #region N+1 Query Elimination Tests

    [Fact]
    public async Task GetByUserIdAsync_Should_EagerLoadAllImages_ButShowWarning()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var heavyUser = _testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();
        
        _output.WriteLine($"Testing N+1 potential with user having {heavyUser.ProcessedImages.Count} images");

        // Act - This method SHOULD be slow as it loads all images
        var (result, elapsed, memoryBefore, memoryAfter) = await MeasurePerformanceAsync(async () =>
        {
            return await _repository.GetByUserIdAsync(heavyUser.UserId);
        });

        // Assert
        result.Should().NotBeNull();
        result!.ProcessedImages.Should().HaveCount(heavyUser.ProcessedImages.Count);
        
        _output.WriteLine($"GetByUserIdAsync (N+1): {elapsed.TotalMilliseconds}ms, Memory: {memoryAfter - memoryBefore} bytes");
        
        // This method is EXPECTED to be slow - it's the "bad" method that loads everything
        // We document this for comparison with optimized methods
        if (heavyUser.ProcessedImages.Count > 100)
        {
            elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50)); // Should be slower with many images
        }
    }

    [Fact]
    public async Task GetByUserIdLightAsync_Should_BeSignificantlyFasterThanEagerLoading()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var heavyUser = _testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();
        
        // Act - Measure both methods
        var (lightResult, lightElapsed, lightMemoryBefore, lightMemoryAfter) = await MeasurePerformanceAsync(async () =>
        {
            return await _repository.GetByUserIdLightAsync(heavyUser.UserId);
        });

        var (fullResult, fullElapsed, fullMemoryBefore, fullMemoryAfter) = await MeasurePerformanceAsync(async () =>
        {
            return await _repository.GetByUserIdAsync(heavyUser.UserId);
        });

        // Assert
        lightResult.Should().NotBeNull();
        lightResult!.UserId.Should().Be(heavyUser.UserId);
        lightResult.ProcessedImages.Should().BeEmpty(); // Should not load images
        
        fullResult.Should().NotBeNull();
        fullResult!.ProcessedImages.Should().HaveCountGreaterThan(0);
        
        // Performance comparison
        var lightMemoryUsed = lightMemoryAfter - lightMemoryBefore;
        var fullMemoryUsed = fullMemoryAfter - fullMemoryBefore;
        
        _output.WriteLine($"Light method: {lightElapsed.TotalMilliseconds}ms, Memory: {lightMemoryUsed} bytes");
        _output.WriteLine($"Full method: {fullElapsed.TotalMilliseconds}ms, Memory: {fullMemoryUsed} bytes");
        _output.WriteLine($"Performance improvement: {((fullElapsed.TotalMilliseconds - lightElapsed.TotalMilliseconds) / fullElapsed.TotalMilliseconds * 100):F1}% time, {((fullMemoryUsed - lightMemoryUsed) / (double)fullMemoryUsed * 100):F1}% memory");

        // Assertions
        lightElapsed.Should().BeLessThan(SimpleQueryThreshold);
        lightElapsed.Should().BeLessThan(fullElapsed); // Should be faster
        
        if (fullMemoryUsed > 0)
        {
            lightMemoryUsed.Should().BeLessThan(fullMemoryUsed); // Should use less memory
        }
    }

    #endregion

    #region Selective Loading Performance Tests

    [Fact]
    public async Task GetUserProfileStatsAsync_Should_MeetPerformanceTargets()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var testUser = _testUsers.First(u => u.ProcessedImages.Count > 10);
        
        // Act
        var (averageTime, averageMemoryIncrease, successfulRuns) = await MeasureAveragePerformanceAsync(
            async () => await _repository.GetUserProfileStatsAsync(testUser.UserId),
            runs: 20,
            "GetUserProfileStatsAsync"
        );

        // Assert
        successfulRuns.Should().Be(20);
        AssertPerformanceTargets(averageTime, ComplexQueryThreshold, averageMemoryIncrease, "GetUserProfileStatsAsync");
        
        // Verify functionality
        var stats = await _repository.GetUserProfileStatsAsync(testUser.UserId);
        stats.Should().NotBeNull();
        stats!.TotalProcessedImages.Should().Be(testUser.ProcessedImages.Count);
        stats.OriginalUploads.Should().Be(testUser.ProcessedImages.Count(i => i.IsOriginalUpload));
        stats.GeneratedImages.Should().Be(testUser.ProcessedImages.Count(i => i.IsGenerated));
    }

    [Fact]
    public async Task GetProfileWithRecentImagesAsync_Should_MeetPerformanceTargets()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var testUser = _testUsers.First(u => u.ProcessedImages.Count > 20);
        
        // Act
        var (averageTime, averageMemoryIncrease, successfulRuns) = await MeasureAveragePerformanceAsync(
            async () => await _repository.GetProfileWithRecentImagesAsync(testUser.UserId, 10),
            runs: 15,
            "GetProfileWithRecentImagesAsync"
        );

        // Assert
        successfulRuns.Should().Be(15);
        AssertPerformanceTargets(averageTime, ComplexQueryThreshold, averageMemoryIncrease, "GetProfileWithRecentImagesAsync");
        
        // Verify functionality
        var profileWithImages = await _repository.GetProfileWithRecentImagesAsync(testUser.UserId, 10);
        profileWithImages.Should().NotBeNull();
        profileWithImages!.RecentImages.Count.Should().BeLessThanOrEqualTo(10);
        profileWithImages.TotalImageCount.Should().Be(testUser.ProcessedImages.Count);
    }

    #endregion

    #region Pagination Performance Tests

    [Fact]
    public async Task GetUserImagesPagedAsync_Should_MeetPerformanceTargetsForAllPageSizes()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var heavyUser = _testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();
        
        var pageSizes = new[] { 10, 20, 50, 100 };
        var results = new List<PerformanceTestResult>();
        
        foreach (var pageSize in pageSizes)
        {
            // Act
            var (averageTime, averageMemoryIncrease, successfulRuns) = await MeasureAveragePerformanceAsync(
                async () => await _repository.GetUserImagesPagedAsync(heavyUser.UserId, 1, pageSize),
                runs: 10,
                $"GetUserImagesPagedAsync_PageSize{pageSize}"
            );

            // Assert
            successfulRuns.Should().Be(10);
            AssertPerformanceTargets(averageTime, SimpleQueryThreshold, averageMemoryIncrease, $"Pagination PageSize {pageSize}");
            
            results.Add(new PerformanceTestResult
            {
                TestName = nameof(GetUserImagesPagedAsync_Should_MeetPerformanceTargetsForAllPageSizes),
                Operation = $"PageSize {pageSize}",
                ExecutionTime = averageTime,
                Threshold = SimpleQueryThreshold,
                MemoryUsed = averageMemoryIncrease,
                PassedTimeTarget = averageTime <= SimpleQueryThreshold,
                PassedMemoryTarget = averageMemoryIncrease <= MaxMemoryUsageBytes,
                DataSetSize = heavyUser.ProcessedImages.Count
            });
        }
        
        // Verify pagination functionality
        var page1 = await _repository.GetUserImagesPagedAsync(heavyUser.UserId, 1, 20);
        var page2 = await _repository.GetUserImagesPagedAsync(heavyUser.UserId, 2, 20);
        
        page1.Should().NotBeNull();
        page1.Items.Should().HaveCount(Math.Min(20, heavyUser.ProcessedImages.Count));
        page1.TotalCount.Should().Be(heavyUser.ProcessedImages.Count);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(20);
        
        if (heavyUser.ProcessedImages.Count > 20)
        {
            page2.Items.Should().NotBeEmpty();
            page1.Items.Should().NotIntersectWith(page2.Items); // No duplicate items
        }
        
        _output.WriteLine($"Pagination performance results:");
        foreach (var result in results)
        {
            _output.WriteLine($"- {result.Operation}: {result.ExecutionTime.TotalMilliseconds:F1}ms (Target: {result.Threshold.TotalMilliseconds}ms)");
        }
    }

    [Fact]
    public async Task GetUserImagesByStyleAsync_Should_MeetPerformanceTargetsWithFiltering()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var testUser = _testUsers.First(u => u.ProcessedImages.Count > 50);
        var testStyle = testUser.ProcessedImages.FirstOrDefault()?.Style ?? "corporate";
        
        // Act
        var (averageTime, averageMemoryIncrease, successfulRuns) = await MeasureAveragePerformanceAsync(
            async () => await _repository.GetUserImagesByStyleAsync(testUser.UserId, testStyle, 1, 20),
            runs: 15,
            "GetUserImagesByStyleAsync"
        );

        // Assert
        successfulRuns.Should().Be(15);
        AssertPerformanceTargets(averageTime, SimpleQueryThreshold, averageMemoryIncrease, "GetUserImagesByStyleAsync");
        
        // Verify filtering functionality
        var styleImages = await _repository.GetUserImagesByStyleAsync(testUser.UserId, testStyle, 1, 20);
        styleImages.Should().NotBeNull();
        styleImages.Items.Should().OnlyContain(img => img.Style == testStyle);
    }

    #endregion

    #region Count Operations Performance Tests

    [Fact]
    public async Task CountOperations_Should_MeetPerformanceTargets()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var testUser = _testUsers.First(u => u.ProcessedImages.Count > 20);
        
        var countOperations = new Dictionary<string, Func<Task<object>>>
        {
            ["GetUserImageCountAsync"] = async () => await _repository.GetUserImageCountAsync(testUser.UserId),
            ["GetUserOriginalUploadCountAsync"] = async () => await _repository.GetUserOriginalUploadCountAsync(testUser.UserId),
            ["GetUserGeneratedImageCountAsync"] = async () => await _repository.GetUserGeneratedImageCountAsync(testUser.UserId),
            ["HasProcessedImagesAsync"] = async () => await _repository.HasProcessedImagesAsync(testUser.UserId),
            ["HasOriginalUploadsAsync"] = async () => await _repository.HasOriginalUploadsAsync(testUser.UserId, 5)
        };

        foreach (var (operationName, operation) in countOperations)
        {
            // Act
            var (averageTime, averageMemoryIncrease, successfulRuns) = await MeasureAveragePerformanceAsync(
                operation,
                runs: 20,
                operationName
            );

            // Assert
            successfulRuns.Should().Be(20);
            AssertPerformanceTargets(averageTime, SimpleQueryThreshold, averageMemoryIncrease, operationName);
            
            _output.WriteLine($"{operationName}: {averageTime.TotalMilliseconds:F1}ms");
        }
        
        // Verify results are accurate
        var totalImages = await _repository.GetUserImageCountAsync(testUser.UserId);
        var originalUploads = await _repository.GetUserOriginalUploadCountAsync(testUser.UserId);
        var generatedImages = await _repository.GetUserGeneratedImageCountAsync(testUser.UserId);
        
        totalImages.Should().Be(testUser.ProcessedImages.Count);
        originalUploads.Should().Be(testUser.ProcessedImages.Count(i => i.IsOriginalUpload));
        generatedImages.Should().Be(testUser.ProcessedImages.Count(i => i.IsGenerated));
        (originalUploads + generatedImages).Should().Be(totalImages);
    }

    #endregion

    #region Memory Usage Tests

    [Fact]
    public async Task MemoryUsage_OptimizedMethodsShouldUseSignificantlyLessMemory()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var heavyUser = _testUsers.OrderByDescending(u => u.ProcessedImages.Count).First();
        
        _output.WriteLine($"Testing memory usage with user having {heavyUser.ProcessedImages.Count} images");

        // Measure memory usage of different methods
        var memoryTests = new Dictionary<string, Func<Task<object>>>
        {
            ["GetByUserIdLightAsync (Optimized)"] = async () => await _repository.GetByUserIdLightAsync(heavyUser.UserId),
            ["GetByUserIdAsync (Full Load)"] = async () => await _repository.GetByUserIdAsync(heavyUser.UserId),
            ["GetUserProfileStatsAsync (Optimized)"] = async () => await _repository.GetUserProfileStatsAsync(heavyUser.UserId),
            ["GetUserImagesPagedAsync (Optimized)"] = async () => await _repository.GetUserImagesPagedAsync(heavyUser.UserId, 1, 20)
        };

        var memoryResults = new Dictionary<string, long>();

        foreach (var (methodName, method) in memoryTests)
        {
            // Measure memory usage
            var (result, elapsed, memoryBefore, memoryAfter) = await MeasurePerformanceAsync(method);
            var memoryUsed = memoryAfter - memoryBefore;
            memoryResults[methodName] = memoryUsed;
            
            _output.WriteLine($"{methodName}: {memoryUsed} bytes, {elapsed.TotalMilliseconds:F1}ms");
        }

        // Assert memory usage improvements
        var lightMemory = memoryResults["GetByUserIdLightAsync (Optimized)"];
        var fullMemory = memoryResults["GetByUserIdAsync (Full Load)"];
        var statsMemory = memoryResults["GetUserProfileStatsAsync (Optimized)"];
        var pagedMemory = memoryResults["GetUserImagesPagedAsync (Optimized)"];

        // Light method should use significantly less memory than full load
        if (fullMemory > 0)
        {
            var memoryReduction = ((fullMemory - lightMemory) / (double)fullMemory * 100);
            _output.WriteLine($"Memory reduction (Light vs Full): {memoryReduction:F1}%");
            memoryReduction.Should().BeGreaterThan(ExpectedMemoryReduction);
        }

        // All optimized methods should use reasonable memory
        lightMemory.Should().BeLessThan(MaxMemoryUsageBytes);
        statsMemory.Should().BeLessThan(MaxMemoryUsageBytes);
        pagedMemory.Should().BeLessThan(MaxMemoryUsageBytes);
    }

    #endregion

    #region Index Effectiveness Tests

    [Fact]
    public async Task DatabaseIndexes_Should_ProvidePerformanceImprovement()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var testUser = _testUsers.First(u => u.ProcessedImages.Count > 100);
        
        // Test queries that should benefit from indexes
        var indexedQueries = new Dictionary<string, Func<Task<object>>>
        {
            ["UserProfile by UserId (IX_UserProfiles_UserId)"] = 
                async () => await _repository.GetByUserIdLightAsync(testUser.UserId),
            
            ["Images by UserProfileId (IX_ProcessedImages_UserProfileId)"] = 
                async () => await _repository.GetUserImageCountAsync(testUser.UserId),
            
            ["Images paginated (IX_ProcessedImages_UserProfileId_CreatedAt_Desc)"] = 
                async () => await _repository.GetUserImagesPagedAsync(testUser.UserId, 1, 10),
            
            ["Images by type (IX_ProcessedImages_UserProfileId_IsOriginalUpload)"] = 
                async () => await _repository.GetUserOriginalUploadCountAsync(testUser.UserId),
                
            ["Images by style (IX_ProcessedImages_UserProfileId_Style_CreatedAt_Desc)"] = 
                async () => await _repository.GetUserImagesByStyleAsync(testUser.UserId, "corporate", 1, 10)
        };

        foreach (var (queryDescription, query) in indexedQueries)
        {
            // Act - Run multiple times to get consistent measurements
            var (averageTime, averageMemoryIncrease, successfulRuns) = await MeasureAveragePerformanceAsync(
                query,
                runs: 10,
                queryDescription
            );

            // Assert - Indexed queries should be fast
            successfulRuns.Should().Be(10);
            AssertPerformanceTargets(averageTime, SimpleQueryThreshold, averageMemoryIncrease, queryDescription);
            
            _output.WriteLine($"{queryDescription}: {averageTime.TotalMilliseconds:F2}ms average");
        }
    }

    #endregion

    #region Bulk Operations Performance Tests

    [Fact]
    public async Task BulkOperations_Should_MeetPerformanceTargets()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var testUser = _testUsers.First(u => u.ProcessedImages.Count > 20);
        var imagesToDelete = testUser.ProcessedImages.Take(5).Select(i => i.Id).ToList();
        
        // Test bulk operations
        var bulkOperations = new Dictionary<string, Func<Task<object>>>
        {
            ["GetUserProcessedImagesAsync (filtered)"] = 
                async () => await _repository.GetUserProcessedImagesAsync(testUser.UserId, includeGenerated: true, includeOriginal: false),
            
            ["DeleteUserImagesAsync (bulk delete)"] = 
                async () => { await _repository.DeleteUserImagesAsync(testUser.UserId, imagesToDelete); return true; }
        };

        foreach (var (operationName, operation) in bulkOperations)
        {
            if (operationName.Contains("Delete"))
            {
                // Only test delete performance once to avoid affecting other tests
                var (result, elapsed, memoryBefore, memoryAfter) = await MeasurePerformanceAsync(operation);
                var memoryUsed = memoryAfter - memoryBefore;
                
                AssertPerformanceTargets(elapsed, ComplexQueryThreshold, memoryUsed, operationName);
                _output.WriteLine($"{operationName}: {elapsed.TotalMilliseconds:F2}ms");
            }
            else
            {
                var (averageTime, averageMemoryIncrease, successfulRuns) = await MeasureAveragePerformanceAsync(
                    operation,
                    runs: 10,
                    operationName
                );

                successfulRuns.Should().Be(10);
                AssertPerformanceTargets(averageTime, ComplexQueryThreshold, averageMemoryIncrease, operationName);
                _output.WriteLine($"{operationName}: {averageTime.TotalMilliseconds:F2}ms average");
            }
        }
    }

    #endregion

    #region Load Testing Simulation

    [Fact]
    public async Task ConcurrentUsers_Should_MaintainPerformance()
    {
        // Arrange
        await Setup_CreateTestData_ForPerformanceTesting();
        var concurrentUsers = _testUsers.Take(10).ToList();
        var concurrency = 5; // Simulate 5 concurrent requests
        
        _output.WriteLine($"Testing concurrent access with {concurrency} simultaneous operations");

        // Act - Simulate concurrent read operations
        var concurrentTasks = new List<Task<(string UserId, TimeSpan Elapsed, bool Success)>>();
        
        foreach (var user in concurrentUsers.Take(concurrency))
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    var (result, elapsed, _, _) = await MeasurePerformanceAsync(async () =>
                    {
                        // Mix of different operations to simulate real usage
                        var stats = await _repository.GetUserProfileStatsAsync(user.UserId);
                        var images = await _repository.GetUserImagesPagedAsync(user.UserId, 1, 10);
                        var count = await _repository.GetUserImageCountAsync(user.UserId);
                        return new { stats, images, count };
                    });
                    
                    return (user.UserId, elapsed, true);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Concurrent operation failed for user {user.UserId}: {ex.Message}");
                    return (user.UserId, TimeSpan.Zero, false);
                }
            });
            
            concurrentTasks.Add(task);
        }

        var results = await Task.WhenAll(concurrentTasks);

        // Assert
        var successfulOperations = results.Count(r => r.Success);
        var averageResponseTime = TimeSpan.FromTicks((long)results.Where(r => r.Success).Select(r => r.Elapsed.Ticks).Average());
        var maxResponseTime = results.Where(r => r.Success).Max(r => r.Elapsed);
        
        successfulOperations.Should().Be(concurrency, "All concurrent operations should succeed");
        averageResponseTime.Should().BeLessThan(ApiResponseThreshold, "Average response time should meet API targets");
        maxResponseTime.Should().BeLessThan(TimeSpan.FromMilliseconds(500), "Maximum response time should be reasonable");
        
        _output.WriteLine($"Concurrent operations results:");
        _output.WriteLine($"- Success rate: {successfulOperations}/{concurrency}");
        _output.WriteLine($"- Average response time: {averageResponseTime.TotalMilliseconds:F1}ms");
        _output.WriteLine($"- Maximum response time: {maxResponseTime.TotalMilliseconds:F1}ms");
    }

    #endregion

    public override void Dispose()
    {
        try
        {
            // Clean up specific test data
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
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Error during test cleanup: {ex.Message}");
        }
        
        base.Dispose();
    }
}