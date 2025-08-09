#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Comprehensive deployment validation script for AI Profile Photo Maker
    
.DESCRIPTION
    Performs thorough validation of deployment readiness including:
    - Environment configuration validation
    - Security configuration checks
    - Performance baseline verification
    - Azure services connectivity
    - Database readiness validation
    - Regression testing
    - Deployment health monitoring

.PARAMETER BaseUrl
    The base URL of the deployed application (default: http://localhost:5000)

.PARAMETER Environment
    The target environment (Development, Staging, Production)

.PARAMETER OutputFormat
    Output format: Console, Json, JUnit (default: Console)

.PARAMETER OutputFile
    Output file path (optional, for Json or JUnit formats)

.PARAMETER FailOnWarnings
    Exit with non-zero code on warnings (default: false)

.PARAMETER SkipRegressionTests
    Skip regression tests (default: false)

.PARAMETER Timeout
    Request timeout in seconds (default: 30)

.EXAMPLE
    ./validate-deployment.ps1 -BaseUrl "https://aipm-api.eastus2.azurecontainerapps.io" -Environment "Production"
    
.EXAMPLE
    ./validate-deployment.ps1 -BaseUrl "http://localhost:5000" -Environment "Development" -OutputFormat "Json" -OutputFile "validation-results.json"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$BaseUrl = "http://localhost:5000",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment = "Development",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Console", "Json", "JUnit")]
    [string]$OutputFormat = "Console",
    
    [Parameter(Mandatory = $false)]
    [string]$OutputFile = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$FailOnWarnings = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipRegressionTests = $false,
    
    [Parameter(Mandatory = $false)]
    [int]$Timeout = 30
)

# Global variables
$script:ValidationResults = @()
$script:TotalTests = 0
$script:PassedTests = 0
$script:FailedTests = 0
$script:WarningTests = 0
$script:StartTime = Get-Date

# Colors for console output
$Colors = @{
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "Cyan"
    Header = "Magenta"
}

function Write-Header {
    param([string]$Message)
    Write-Host "`n" -NoNewline
    Write-Host "=" * 80 -ForegroundColor $Colors.Header
    Write-Host " $Message" -ForegroundColor $Colors.Header
    Write-Host "=" * 80 -ForegroundColor $Colors.Header
}

function Write-TestResult {
    param(
        [string]$TestName,
        [string]$Status,
        [string]$Message = "",
        [object]$Data = $null
    )
    
    $script:TotalTests++
    
    $result = @{
        TestName = $TestName
        Status = $Status
        Message = $Message
        Data = $Data
        Timestamp = Get-Date
    }
    
    $script:ValidationResults += $result
    
    switch ($Status.ToLower()) {
        "pass" { 
            $script:PassedTests++
            Write-Host "✅ $TestName" -ForegroundColor $Colors.Success
            if ($Message) { Write-Host "   $Message" -ForegroundColor Gray }
        }
        "fail" { 
            $script:FailedTests++
            Write-Host "❌ $TestName" -ForegroundColor $Colors.Error
            if ($Message) { Write-Host "   $Message" -ForegroundColor $Colors.Error }
        }
        "warning" { 
            $script:WarningTests++
            Write-Host "⚠️  $TestName" -ForegroundColor $Colors.Warning
            if ($Message) { Write-Host "   $Message" -ForegroundColor $Colors.Warning }
        }
        "info" {
            Write-Host "ℹ️  $TestName" -ForegroundColor $Colors.Info
            if ($Message) { Write-Host "   $Message" -ForegroundColor Gray }
        }
    }
}

function Invoke-ApiRequest {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [object]$Body = $null
    )
    
    try {
        $uri = "$BaseUrl/$Endpoint".Replace("//", "/").Replace("http:/", "http://").Replace("https:/", "https://")
        
        $headers = @{
            "Accept" = "application/json"
            "User-Agent" = "DeploymentValidator/1.0"
        }
        
        $params = @{
            Uri = $uri
            Method = $Method
            Headers = $headers
            TimeoutSec = $Timeout
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-RestMethod @params
        return @{ Success = $true; Data = $response; StatusCode = 200 }
    }
    catch {
        $statusCode = 500
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        return @{ Success = $false; Error = $_.Exception.Message; StatusCode = $statusCode }
    }
}

