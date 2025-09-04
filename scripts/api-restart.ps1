param(
  [string] $Environment = "Development"
)

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $PSScriptRoot 'api-stop.ps1')
Write-Host '---'
& (Join-Path $PSScriptRoot 'api-start.ps1') -Environment $Environment

