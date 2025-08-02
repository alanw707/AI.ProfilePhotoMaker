#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test script to verify CreditPackages API endpoint is working
    
.DESCRIPTION
    Tests the /api/credit-packages endpoint to ensure it returns HTTP 200
    instead of HTTP 500 after the emergency migration.
    
.PARAMETER BaseUrl
    The base URL of the API (default: staging endpoint)
    
.PARAMETER ShowDetails
    If specified, shows full response details
#>

param(
    [string]$BaseUrl = "https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io",
    [switch]$ShowDetails
)

$ErrorActionPreference = "Stop"

Write-Host "🧪 TESTING CREDITPACKAGES ENDPOINT" -ForegroundColor Blue
Write-Host "===================================" -ForegroundColor Blue
Write-Host ""

$endpoint = "$BaseUrl/api/credit-packages"

Write-Host "🎯 Testing endpoint: $endpoint" -ForegroundColor Cyan
Write-Host ""

try {
    # Test the endpoint
    Write-Host "🔄 Making HTTP GET request..." -ForegroundColor Cyan
    
    $response = Invoke-WebRequest -Uri $endpoint -Method Get -TimeoutSec 30
    
    Write-Host "✅ SUCCESS!" -ForegroundColor Green
    Write-Host "  Status Code: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "  Status Description: $($response.StatusDescription)" -ForegroundColor Green
    Write-Host "  Content Length: $($response.Content.Length) bytes" -ForegroundColor Green
    
    if ($ShowDetails) {
        Write-Host ""
        Write-Host "📋 Response Headers:" -ForegroundColor Cyan
        $response.Headers | Format-Table -AutoSize
        
        Write-Host "📄 Response Content:" -ForegroundColor Cyan
        $jsonContent = $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 3
        Write-Host $jsonContent -ForegroundColor White
    } else {
        # Parse JSON to show package count
        try {
            $packages = $response.Content | ConvertFrom-Json
            if ($packages -is [array]) {
                Write-Host "  Credit Packages Found: $($packages.Count)" -ForegroundColor Green
                foreach ($package in $packages) {
                    Write-Host "    - $($package.name): $($package.credits) credits for `$$($package.price)" -ForegroundColor White
                }
            } else {
                Write-Host "  Response is not an array of packages" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "  Could not parse JSON response" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "🎉 ENDPOINT IS WORKING - CreditPackages table exists and is accessible!" -ForegroundColor Green
    
} catch [System.Net.WebException] {
    $statusCode = [int]$_.Exception.Response.StatusCode
    $statusDescription = $_.Exception.Response.StatusDescription
    
    Write-Host "❌ HTTP ERROR" -ForegroundColor Red
    Write-Host "  Status Code: $statusCode" -ForegroundColor Red
    Write-Host "  Status Description: $statusDescription" -ForegroundColor Red
    
    if ($statusCode -eq 500) {
        Write-Host ""
        Write-Host "🚨 HTTP 500 - Internal Server Error detected!" -ForegroundColor Red
        Write-Host "This indicates the CreditPackages table is still missing or inaccessible." -ForegroundColor Red
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Yellow
        Write-Host "1. Run the emergency migration script" -ForegroundColor White
        Write-Host "2. Restart the Container App" -ForegroundColor White
        Write-Host "3. Check application logs for database connection errors" -ForegroundColor White
    } elseif ($statusCode -eq 404) {
        Write-Host ""
        Write-Host "ℹ️  HTTP 404 - Endpoint not found" -ForegroundColor Yellow
        Write-Host "Check if the API is deployed and the endpoint path is correct." -ForegroundColor Yellow
    }
    
    # Try to get response content for more details
    try {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseContent = $reader.ReadToEnd()
        if ($responseContent) {
            Write-Host ""
            Write-Host "Error Response Content:" -ForegroundColor Yellow
            Write-Host $responseContent -ForegroundColor White
        }
    } catch {
        # Ignore if we can't read the response
    }
    
} catch {
    Write-Host "❌ UNEXPECTED ERROR" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Possible causes:" -ForegroundColor Yellow
    Write-Host "1. Network connectivity issues" -ForegroundColor White
    Write-Host "2. API is not running or not deployed" -ForegroundColor White  
    Write-Host "3. DNS resolution problems" -ForegroundColor White
    Write-Host "4. Firewall blocking the request" -ForegroundColor White
}

Write-Host ""
Write-Host "🔍 Additional debugging:" -ForegroundColor Cyan
Write-Host "  Health check endpoint: $BaseUrl/health" -ForegroundColor White
Write-Host "  Application logs: Check Container App logs in Azure Portal" -ForegroundColor White