function Test-BasicConnectivity {
    Write-Header "Basic Connectivity Tests"
    
    # Test basic API connectivity
    $result = Invoke-ApiRequest -Endpoint "health"
    if ($result.Success) {
        Write-TestResult -TestName "API Connectivity" -Status "Pass" -Message "API is reachable"
        
        # Test comprehensive health endpoint
        $healthResult = Invoke-ApiRequest -Endpoint "api/health/comprehensive"
        if ($healthResult.Success) {
            $health = $healthResult.Data
            $status = if ($health.status -eq "Healthy") { "Pass" } elseif ($health.status -eq "Degraded") { "Warning" } else { "Fail" }
            Write-TestResult -TestName "Application Health" -Status $status -Message "Status: $($health.status)" -Data $health
        } else {
            Write-TestResult -TestName "Application Health" -Status "Fail" -Message $healthResult.Error
        }
    } else {
        Write-TestResult -TestName "API Connectivity" -Status "Fail" -Message $result.Error
        Write-TestResult -TestName "Application Health" -Status "Fail" -Message "Cannot reach API"
        return $false
    }
    
    return $true
}

function Test-DeploymentReadiness {
    Write-Header "Deployment Readiness Validation"
    
    # Get overall readiness score
    $result = Invoke-ApiRequest -Endpoint "api/deployment/readiness-score"
    if ($result.Success) {
        $readiness = $result.Data
        $score = $readiness.overallScore
        
        if ($score -ge 90) {
            Write-TestResult -TestName "Readiness Score" -Status "Pass" -Message "Score: $score% ($($readiness.readinessLevel))"
        } elseif ($score -ge 75) {
            Write-TestResult -TestName "Readiness Score" -Status "Warning" -Message "Score: $score% ($($readiness.readinessLevel))"
        } else {
            Write-TestResult -TestName "Readiness Score" -Status "Fail" -Message "Score: $score% ($($readiness.readinessLevel))"
        }
        
        # Check component scores
        if ($readiness.componentScores) {
            foreach ($component in $readiness.componentScores.PSObject.Properties) {
                $componentData = $component.Value
                $componentScore = $componentData.score
                
                if ($componentScore -ge 80) {
                    Write-TestResult -TestName "Component: $($component.Name)" -Status "Pass" -Message "Score: $componentScore%"
                } elseif ($componentScore -ge 60) {
                    Write-TestResult -TestName "Component: $($component.Name)" -Status "Warning" -Message "Score: $componentScore%"
                } else {
                    Write-TestResult -TestName "Component: $($component.Name)" -Status "Fail" -Message "Score: $componentScore%"
                }
            }
        }
        
        # Check for blocking issues
        if ($readiness.blockingIssues -and $readiness.blockingIssues.Count -gt 0) {
            Write-TestResult -TestName "Blocking Issues" -Status "Fail" -Message "$($readiness.blockingIssues.Count) blocking issues found"
            foreach ($issue in $readiness.blockingIssues) {
                Write-Host "     - $issue" -ForegroundColor $Colors.Error
            }
        } else {
            Write-TestResult -TestName "Blocking Issues" -Status "Pass" -Message "No blocking issues"
        }
    } else {
        Write-TestResult -TestName "Readiness Score" -Status "Fail" -Message $result.Error
    }
}

function Test-Configuration {
    Write-Header "Configuration Validation"
    
    $result = Invoke-ApiRequest -Endpoint "api/deployment/validate/configuration"
    if ($result.Success) {
        $config = $result.Data
        
        if ($config.isValid) {
            Write-TestResult -TestName "Configuration Valid" -Status "Pass" -Message "All configuration is valid"
        } else {
            Write-TestResult -TestName "Configuration Valid" -Status "Fail" -Message "$($config.errors.Count) configuration errors"
        }
        
        # Check missing required variables
        if ($config.missingRequired -and $config.missingRequired.Count -gt 0) {
            Write-TestResult -TestName "Required Variables" -Status "Fail" -Message "$($config.missingRequired.Count) missing required variables"
        } else {
            Write-TestResult -TestName "Required Variables" -Status "Pass" -Message "All required variables configured"
        }
        
        # Check for defaults being used
        if ($config.usingDefaults -and $config.usingDefaults.Count -gt 0) {
            Write-TestResult -TestName "Default Values" -Status "Warning" -Message "$($config.usingDefaults.Count) default values in use"
        } else {
            Write-TestResult -TestName "Default Values" -Status "Pass" -Message "No default values detected"
        }
    } else {
        Write-TestResult -TestName "Configuration Validation" -Status "Fail" -Message $result.Error
    }
}

