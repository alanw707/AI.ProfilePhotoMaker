param(
  [string] $Environment = "Development"
)

$ErrorActionPreference = 'Stop'

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiDir = Join-Path $PSScriptRoot '..\AI.ProfilePhotoMaker.API'

Write-Host "Starting API from: $apiDir (ASPNETCORE_ENVIRONMENT=$Environment)"
$env:ASPNETCORE_ENVIRONMENT = $Environment

dotnet run --project (Join-Path $apiDir 'AI.ProfilePhotoMaker.API.csproj') --launch-profile https
