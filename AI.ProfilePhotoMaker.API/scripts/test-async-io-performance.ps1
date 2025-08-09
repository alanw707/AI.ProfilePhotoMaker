# Async I/O Performance Testing Script
# Tests all async I/O improvements and validates performance targets

param(
    [string]$BaseUrl = "https://localhost:5001",
    [string]$OutputPath = "./async-io-test-results.json",
    [switch]$Verbose = $false
)

Write-Host "🚀 AI Profile Photo Maker - Async I/O Performance Testing" -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Green
Write-Host "Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "Output Path: $OutputPath" -ForegroundColor Cyan
Write-Host ""

# Test results collection
$testResults = @{
    testId = [Guid]::NewGuid().ToString()
    timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
    baseUrl = $BaseUrl
    tests = @{}
    summary = @{
        totalTests = 0
        passedTests = 0
        failedTests = 0
        overallScore = 0
        totalDuration = 0
    }
}

function Test-Endpoint {
    param(
        [string]$Endpoint,
        [string]$Method = "POST",
        [string]$TestName,
        [hashtable]$ExpectedMetrics = @{}
    )
    
    Write-Host "🔄 Testing: $TestName" -ForegroundColor Yellow
    
    $testResult = @{
        testName = $TestName
        endpoint = $Endpoint
        method = $Method
        success = $false
        duration = 0
        response = $null
        error = $null
        metrics = @{}
        timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
    }
    
    try {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        
        $response = if ($Method -eq "POST") {
            Invoke-RestMethod -Uri "$BaseUrl$Endpoint" -Method Post -ContentType "application/json" -Body "{}"
        } else {
            Invoke-RestMethod -Uri "$BaseUrl$Endpoint" -Method Get
        }
        
        $stopwatch.Stop()
        
        $testResult.duration = $stopwatch.ElapsedMilliseconds
        $testResult.response = $response
        $testResult.success = $response.success -eq $true
        
        if ($response.data) {
            $testResult.metrics = $response.data
        } elseif ($response.metrics) {
            $testResult.metrics = $response.metrics
        }
        
        # Validate expected metrics
        if ($ExpectedMetrics.Count -gt 0) {
            foreach ($key in $ExpectedMetrics.Keys) {
                $expectedValue = $ExpectedMetrics[$key]
                $actualValue = $testResult.metrics.$key
                
                if ($null -eq $actualValue) {
                    Write-Host "⚠️  Missing expected metric: $key" -ForegroundColor Red
                    $testResult.success = $false
                } elseif ($expectedValue -is [double] -and $actualValue -lt $expectedValue) {
                    Write-Host "⚠️  Metric $key ($actualValue) below expected value ($expectedValue)" -ForegroundColor Red
                    $testResult.success = $false
                }
            }
        }
        
        if ($testResult.success) {
            Write-Host "✅ $TestName passed (${$testResult.duration}ms)" -ForegroundColor Green
            $testResults.summary.passedTests++
        } else {
            Write-Host "❌ $TestName failed (${$testResult.duration}ms)" -ForegroundColor Red
            $testResults.summary.failedTests++
        }
        
    } catch {
        $testResult.error = $_.Exception.Message
        $testResult.success = $false
        Write-Host "❌ $TestName failed: $($_.Exception.Message)" -ForegroundColor Red
        $testResults.summary.failedTests++
    }
    
    $testResults.summary.totalTests++
    $testResults.summary.totalDuration += $testResult.duration
    
    return $testResult
}

# Health Check Test
Write-Host "📋 Phase 1: Health Check and Service Registration" -ForegroundColor Blue
$healthTest = Test-Endpoint -Endpoint "/api/asynciotest/health" -Method "GET" -TestName "Service Health Check"
$testResults.tests.healthCheck = $healthTest

if (-not $healthTest.success) {
    Write-Host "🚫 Health check failed. Async I/O services may not be registered properly." -ForegroundColor Red
    Write-Host "   Make sure AsyncFileService and AsyncZipService are registered in Program.cs" -ForegroundColor Yellow
}

# Thread Pool Statistics
Write-Host "`n📋 Phase 2: Thread Pool Monitoring" -ForegroundColor Blue
$threadPoolTest = Test-Endpoint -Endpoint "/api/asynciotest/thread-pool-stats" -Method "GET" -TestName "Thread Pool Statistics"
$testResults.tests.threadPoolStats = $threadPoolTest

# Async Pattern Validation
Write-Host "`n📋 Phase 3: Async Pattern Validation" -ForegroundColor Blue
$asyncPatternsTest = Test-Endpoint -Endpoint "/api/asynciotest/async-patterns" -Method "POST" -TestName "Async Pattern Validation"
$testResults.tests.asyncPatterns = $asyncPatternsTest

# Memory Usage Test
Write-Host "`n📋 Phase 4: Memory Usage and Streaming Efficiency" -ForegroundColor Blue
$memoryTest = Test-Endpoint -Endpoint "/api/asynciotest/memory-usage" -Method "POST" -TestName "Memory Usage Test" -ExpectedMetrics @{
    "MemoryEfficient" = $true
    "WorkingSetEfficient" = $true
}
$testResults.tests.memoryUsage = $memoryTest

# Throughput Test
Write-Host "`n📋 Phase 5: Throughput and Concurrency" -ForegroundColor Blue
$throughputTest = Test-Endpoint -Endpoint "/api/asynciotest/throughput" -Method "POST" -TestName "Throughput Test" -ExpectedMetrics @{
    "ImprovementPercent" = 40.0
}
$testResults.tests.throughput = $throughputTest