function Test-Security {
    Write-Header "Security Configuration"
    
    $result = Invoke-ApiRequest -Endpoint "api/deployment/validate/security"
    if ($result.Success) {
        $security = $result.Data
        
        if ($security.isValid) {
            Write-TestResult -TestName "Security Configuration" -Status "Pass" -Message "Security score: $($security.securityScore)"
        } else {
            $status = if ($security.hasSecurityIssues) { "Fail" } else { "Warning" }
            Write-TestResult -TestName "Security Configuration" -Status $status -Message "Security score: $($security.securityScore)"
        }
        
        # Check individual security checks
        if ($security.securityChecks) {
            $criticalFailed = 0
            $highFailed = 0
            
            foreach ($check in $security.securityChecks) {
                if (-not $check.passed) {
                    if ($check.severity -eq "Critical") {
                        $criticalFailed++
                    } elseif ($check.severity -eq "High") {
                        $highFailed++
                    }
                }
            }
            
            if ($criticalFailed -gt 0) {
                Write-TestResult -TestName "Critical Security Checks" -Status "Fail" -Message "$criticalFailed critical security issues"
            } elseif ($highFailed -gt 0) {
                Write-TestResult -TestName "High Priority Security Checks" -Status "Warning" -Message "$highFailed high priority issues"
            } else {
                Write-TestResult -TestName "Security Checks" -Status "Pass" -Message "All security checks passed"
            }
        }
    } else {
        Write-TestResult -TestName "Security Configuration" -Status "Fail" -Message $result.Error
    }
}

function Test-Performance {
    Write-Header "Performance Validation"
    
    $result = Invoke-ApiRequest -Endpoint "api/deployment/validate/performance"
    if ($result.Success) {
        $performance = $result.Data
        
        if ($performance.meetsRequirements) {
            Write-TestResult -TestName "Performance Requirements" -Status "Pass" -Message "All performance baselines met"
        } else {
            Write-TestResult -TestName "Performance Requirements" -Status "Fail" -Message "$($performance.issues.Count) performance issues"
        }
        
        # Check specific metrics
        if ($performance.testResults) {
            $results = $performance.testResults
            $baseline = $performance.baseline
            
            # Response time check
            if ($results.averageResponseTimeMs -le $baseline.maxResponseTimeMs) {
                Write-TestResult -TestName "Response Time" -Status "Pass" -Message "$($results.averageResponseTimeMs)ms (baseline: $($baseline.maxResponseTimeMs)ms)"
            } else {
                Write-TestResult -TestName "Response Time" -Status "Fail" -Message "$($results.averageResponseTimeMs)ms exceeds baseline $($baseline.maxResponseTimeMs)ms"
            }
            
            # Error rate check
            if ($results.errorRatePercent -le $baseline.maxErrorRatePercent) {
                Write-TestResult -TestName "Error Rate" -Status "Pass" -Message "$($results.errorRatePercent)% (baseline: $($baseline.maxErrorRatePercent)%)"
            } else {
                Write-TestResult -TestName "Error Rate" -Status "Fail" -Message "$($results.errorRatePercent)% exceeds baseline $($baseline.maxErrorRatePercent)%"
            }
            
            # Memory usage check
            if ($results.memoryUsagePercent -le $baseline.maxMemoryUsagePercent) {
                Write-TestResult -TestName "Memory Usage" -Status "Pass" -Message "$($results.memoryUsagePercent)% (baseline: $($baseline.maxMemoryUsagePercent)%)"
            } else {
                Write-TestResult -TestName "Memory Usage" -Status "Fail" -Message "$($results.memoryUsagePercent)% exceeds baseline $($baseline.maxMemoryUsagePercent)%"
            }
        }
    } else {
        Write-TestResult -TestName "Performance Validation" -Status "Fail" -Message $result.Error
    }
}

function Test-AzureServices {
    Write-Header "Azure Services Connectivity"
    
    $result = Invoke-ApiRequest -Endpoint "api/deployment/validate/azure"
    if ($result.Success) {
        $azure = $result.Data
        
        if ($azure.allServicesHealthy) {
            Write-TestResult -TestName "Azure Services" -Status "Pass" -Message "All Azure services healthy"
        } else {
            Write-TestResult -TestName "Azure Services" -Status "Fail" -Message "$($azure.failedServices.Count) services failed"
        }
        
        # Check individual services
        if ($azure.services) {
            foreach ($service in $azure.services.PSObject.Properties) {
                $serviceData = $service.Value
                
                if ($serviceData.isHealthy) {
                    Write-TestResult -TestName "Service: $($service.Name)" -Status "Pass" -Message "Response time: $($serviceData.responseTimeMs)ms"
                } else {
                    Write-TestResult -TestName "Service: $($service.Name)" -Status "Fail" -Message $serviceData.errorMessage
                }
            }
        }
    } else {
        Write-TestResult -TestName "Azure Services Validation" -Status "Fail" -Message $result.Error
    }
}

