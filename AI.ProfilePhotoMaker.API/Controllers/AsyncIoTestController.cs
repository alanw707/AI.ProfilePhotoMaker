using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AI.ProfilePhotoMaker.API.Tests;
using AI.ProfilePhotoMaker.API.Services;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for executing async I/O performance tests and monitoring blocking operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AsyncIoTestController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AsyncIoTestController> _logger;

    public AsyncIoTestController(IServiceProvider serviceProvider, ILogger<AsyncIoTestController> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Execute comprehensive async I/O performance test suite
    /// </summary>
    [HttpPost("comprehensive")]
    [AllowAnonymous] // For testing purposes - remove in production
    public async Task<IActionResult> ExecuteComprehensiveTests()
    {
        try
        {
            _logger.LogInformation("Starting comprehensive async I/O performance tests");

            var testRunner = new AsyncIoPerformanceTests(_serviceProvider);
            var results = await testRunner.ExecuteComprehensiveTestSuiteAsync();

            var response = new
            {
                success = results.OverallSuccess,
                data = results,
                summary = new
                {
                    testId = results.TestId,
                    duration = results.TotalDuration?.TotalSeconds,
                    overallScore = results.OverallPerformanceImprovement?.OverallScore,
                    testsPassedRatio = $"{results.OverallPerformanceImprovement?.PassedTests}/{results.OverallPerformanceImprovement?.TotalTests}",
                    throughputImprovement = results.OverallPerformanceImprovement?.ThroughputIncreasePercent,
                    memoryEfficiency = results.OverallPerformanceImprovement?.MemoryReductionPercent,
                    testResults = new
                    {
                        asyncPatternValidation = results.AsyncPatternValidation?.Success,
                        memoryUsageTest = results.MemoryUsageTest?.Success,
                        throughputTest = results.ThroughputTest?.Success,
                        blockingDetection = results.BlockingDetection?.Success,
                        fileStreamingTest = results.FileStreamingTest?.Success,
                        zipProcessingTest = results.ZipProcessingTest?.Success,
                        errorHandlingTest = results.ErrorHandlingTest?.Success
                    }
                }
            };

            if (results.OverallSuccess)
            {
                _logger.LogInformation("✅ Comprehensive async I/O tests completed successfully");
                return Ok(response);
            }
            else
            {
                _logger.LogWarning("⚠️  Some async I/O tests failed");
                return Ok(response); // Still return 200 with test results
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Comprehensive async I/O tests failed");
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "TestExecutionFailed",
                    message = "Failed to execute async I/O performance tests",
                    detail = ex.Message
                }
            });
        }
    }

    /// <summary>
    /// Execute async pattern validation test only
    /// </summary>
    [HttpPost("async-patterns")]
    [AllowAnonymous]
    public async Task<IActionResult> TestAsyncPatterns()
    {
        try
        {
            var testRunner = new AsyncIoPerformanceTests(_serviceProvider);
            
            // Execute setup
            await testRunner.SetupTestEnvironmentAsync();

            // Run specific test
            var result = await testRunner.TestAsyncPatternValidationAsync();

            return Ok(new
            {
                success = result.Success,
                testName = result.TestName,
                duration = result.Duration.TotalMilliseconds,
                metrics = result.Metrics,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Async pattern validation test failed");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Execute memory usage test to validate streaming efficiency
    /// </summary>
    [HttpPost("memory-usage")]
    [AllowAnonymous]
    public async Task<IActionResult> TestMemoryUsage()
    {
        try
        {
            var testRunner = new AsyncIoPerformanceTests(_serviceProvider);
            
            await testRunner.SetupTestEnvironmentAsync();
            var result = await testRunner.TestMemoryUsageAsync();

            return Ok(new
            {
                success = result.Success,
                testName = result.TestName,
                duration = result.Duration.TotalMilliseconds,
                metrics = result.Metrics,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory usage test failed");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Execute throughput test to measure concurrent operation improvements
    /// </summary>
    [HttpPost("throughput")]
    [AllowAnonymous]
    public async Task<IActionResult> TestThroughput()
    {
        try
        {
            var testRunner = new AsyncIoPerformanceTests(_serviceProvider);
            
            await testRunner.SetupTestEnvironmentAsync();
            var result = await testRunner.TestThroughputAsync();

            return Ok(new
            {
                success = result.Success,
                testName = result.TestName,
                duration = result.Duration.TotalMilliseconds,
                metrics = result.Metrics,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Throughput test failed");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Execute ZIP processing test to validate streaming compression
    /// </summary>
    [HttpPost("zip-processing")]
    [AllowAnonymous]
    public async Task<IActionResult> TestZipProcessing()
    {
        try
        {
            var testRunner = new AsyncIoPerformanceTests(_serviceProvider);
            
            await testRunner.SetupTestEnvironmentAsync();
            var result = await testRunner.TestZipProcessingAsync();

            return Ok(new
            {
                success = result.Success,
                testName = result.TestName,
                duration = result.Duration.TotalMilliseconds,
                metrics = result.Metrics,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZIP processing test failed");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get current thread pool statistics for monitoring blocking operations
    /// </summary>
    [HttpGet("thread-pool-stats")]
    [AllowAnonymous]
    public IActionResult GetThreadPoolStats()
    {
        try
        {
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

            var stats = new
            {
                timestamp = DateTimeOffset.UtcNow,
                availableWorkerThreads,
                availableCompletionPortThreads,
                maxWorkerThreads,
                maxCompletionPortThreads,
                busyWorkerThreads = maxWorkerThreads - availableWorkerThreads,
                busyCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads,
                healthStatus = new
                {
                    healthy = availableWorkerThreads > 5 && availableCompletionPortThreads > 5,
                    workerThreadsLow = availableWorkerThreads <= 5,
                    completionPortsLow = availableCompletionPortThreads <= 5
                }
            };

            return Ok(new
            {
                success = true,
                data = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get thread pool statistics");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Test file streaming operations with various file sizes
    /// </summary>
    [HttpPost("file-streaming")]
    [AllowAnonymous]
    public async Task<IActionResult> TestFileStreaming([FromQuery] int fileSizeMB = 10)
    {
        try
        {
            if (fileSizeMB <= 0 || fileSizeMB > 100)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "File size must be between 1 and 100 MB"
                });
            }

            var testRunner = new AsyncIoPerformanceTests(_serviceProvider);
            
            await testRunner.SetupTestEnvironmentAsync();
            var result = await testRunner.TestFileStreamingAsync();

            return Ok(new
            {
                success = result.Success,
                testName = result.TestName,
                duration = result.Duration.TotalMilliseconds,
                fileSizeMB = fileSizeMB,
                metrics = result.Metrics,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File streaming test failed");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Simulate high load to test blocking operation detection
    /// </summary>
    [HttpPost("blocking-detection")]
    [AllowAnonymous]
    public async Task<IActionResult> TestBlockingDetection([FromQuery] int concurrency = 10)
    {
        try
        {
            if (concurrency <= 0 || concurrency > 50)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Concurrency must be between 1 and 50"
                });
            }

            var testRunner = new AsyncIoPerformanceTests(_serviceProvider);
            
            await testRunner.SetupTestEnvironmentAsync();
            var result = await testRunner.TestBlockingDetectionAsync();

            return Ok(new
            {
                success = result.Success,
                testName = result.TestName,
                duration = result.Duration.TotalMilliseconds,
                concurrency = concurrency,
                metrics = result.Metrics,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blocking detection test failed");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Run a quick health check on async I/O services
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> HealthCheck()
    {
        try
        {
            var asyncFileService = _serviceProvider.GetService<IAsyncFileService>();
            var asyncZipService = _serviceProvider.GetService<IAsyncZipService>();

            var health = new
            {
                timestamp = DateTimeOffset.UtcNow,
                services = new
                {
                    asyncFileService = asyncFileService != null ? "registered" : "not_registered",
                    asyncZipService = asyncZipService != null ? "registered" : "not_registered"
                },
                threadPool = await GetThreadPoolHealthAsync(),
                systemInfo = new
                {
                    processorCount = Environment.ProcessorCount,
                    workingSet = Environment.WorkingSet,
                    gcMemory = GC.GetTotalMemory(false)
                }
            };

            var allServicesRegistered = asyncFileService != null && asyncZipService != null;

            return Ok(new
            {
                success = allServicesRegistered,
                data = health,
                message = allServicesRegistered ? 
                    "All async I/O services are healthy" : 
                    "Some async I/O services are not registered"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private async Task<object> GetThreadPoolHealthAsync()
    {
        return await Task.Run(() =>
        {
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

            return new
            {
                healthy = availableWorkerThreads > 5 && availableCompletionPortThreads > 5,
                availableWorkerThreads,
                availableCompletionPortThreads,
                maxWorkerThreads,
                maxCompletionPortThreads
            };
        });
    }
}