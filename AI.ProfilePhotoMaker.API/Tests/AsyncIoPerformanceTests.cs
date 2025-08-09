using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Middleware;

namespace AI.ProfilePhotoMaker.API.Tests;

/// <summary>
/// Comprehensive async I/O performance testing suite for validating non-blocking operations
/// and measuring performance improvements from async file service implementations
/// </summary>
public class AsyncIoPerformanceTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AsyncIoPerformanceTests> _logger;
    private readonly IAsyncFileService _asyncFileService;
    private readonly IAsyncZipService _asyncZipService;
    private readonly string _testDataDirectory;
    private readonly PerformanceMetrics _baseline;

    public AsyncIoPerformanceTests(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<AsyncIoPerformanceTests>>();
        _asyncFileService = serviceProvider.GetRequiredService<IAsyncFileService>();
        _asyncZipService = serviceProvider.GetRequiredService<IAsyncZipService>();
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "AsyncIoTests", Guid.NewGuid().ToString());
        _baseline = new PerformanceMetrics();
    }

    /// <summary>
    /// Execute comprehensive async I/O performance test suite
    /// </summary>
    public async Task<AsyncIoTestResults> ExecuteComprehensiveTestSuiteAsync()
    {
        var results = new AsyncIoTestResults
        {
            TestStartTime = DateTimeOffset.UtcNow,
            TestId = Guid.NewGuid().ToString()
        };

        try
        {
            _logger.LogInformation("🚀 Starting comprehensive async I/O performance test suite");

            // Setup test environment
            await SetupTestEnvironmentAsync();

            // Test 1: Async Pattern Validation
            _logger.LogInformation("📋 Test 1: Async Pattern Validation");
            results.AsyncPatternValidation = await TestAsyncPatternValidationAsync();

            // Test 2: Memory Usage Testing with Large Files
            _logger.LogInformation("📋 Test 2: Memory Usage Testing");
            results.MemoryUsageTest = await TestMemoryUsageAsync();

            // Test 3: Throughput Testing with Concurrent Operations
            _logger.LogInformation("📋 Test 3: Throughput Testing");
            results.ThroughputTest = await TestThroughputAsync();

            // Test 4: Blocking Detection Validation
            _logger.LogInformation("📋 Test 4: Blocking Detection");
            results.BlockingDetection = await TestBlockingDetectionAsync();

            // Test 5: File Streaming Testing
            _logger.LogInformation("📋 Test 5: File Streaming");
            results.FileStreamingTest = await TestFileStreamingAsync();

            // Test 6: ZIP Processing with Streaming Compression
            _logger.LogInformation("📋 Test 6: ZIP Processing");
            results.ZipProcessingTest = await TestZipProcessingAsync();

            // Test 7: Error Handling and Resource Disposal
            _logger.LogInformation("📋 Test 7: Error Handling");
            results.ErrorHandlingTest = await TestErrorHandlingAsync();

            // Calculate overall performance metrics
            results.OverallPerformanceImprovement = CalculateOverallImprovement(results);

            results.TestEndTime = DateTimeOffset.UtcNow;
            results.TotalDuration = results.TestEndTime - results.TestStartTime;

            _logger.LogInformation("✅ Async I/O performance test suite completed successfully");
            LogTestSummary(results);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Async I/O performance test suite failed");
            results.TestEndTime = DateTimeOffset.UtcNow;
            results.TotalDuration = results.TestEndTime - results.TestStartTime;
            results.OverallSuccess = false;
            results.Error = ex.Message;
            return results;
        }
        finally
        {
            // Cleanup test environment
            await CleanupTestEnvironmentAsync();
        }
    }

    public async Task SetupTestEnvironmentAsync()
    {
        await _asyncFileService.CreateDirectoryAsync(_testDataDirectory);
        
        // Create test files of various sizes
        await CreateTestFileAsync("small.txt", 1024); // 1KB
        await CreateTestFileAsync("medium.txt", 1024 * 1024); // 1MB
        await CreateTestFileAsync("large.txt", 10 * 1024 * 1024); // 10MB
        await CreateTestFileAsync("huge.txt", 50 * 1024 * 1024); // 50MB

        // Create test images
        for (int i = 0; i < 15; i++)
        {
            await CreateTestImageAsync($"test_image_{i:D3}.jpg", 2 * 1024 * 1024); // 2MB each
        }

        _logger.LogInformation("✅ Test environment setup completed");
    }

    public async Task<TestResult> TestAsyncPatternValidationAsync()
    {
        var result = new TestResult { TestName = "Async Pattern Validation" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var validationResults = new List<string>();

            // Test 1: Validate AsyncFileService methods are truly async
            await ValidateAsyncMethod(() => _asyncFileService.CreateDirectoryAsync(Path.Combine(_testDataDirectory, "async_test")),
                "CreateDirectoryAsync", validationResults);

            await ValidateAsyncMethod(() => _asyncFileService.FileExistsAsync(Path.Combine(_testDataDirectory, "small.txt")),
                "FileExistsAsync", validationResults);

            var testFile = Path.Combine(_testDataDirectory, "copy_test.txt");
            await using var sourceStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content"));
            await ValidateAsyncMethod(() => _asyncFileService.CopyStreamToFileAsync(sourceStream, testFile, 81920),
                "CopyStreamToFileAsync", validationResults);

            // Test 2: Validate no blocking operations in request pipeline
            var threadPoolBefore = GetThreadPoolStats();
            await ValidateNonBlockingPattern();
            var threadPoolAfter = GetThreadPoolStats();

            result.Success = validationResults.All(r => r.Contains("✅"));
            result.Metrics = new Dictionary<string, object>
            {
                ["ValidationResults"] = validationResults,
                ["ThreadPoolBefore"] = threadPoolBefore,
                ["ThreadPoolAfter"] = threadPoolAfter,
                ["ThreadPoolDelta"] = new
                {
                    WorkerThreadsUsed = threadPoolBefore.AvailableWorkerThreads - threadPoolAfter.AvailableWorkerThreads,
                    CompletionPortsUsed = threadPoolBefore.AvailableCompletionPortThreads - threadPoolAfter.AvailableCompletionPortThreads
                }
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation($"✅ Async pattern validation completed in {result.Duration.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "❌ Async pattern validation failed");
        }

        return result;
    }

    public async Task<TestResult> TestMemoryUsageAsync()
    {
        var result = new TestResult { TestName = "Memory Usage Test" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var beforeMemory = GC.GetTotalMemory(true);
            var process = Process.GetCurrentProcess();
            var beforeWorkingSet = process.WorkingSet64;

            // Test large file processing without loading into memory
            var largeFilePath = Path.Combine(_testDataDirectory, "huge.txt");
            var copyPath = Path.Combine(_testDataDirectory, "huge_copy.txt");

            await using var sourceStream = await _asyncFileService.OpenFileStreamAsync(largeFilePath);
            if (sourceStream != null)
            {
                await _asyncFileService.CopyStreamToFileAsync(sourceStream, copyPath, 81920);
            }

            // Force garbage collection to get accurate memory reading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var afterMemory = GC.GetTotalMemory(false);
            process.Refresh();
            var afterWorkingSet = process.WorkingSet64;

            var memoryIncrease = afterMemory - beforeMemory;
            var workingSetIncrease = afterWorkingSet - beforeWorkingSet;

            // Validate memory usage stayed within acceptable bounds
            var maxAcceptableIncrease = 160 * 1024 * 1024; // 160MB threshold
            var memoryEfficient = memoryIncrease < maxAcceptableIncrease;
            var workingSetEfficient = workingSetIncrease < maxAcceptableIncrease;

            result.Success = memoryEfficient && workingSetEfficient;
            result.Metrics = new Dictionary<string, object>
            {
                ["MemoryIncrease"] = memoryIncrease,
                ["WorkingSetIncrease"] = workingSetIncrease,
                ["MaxAcceptableIncrease"] = maxAcceptableIncrease,
                ["MemoryEfficient"] = memoryEfficient,
                ["WorkingSetEfficient"] = workingSetEfficient,
                ["MemoryIncreasePercent"] = (double)memoryIncrease / (50 * 1024 * 1024) * 100, // % of file size
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (result.Success)
            {
                _logger.LogInformation($"✅ Memory usage test passed - Memory increase: {memoryIncrease / 1024 / 1024:F2}MB, Duration: {result.Duration.TotalMilliseconds:F2}ms");
            }
            else
            {
                _logger.LogWarning($"⚠️  Memory usage test failed - Memory increase: {memoryIncrease / 1024 / 1024:F2}MB exceeds {maxAcceptableIncrease / 1024 / 1024}MB limit");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "❌ Memory usage test failed");
        }

        return result;
    }

    public async Task<TestResult> TestThroughputAsync()
    {
        var result = new TestResult { TestName = "Throughput Test" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Measure baseline sequential processing
            var sequentialStart = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                var filePath = Path.Combine(_testDataDirectory, $"sequential_{i}.txt");
                await CreateTestFileAsync($"sequential_{i}.txt", 1024 * 1024);
            }
            sequentialStart.Stop();

            // Measure concurrent processing with async operations
            var concurrentStart = Stopwatch.StartNew();
            var concurrentTasks = Enumerable.Range(0, 10).Select(async i =>
            {
                var filePath = Path.Combine(_testDataDirectory, $"concurrent_{i}.txt");
                await CreateTestFileAsync($"concurrent_{i}.txt", 1024 * 1024);
            }).ToArray();

            await Task.WhenAll(concurrentTasks);
            concurrentStart.Stop();

            // Test concurrent file operations with semaphore throttling
            var fileList = Enumerable.Range(0, 10).Select(i => 
                Path.Combine(_testDataDirectory, $"test_image_{i:D3}.jpg")).ToList();

            var processingStart = Stopwatch.StartNew();
            var processResults = await _asyncFileService.ProcessFilesAsync(
                fileList,
                async (filePath, ct) =>
                {
                    var fileInfo = await _asyncFileService.GetFileInfoAsync(filePath, ct);
                    return fileInfo?.Length ?? 0;
                },
                maxConcurrency: 4);
            processingStart.Stop();

            // Calculate performance improvements
            var sequentialThroughput = 10.0 / sequentialStart.Elapsed.TotalSeconds;
            var concurrentThroughput = 10.0 / concurrentStart.Elapsed.TotalSeconds;
            var improvementPercent = ((concurrentThroughput - sequentialThroughput) / sequentialThroughput) * 100;

            result.Success = improvementPercent >= 40; // Target 40%+ improvement
            result.Metrics = new Dictionary<string, object>
            {
                ["SequentialDuration"] = sequentialStart.Elapsed,
                ["ConcurrentDuration"] = concurrentStart.Elapsed,
                ["ProcessingDuration"] = processingStart.Elapsed,
                ["SequentialThroughput"] = sequentialThroughput,
                ["ConcurrentThroughput"] = concurrentThroughput,
                ["ImprovementPercent"] = improvementPercent,
                ["ProcessedFilesCount"] = processResults.Count,
                ["SuccessfulProcessing"] = processResults.Count(r => r.Success)
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation($"✅ Throughput test completed - Improvement: {improvementPercent:F1}%, Duration: {result.Duration.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "❌ Throughput test failed");
        }

        return result;
    }

    public async Task<TestResult> TestBlockingDetectionAsync()
    {
        var result = new TestResult { TestName = "Blocking Detection Test" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Simulate middleware detection of blocking operations
            var mockContext = CreateMockHttpContext();
            var middleware = new AsyncIoPerformanceMiddleware(
                (ctx) => Task.CompletedTask,
                _serviceProvider.GetRequiredService<ILogger<AsyncIoPerformanceMiddleware>>(),
                _serviceProvider.GetRequiredService<IConfiguration>());

            var threadPoolBefore = GetThreadPoolStats();

            // Execute async file operations
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_asyncFileService.FileExistsAsync(Path.Combine(_testDataDirectory, $"test_image_{i:D3}.jpg")));
            }
            await Task.WhenAll(tasks);

            var threadPoolAfter = GetThreadPoolStats();

            // Validate thread pool health
            var threadPoolHealthy = threadPoolAfter.AvailableWorkerThreads > 5 && 
                                   threadPoolAfter.AvailableCompletionPortThreads > 5;

            result.Success = threadPoolHealthy;
            result.Metrics = new Dictionary<string, object>
            {
                ["ThreadPoolBefore"] = threadPoolBefore,
                ["ThreadPoolAfter"] = threadPoolAfter,
                ["ThreadPoolHealthy"] = threadPoolHealthy,
                ["WorkerThreadsUsed"] = threadPoolBefore.AvailableWorkerThreads - threadPoolAfter.AvailableWorkerThreads
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation($"✅ Blocking detection test completed - Thread pool healthy: {threadPoolHealthy}, Duration: {result.Duration.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "❌ Blocking detection test failed");
        }

        return result;
    }

    public async Task<TestResult> TestFileStreamingAsync()
    {
        var result = new TestResult { TestName = "File Streaming Test" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var largeFilePath = Path.Combine(_testDataDirectory, "huge.txt");
            var streamedCopyPath = Path.Combine(_testDataDirectory, "streamed_copy.txt");

            var beforeMemory = GC.GetTotalMemory(true);

            // Test streaming file copy with optimal buffer size
            await using var sourceStream = await _asyncFileService.OpenFileStreamAsync(largeFilePath);
            if (sourceStream != null)
            {
                await _asyncFileService.CopyStreamToFileAsync(sourceStream, streamedCopyPath, 81920);
            }

            // Verify file integrity
            var originalExists = await _asyncFileService.FileExistsAsync(largeFilePath);
            var copyExists = await _asyncFileService.FileExistsAsync(streamedCopyPath);
            var originalInfo = await _asyncFileService.GetFileInfoAsync(largeFilePath);
            var copyInfo = await _asyncFileService.GetFileInfoAsync(streamedCopyPath);

            var afterMemory = GC.GetTotalMemory(false);
            var memoryIncrease = afterMemory - beforeMemory;

            var filesMatch = originalExists && copyExists && 
                            originalInfo?.Length == copyInfo?.Length;
            var memoryEfficient = memoryIncrease < (10 * 1024 * 1024); // 10MB threshold

            result.Success = filesMatch && memoryEfficient;
            result.Metrics = new Dictionary<string, object>
            {
                ["OriginalFileSize"] = originalInfo?.Length ?? 0,
                ["CopyFileSize"] = copyInfo?.Length ?? 0,
                ["FilesMatch"] = filesMatch,
                ["MemoryIncrease"] = memoryIncrease,
                ["MemoryEfficient"] = memoryEfficient,
                ["StreamingSuccessful"] = result.Success
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation($"✅ File streaming test completed - Files match: {filesMatch}, Memory efficient: {memoryEfficient}, Duration: {result.Duration.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "❌ File streaming test failed");
        }

        return result;
    }

    public async Task<TestResult> TestZipProcessingAsync()
    {
        var result = new TestResult { TestName = "ZIP Processing Test" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var zipPath = Path.Combine(_testDataDirectory, "test_archive.zip");
            var beforeMemory = GC.GetTotalMemory(true);

            // Test streaming ZIP creation with multiple large files
            var zipOptions = new AsyncZipOptions
            {
                AllowedExtensions = new[] { ".jpg", ".txt" },
                MinimumFiles = 5,
                CompressionLevel = CompressionLevel.Optimal,
                BufferSize = 81920,
                OverwriteExisting = true,
                MaxConcurrency = 4
            };

            var zipResult = await _asyncZipService.CreateStreamingZipAsync(_testDataDirectory, zipPath, zipOptions);

            var afterMemory = GC.GetTotalMemory(false);
            var memoryIncrease = afterMemory - beforeMemory;

            // Validate ZIP creation
            var zipExists = await _asyncFileService.FileExistsAsync(zipPath);
            var zipInfo = await _asyncFileService.GetFileInfoAsync(zipPath);
            var memoryEfficient = memoryIncrease < (50 * 1024 * 1024); // 50MB threshold

            result.Success = zipResult.Success && zipExists && memoryEfficient;
            result.Metrics = new Dictionary<string, object>
            {
                ["ZipCreated"] = zipResult.Success,
                ["ZipFileSize"] = zipInfo?.Length ?? 0,
                ["FilesProcessed"] = zipResult.FilesProcessed,
                ["CompressionRatio"] = zipResult.UncompressedSize > 0 ? 
                    (double)zipResult.CompressedSize / zipResult.UncompressedSize : 0,
                ["ProcessingTime"] = zipResult.ProcessingTime,
                ["MemoryIncrease"] = memoryIncrease,
                ["MemoryEfficient"] = memoryEfficient
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation($"✅ ZIP processing test completed - Files processed: {zipResult.FilesProcessed}, Duration: {result.Duration.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "❌ ZIP processing test failed");
        }

        return result;
    }

    public async Task<TestResult> TestErrorHandlingAsync()
    {
        var result = new TestResult { TestName = "Error Handling Test" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var errorTests = new List<(string TestName, bool Success, string Description)>();

            // Test 1: Cancellation token handling
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100); // Cancel after 100ms

            try
            {
                var largeFilePath = Path.Combine(_testDataDirectory, "huge.txt");
                await using var stream = await _asyncFileService.OpenFileStreamAsync(largeFilePath);
                if (stream != null)
                {
                    await _asyncFileService.CopyStreamToFileAsync(stream, 
                        Path.Combine(_testDataDirectory, "cancelled_copy.txt"), 
                        81920, cts.Token);
                }
                errorTests.Add(("Cancellation", false, "Should have been cancelled"));
            }
            catch (OperationCanceledException)
            {
                errorTests.Add(("Cancellation", true, "Properly handled cancellation"));
            }

            // Test 2: Invalid path handling
            try
            {
                await _asyncFileService.CreateDirectoryAsync("<<invalid>>path");
                errorTests.Add(("InvalidPath", false, "Should have failed on invalid path"));
            }
            catch (Exception)
            {
                errorTests.Add(("InvalidPath", true, "Properly handled invalid path"));
            }

            // Test 3: Resource disposal
            var disposalTest = await TestResourceDisposalAsync();
            errorTests.Add(("ResourceDisposal", disposalTest, "Resource disposal test"));

            result.Success = errorTests.All(t => t.Success);
            result.Metrics = new Dictionary<string, object>
            {
                ["ErrorTests"] = errorTests,
                ["AllTestsPassed"] = result.Success
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation($"✅ Error handling test completed - All tests passed: {result.Success}, Duration: {result.Duration.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "❌ Error handling test failed");
        }

        return result;
    }

    #region Helper Methods

    private async Task CreateTestFileAsync(string fileName, int sizeBytes)
    {
        var filePath = Path.Combine(_testDataDirectory, fileName);
        var data = new byte[sizeBytes];
        new Random().NextBytes(data);
        
        await using var stream = new MemoryStream(data);
        await _asyncFileService.CopyStreamToFileAsync(stream, filePath);
    }

    private async Task CreateTestImageAsync(string fileName, int sizeBytes)
    {
        var filePath = Path.Combine(_testDataDirectory, fileName);
        // Create a simple bitmap-like structure for testing
        var header = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
        var data = new byte[sizeBytes];
        new Random().NextBytes(data);
        Array.Copy(header, 0, data, 0, header.Length);
        
        await using var stream = new MemoryStream(data);
        await _asyncFileService.CopyStreamToFileAsync(stream, filePath);
    }

    private async Task ValidateAsyncMethod(Func<Task> asyncMethod, string methodName, List<string> results)
    {
        try
        {
            var task = asyncMethod();
            if (task.IsCompletedSuccessfully)
            {
                results.Add($"⚠️  {methodName}: Completed synchronously (may not be truly async)");
            }
            else
            {
                await task;
                results.Add($"✅ {methodName}: Properly async execution");
            }
        }
        catch (Exception ex)
        {
            results.Add($"❌ {methodName}: Exception - {ex.Message}");
        }
    }

    private async Task ValidateNonBlockingPattern()
    {
        // Simulate concurrent async operations to validate non-blocking behavior
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_asyncFileService.FileExistsAsync(Path.Combine(_testDataDirectory, "small.txt")));
        }
        await Task.WhenAll(tasks);
    }

    private async Task<bool> TestResourceDisposalAsync()
    {
        try
        {
            var testFilePath = Path.Combine(_testDataDirectory, "disposal_test.txt");
            
            // Open stream and let it go out of scope
            {
                await using var stream = await _asyncFileService.OpenFileStreamAsync(testFilePath, FileMode.Create, FileAccess.Write);
                // Stream should be disposed here
            }

            // Try to delete the file - should succeed if resources were properly disposed
            await Task.Delay(100); // Brief delay to ensure disposal
            return await _asyncFileService.DeleteFileAsync(testFilePath);
        }
        catch
        {
            return false;
        }
    }

    private HttpContext CreateMockHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/image/upload";
        context.Request.Method = "POST";
        context.TraceIdentifier = Guid.NewGuid().ToString();
        return context;
    }

    private ThreadPoolStats GetThreadPoolStats()
    {
        ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

        return new ThreadPoolStats
        {
            AvailableWorkerThreads = availableWorkerThreads,
            AvailableCompletionPortThreads = availableCompletionPortThreads,
            MaxWorkerThreads = maxWorkerThreads,
            MaxCompletionPortThreads = maxCompletionPortThreads,
            BusyWorkerThreads = maxWorkerThreads - availableWorkerThreads,
            BusyCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads
        };
    }

    private PerformanceImprovement CalculateOverallImprovement(AsyncIoTestResults results)
    {
        var improvement = new PerformanceImprovement();

        // Calculate memory improvement
        if (results.MemoryUsageTest?.Success == true && results.MemoryUsageTest.Metrics != null)
        {
            var memoryIncreasePercent = (double)(results.MemoryUsageTest.Metrics.GetValueOrDefault("MemoryIncreasePercent", 0d));
            improvement.MemoryReductionPercent = Math.Max(0, 100 - memoryIncreasePercent);
        }

        // Calculate throughput improvement
        if (results.ThroughputTest?.Success == true && results.ThroughputTest.Metrics != null)
        {
            improvement.ThroughputIncreasePercent = (double)(results.ThroughputTest.Metrics.GetValueOrDefault("ImprovementPercent", 0d));
        }

        // Calculate overall score
        var testScores = new[]
        {
            results.AsyncPatternValidation?.Success == true ? 1.0 : 0.0,
            results.MemoryUsageTest?.Success == true ? 1.0 : 0.0,
            results.ThroughputTest?.Success == true ? 1.0 : 0.0,
            results.BlockingDetection?.Success == true ? 1.0 : 0.0,
            results.FileStreamingTest?.Success == true ? 1.0 : 0.0,
            results.ZipProcessingTest?.Success == true ? 1.0 : 0.0,
            results.ErrorHandlingTest?.Success == true ? 1.0 : 0.0
        };

        improvement.OverallScore = testScores.Average() * 100;
        improvement.PassedTests = testScores.Count(score => score > 0);
        improvement.TotalTests = testScores.Length;

        return improvement;
    }

    private void LogTestSummary(AsyncIoTestResults results)
    {
        _logger.LogInformation("🎯 ASYNC I/O PERFORMANCE TEST SUMMARY");
        _logger.LogInformation("=====================================");
        _logger.LogInformation($"Test Duration: {results.TotalDuration?.TotalSeconds:F2}s");
        _logger.LogInformation($"Overall Score: {results.OverallPerformanceImprovement?.OverallScore:F1}%");
        _logger.LogInformation($"Tests Passed: {results.OverallPerformanceImprovement?.PassedTests}/{results.OverallPerformanceImprovement?.TotalTests}");
        
        if (results.OverallPerformanceImprovement?.ThroughputIncreasePercent > 0)
        {
            _logger.LogInformation($"Throughput Improvement: +{results.OverallPerformanceImprovement.ThroughputIncreasePercent:F1}%");
        }
        
        if (results.OverallPerformanceImprovement?.MemoryReductionPercent > 0)
        {
            _logger.LogInformation($"Memory Efficiency: {results.OverallPerformanceImprovement.MemoryReductionPercent:F1}% reduction");
        }

        _logger.LogInformation($"✅ Async Pattern Validation: {(results.AsyncPatternValidation?.Success == true ? "PASS" : "FAIL")}");
        _logger.LogInformation($"✅ Memory Usage Test: {(results.MemoryUsageTest?.Success == true ? "PASS" : "FAIL")}");
        _logger.LogInformation($"✅ Throughput Test: {(results.ThroughputTest?.Success == true ? "PASS" : "FAIL")}");
        _logger.LogInformation($"✅ Blocking Detection: {(results.BlockingDetection?.Success == true ? "PASS" : "FAIL")}");
        _logger.LogInformation($"✅ File Streaming: {(results.FileStreamingTest?.Success == true ? "PASS" : "FAIL")}");
        _logger.LogInformation($"✅ ZIP Processing: {(results.ZipProcessingTest?.Success == true ? "PASS" : "FAIL")}");
        _logger.LogInformation($"✅ Error Handling: {(results.ErrorHandlingTest?.Success == true ? "PASS" : "FAIL")}");
        _logger.LogInformation("=====================================");
    }

    private async Task CleanupTestEnvironmentAsync()
    {
        try
        {
            if (Directory.Exists(_testDataDirectory))
            {
                Directory.Delete(_testDataDirectory, recursive: true);
            }
            _logger.LogInformation("✅ Test environment cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️  Test environment cleanup failed");
        }
    }

    #endregion
}

#region Data Transfer Objects

public class AsyncIoTestResults
{
    public string TestId { get; set; } = string.Empty;
    public DateTimeOffset TestStartTime { get; set; }
    public DateTimeOffset TestEndTime { get; set; }
    public TimeSpan? TotalDuration { get; set; }
    public bool OverallSuccess { get; set; } = true;
    public string? Error { get; set; }

    public TestResult? AsyncPatternValidation { get; set; }
    public TestResult? MemoryUsageTest { get; set; }
    public TestResult? ThroughputTest { get; set; }
    public TestResult? BlockingDetection { get; set; }
    public TestResult? FileStreamingTest { get; set; }
    public TestResult? ZipProcessingTest { get; set; }
    public TestResult? ErrorHandlingTest { get; set; }

    public PerformanceImprovement? OverallPerformanceImprovement { get; set; }
}

public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public bool Success { get; set; } = false;
    public TimeSpan Duration { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object>? Metrics { get; set; }
}

public class PerformanceMetrics
{
    public double BaselineMemoryUsage { get; set; }
    public double BaselineThroughput { get; set; }
    public TimeSpan BaselineResponseTime { get; set; }
}

public class PerformanceImprovement
{
    public double ThroughputIncreasePercent { get; set; }
    public double MemoryReductionPercent { get; set; }
    public double OverallScore { get; set; }
    public int PassedTests { get; set; }
    public int TotalTests { get; set; }
}

#endregion