function Test-Database {
    Write-Header "Database Validation"
    
    $result = Invoke-ApiRequest -Endpoint "api/deployment/validate/database"
    if ($result.Success) {
        $database = $result.Data
        
        if ($database.isProductionReady) {
            Write-TestResult -TestName "Database Production Ready" -Status "Pass" -Message "Database is production ready"
        } else {
            Write-TestResult -TestName "Database Production Ready" -Status "Fail" -Message "Database not ready for production"
        }
        
        # Check schema
        if ($database.schema) {
            if ($database.schema.isUpToDate) {
                Write-TestResult -TestName "Database Schema" -Status "Pass" -Message "Schema up to date"
            } else {
                Write-TestResult -TestName "Database Schema" -Status "Fail" -Message "$($database.schema.pendingMigrations) pending migrations"
            }
        }
        
        # Check seed data
        if ($database.data) {
            if ($database.data.hasRequiredSeedData) {
                Write-TestResult -TestName "Seed Data" -Status "Pass" -Message "Required seed data present"
            } else {
                Write-TestResult -TestName "Seed Data" -Status "Fail" -Message "$($database.data.missingSeedData.Count) missing seed data items"
            }
        }
        
        # Check performance
        if ($database.performance) {
            if (-not $database.performance.performanceIssuesDetected) {
                Write-TestResult -TestName "Database Performance" -Status "Pass" -Message "No performance issues detected"
            } else {
                Write-TestResult -TestName "Database Performance" -Status "Warning" -Message "$($database.performance.slowQueries.Count) slow queries detected"
            }
        }
    } else {
        Write-TestResult -TestName "Database Validation" -Status "Fail" -Message $result.Error
    }
}

function Test-RegressionTests {
    if ($SkipRegressionTests) {
        Write-TestResult -TestName "Regression Tests" -Status "Info" -Message "Skipped (--SkipRegressionTests flag)"
        return
    }
    
    Write-Header "Regression Tests"
    
    $result = Invoke-ApiRequest -Endpoint "api/deployment/validate/regression-tests" -Method "POST"
    if ($result.Success) {
        $tests = $result.Data
        
        if ($tests.allTestsPassed) {
            Write-TestResult -TestName "Regression Tests" -Status "Pass" -Message "$($tests.passedTests)/$($tests.testResults.Count) tests passed"
        } else {
            Write-TestResult -TestName "Regression Tests" -Status "Fail" -Message "$($tests.failedTests)/$($tests.testResults.Count) tests failed"
        }
        
        # Show test results by category
        if ($tests.testsByCategory) {
            foreach ($category in $tests.testsByCategory.PSObject.Properties) {
                $categoryTests = $category.Value
                $passed = ($categoryTests | Where-Object { $_.passed }).Count
                $total = $categoryTests.Count
                
                if ($passed -eq $total) {
                    Write-TestResult -TestName "Tests: $($category.Name)" -Status "Pass" -Message "$passed/$total passed"
                } else {
                    Write-TestResult -TestName "Tests: $($category.Name)" -Status "Fail" -Message "$passed/$total passed"
                }
            }
        }
    } else {
        Write-TestResult -TestName "Regression Tests" -Status "Fail" -Message $result.Error
    }
}

function Test-DeploymentHealth {
    Write-Header "Deployment Health Monitoring"
    
    $result = Invoke-ApiRequest -Endpoint "api/deployment/health"
    if ($result.Success) {
        $health = $result.Data
        
        if ($health.isHealthy) {
            Write-TestResult -TestName "Deployment Health" -Status "Pass" -Message "Health status: $($health.healthStatus)"
        } else {
            Write-TestResult -TestName "Deployment Health" -Status "Fail" -Message "Health status: $($health.healthStatus), Issues: $($health.issues.Count)"
        }
        
        # Check service health
        if ($health.services) {
            $healthyServices = ($health.services.PSObject.Properties.Value | Where-Object { $_.isHealthy }).Count
            $totalServices = $health.services.PSObject.Properties.Count
            
            if ($healthyServices -eq $totalServices) {
                Write-TestResult -TestName "Service Health" -Status "Pass" -Message "$healthyServices/$totalServices services healthy"
            } else {
                Write-TestResult -TestName "Service Health" -Status "Warning" -Message "$healthyServices/$totalServices services healthy"
            }
        }
    } else {
        Write-TestResult -TestName "Deployment Health" -Status "Fail" -Message $result.Error
    }
}

