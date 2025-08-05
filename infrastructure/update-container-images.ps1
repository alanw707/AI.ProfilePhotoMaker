param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$ContainerRegistryName,
    
    [Parameter(Mandatory=$true)]
    [string]$BackendAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$FrontendAppName
)

Write-Host "[UPDATE] Updating Container Apps with actual container images..." -ForegroundColor Green

try {
    # Get container registry login server
    $registry = Get-AzContainerRegistry -ResourceGroupName $ResourceGroupName -Name $ContainerRegistryName
    $registryServer = $registry.LoginServer
    
    Write-Host "[INFO] Registry server: $registryServer" -ForegroundColor Cyan
    
    # Update backend container app
    Write-Host "[UPDATE] Updating backend app: $BackendAppName" -ForegroundColor Yellow
    
    $backendApp = Get-AzContainerApp -ResourceGroupName $ResourceGroupName -Name $BackendAppName
    $backendConfig = $backendApp.Properties.Configuration
    $backendTemplate = $backendApp.Properties.Template
    
    # Update the container image
    $backendTemplate.Containers[0].Image = "$registryServer/aiprofilemaker-backend:latest"
    
    # Update the container app with new configuration
    Update-AzContainerApp -ResourceGroupName $ResourceGroupName -Name $BackendAppName -Configuration $backendConfig -TemplateContainer $backendTemplate.Containers -TemplateRevisionSuffix ((Get-Date).ToString("yyyyMMdd-HHmmss"))
    
    Write-Host "[SUCCESS] Backend app updated successfully" -ForegroundColor Green
    
    # Update frontend container app
    Write-Host "[UPDATE] Updating frontend app: $FrontendAppName" -ForegroundColor Yellow
    
    $frontendApp = Get-AzContainerApp -ResourceGroupName $ResourceGroupName -Name $FrontendAppName
    $frontendConfig = $frontendApp.Properties.Configuration
    $frontendTemplate = $frontendApp.Properties.Template
    
    # Update the container image
    $frontendTemplate.Containers[0].Image = "$registryServer/aiprofilemaker-frontend:latest"
    
    # Update the container app with new configuration
    Update-AzContainerApp -ResourceGroupName $ResourceGroupName -Name $FrontendAppName -Configuration $frontendConfig -TemplateContainer $frontendTemplate.Containers -TemplateRevisionSuffix ((Get-Date).ToString("yyyyMMdd-HHmmss"))
    
    Write-Host "[SUCCESS] Frontend app updated successfully" -ForegroundColor Green
    Write-Host "[SUCCESS] All container apps updated with actual images!" -ForegroundColor Green
    
} catch {
    Write-Host "[ERROR] Failed to update container images" -ForegroundColor Red
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
    throw
}