# File Streaming Test
Write-Host "`n📋 Phase 6: File Streaming Operations" -ForegroundColor Blue
$streamingTest = Test-Endpoint -Endpoint "/api/asynciotest/file-streaming?fileSizeMB=10" -Method "POST" -TestName "File Streaming Test"
$testResults.tests.fileStreaming = $streamingTest

# ZIP Processing Test
Write-Host "`n📋 Phase 7: ZIP Processing and Compression" -ForegroundColor Blue
$zipTest = Test-Endpoint -Endpoint "/api/asynciotest/zip-processing" -Method "POST" -TestName "ZIP Processing Test"
$testResults.tests.zipProcessing = $zipTest

# Blocking Detection Test
Write-Host "`n📋 Phase 8: Blocking Operation Detection" -ForegroundColor Blue
$blockingTest = Test-Endpoint -Endpoint "/api/asynciotest/blocking-detection?concurrency=10" -Method "POST" -TestName "Blocking Detection Test"
$testResults.tests.blockingDetection = $blockingTest

# Comprehensive Test Suite
Write-Host "`n📋 Phase 9: Comprehensive Test Suite" -ForegroundColor Blue
$comprehensiveTest = Test-Endpoint -Endpoint "/api/asynciotest/comprehensive" -Method "POST" -TestName "Comprehensive Test Suite"
$testResults.tests.comprehensive = $comprehensiveTest

# Calculate overall results
Write-Host "`n📊 Calculating Overall Results..." -ForegroundColor Blue

$testResults.summary.overallScore = if ($testResults.summary.totalTests -gt 0) {
    ($testResults.summary.passedTests / $testResults.summary.totalTests) * 100
} else { 0 }

# Performance targets validation
$performanceTargets = @{
    "Memory Usage" = $memoryTest.success
    "Throughput Improvement" = $throughputTest.success
    "File Streaming" = $streamingTest.success
    "ZIP Processing" = $zipTest.success
    "No Blocking Operations" = $blockingTest.success
}

$targetsMet = ($performanceTargets.Values | Where-Object { $_ -eq $true }).Count
$totalTargets = $performanceTargets.Count

# Display Results Summary
Write-Host "`n🎯 ASYNC I/O PERFORMANCE TEST RESULTS" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host "Test ID: $($testResults.testId)" -ForegroundColor Cyan
Write-Host "Total Duration: $($testResults.summary.totalDuration)ms" -ForegroundColor Cyan
Write-Host "Overall Score: $([math]::Round($testResults.summary.overallScore, 1))%" -ForegroundColor $(if ($testResults.summary.overallScore -ge 80) { "Green" } elseif ($testResults.summary.overallScore -ge 60) { "Yellow" } else { "Red" })
Write-Host "Tests Passed: $($testResults.summary.passedTests)/$($testResults.summary.totalTests)" -ForegroundColor $(if ($testResults.summary.passedTests -eq $testResults.summary.totalTests) { "Green" } else { "Yellow" })
Write-Host "Performance Targets Met: $targetsMet/$totalTargets" -ForegroundColor $(if ($targetsMet -eq $totalTargets) { "Green" } else { "Yellow" })

Write-Host "`nDetailed Test Results:" -ForegroundColor White
foreach ($target in $performanceTargets.GetEnumerator()) {
    $status = if ($target.Value) { "✅ PASS" } else { "❌ FAIL" }
    $color = if ($target.Value) { "Green" } else { "Red" }
    Write-Host "  $($target.Key): $status" -ForegroundColor $color
}

# Performance Metrics Summary
if ($comprehensiveTest.success -and $comprehensiveTest.response.summary) {
    $summary = $comprehensiveTest.response.summary
    Write-Host "`nPerformance Improvements:" -ForegroundColor White
    
    if ($summary.throughputImprovement) {
        Write-Host "  Throughput: +$([math]::Round($summary.throughputImprovement, 1))%" -ForegroundColor Green
    }
    
    if ($summary.memoryEfficiency) {
        Write-Host "  Memory Efficiency: $([math]::Round($summary.memoryEfficiency, 1))%" -ForegroundColor Green
    }
}

# Recommendations based on results
Write-Host "`n💡 Recommendations:" -ForegroundColor Yellow
if (-not $healthTest.success) {
    Write-Host "  🔧 Register AsyncFileService and AsyncZipService in Program.cs" -ForegroundColor Red
}
if (-not $memoryTest.success) {
    Write-Host "  🔧 Review memory usage - streaming may not be working correctly" -ForegroundColor Red
}
if (-not $throughputTest.success) {
    Write-Host "  🔧 Async operations may not be providing expected performance gains" -ForegroundColor Red
}
if (-not $blockingTest.success) {
    Write-Host "  🔧 Blocking operations detected - review async/await patterns" -ForegroundColor Red
}

# Save results to file
Write-Host "`n💾 Saving results to: $OutputPath" -ForegroundColor Cyan
$testResults | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8

# Final status
$overallSuccess = $testResults.summary.overallScore -ge 80 -and $targetsMet -eq $totalTargets

if ($overallSuccess) {
    Write-Host "`n🎉 Async I/O Performance Testing PASSED!" -ForegroundColor Green
    Write-Host "   All performance targets met. Async I/O improvements are working correctly." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️  Async I/O Performance Testing needs attention" -ForegroundColor Yellow
    Write-Host "   Some performance targets not met. Review failed tests above." -ForegroundColor Yellow
    exit 1
}