function Show-Summary {
    Write-Header "Validation Summary"
    
    $duration = (Get-Date) - $script:StartTime
    
    Write-Host ""
    Write-Host "📊 Test Results:" -ForegroundColor $Colors.Header
    Write-Host "   Total Tests: $script:TotalTests" -ForegroundColor Gray
    Write-Host "   Passed: $script:PassedTests" -ForegroundColor $Colors.Success
    Write-Host "   Failed: $script:FailedTests" -ForegroundColor $Colors.Error
    Write-Host "   Warnings: $script:WarningTests" -ForegroundColor $Colors.Warning
    Write-Host "   Duration: $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Gray
    Write-Host ""
    
    # Overall status
    if ($script:FailedTests -eq 0) {
        if ($script:WarningTests -eq 0) {
            Write-Host "✅ Deployment validation PASSED - Ready for deployment!" -ForegroundColor $Colors.Success
            $exitCode = 0
        } else {
            Write-Host "⚠️  Deployment validation PASSED with warnings" -ForegroundColor $Colors.Warning
            $exitCode = if ($FailOnWarnings) { 1 } else { 0 }
        }
    } else {
        Write-Host "❌ Deployment validation FAILED - $script:FailedTests critical issues found" -ForegroundColor $Colors.Error
        $exitCode = 1
    }
    
    Write-Host ""
    return $exitCode
}

function Export-Results {
    param([int]$ExitCode)
    
    if ($OutputFile -and ($OutputFormat -eq "Json" -or $OutputFormat -eq "JUnit")) {
        $summary = @{
            Environment = $Environment
            BaseUrl = $BaseUrl
            StartTime = $script:StartTime
            EndTime = Get-Date
            Duration = ((Get-Date) - $script:StartTime).TotalSeconds
            TotalTests = $script:TotalTests
            PassedTests = $script:PassedTests
            FailedTests = $script:FailedTests
            WarningTests = $script:WarningTests
            ExitCode = $ExitCode
            Results = $script:ValidationResults
        }
        
        if ($OutputFormat -eq "Json") {
            $summary | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputFile -Encoding UTF8
            Write-Host "Results exported to: $OutputFile" -ForegroundColor $Colors.Info
        }
        elseif ($OutputFormat -eq "JUnit") {
            # Convert to JUnit XML format
            $xml = @"
<?xml version="1.0" encoding="UTF-8"?>
<testsuites tests="$($script:TotalTests)" failures="$($script:FailedTests)" time="$($summary.Duration)">
    <testsuite name="DeploymentValidation" tests="$($script:TotalTests)" failures="$($script:FailedTests)" time="$($summary.Duration)">
"@
            
            foreach ($result in $script:ValidationResults) {
                $status = if ($result.Status -eq "Fail") { "failure" } else { "success" }
                $xml += @"
        <testcase name="$($result.TestName)" time="0">
"@
                if ($result.Status -eq "Fail") {
                    $xml += @"
            <failure message="$($result.Message)"></failure>
"@
                }
                $xml += @"
        </testcase>
"@
            }
            
            $xml += @"
    </testsuite>
</testsuites>
"@
            
            $xml | Out-File -FilePath $OutputFile -Encoding UTF8
            Write-Host "JUnit results exported to: $OutputFile" -ForegroundColor $Colors.Info
        }
    }
}

# Main execution
try {
    Write-Header "AI Profile Photo Maker - Deployment Validation"
    Write-Host "Environment: $Environment" -ForegroundColor $Colors.Info
    Write-Host "Target URL: $BaseUrl" -ForegroundColor $Colors.Info
    Write-Host "Timeout: $Timeout seconds" -ForegroundColor $Colors.Info
    Write-Host ""
    
    # Run validation tests
    $canContinue = Test-BasicConnectivity
    
    if ($canContinue) {
        Test-DeploymentReadiness
        Test-Configuration
        Test-Security
        Test-Performance
        Test-AzureServices
        Test-Database
        Test-RegressionTests
        Test-DeploymentHealth
    } else {
        Write-TestResult -TestName "Validation Aborted" -Status "Fail" -Message "Cannot continue - basic connectivity failed"
    }
    
    # Show summary and export results
    $exitCode = Show-Summary
    Export-Results -ExitCode $exitCode
    
    exit $exitCode
}
catch {
    Write-Host "❌ Validation script failed: $($_.Exception.Message)" -ForegroundColor $Colors.Error
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
    
    if ($OutputFile -and $OutputFormat -eq "Json") {
        @{
            Error = $_.Exception.Message
            StackTrace = $_.ScriptStackTrace
            Timestamp = Get-Date
        } | ConvertTo-Json | Out-File -FilePath $OutputFile -Encoding UTF8
    }
    
    exit 1
}