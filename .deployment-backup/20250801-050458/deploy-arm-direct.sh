#!/bin/bash

# Direct ARM Template Deployment Script (No Azure CLI API dependency)
# Uses Azure PowerShell Core for reliable cross-platform deployment

set -e

# Configuration
RESOURCE_GROUP_NAME="ai-profile-photo-maker"
LOCATION="East US"
DEPLOYMENT_NAME="ai-profile-deployment-$(date +%Y%m%d-%H%M%S)"
TEMPLATE_FILE="main.json"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
show_usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Deploy Azure infrastructure using ARM templates directly (bypasses Azure CLI API issues)

OPTIONS:
    -e, --environment ENV    Environment (prod, staging, dev) [default: staging]
    -g, --resource-group RG  Resource group name [default: ai-profile-photo-maker]
    -l, --location LOC       Azure location [default: East US]
    -v, --validate           Validate template only (no deployment)
    -m, --method METHOD      Deployment method (pwsh, rest, portal) [default: pwsh]
    -h, --help               Show this help message

DEPLOYMENT METHODS:
    pwsh    - Azure PowerShell Core (recommended for local)
    rest    - Direct REST API calls
    portal  - Generate Azure Portal deployment URL

EXAMPLES:
    $0                       Deploy to staging using PowerShell
    $0 -e prod -m rest      Deploy to production using REST API
    $0 --validate           Validate template without deploying
    $0 -m portal            Generate Portal deployment URL

PREREQUISITES:
    pwsh method: PowerShell Core + Az PowerShell module
    rest method: curl + valid Azure access token
    portal method: Just a web browser

EOF
}

# Parse command line arguments
ENVIRONMENT="staging"
VALIDATE_ONLY=false
METHOD="pwsh"

while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -g|--resource-group)
            RESOURCE_GROUP_NAME="$2"
            shift 2
            ;;
        -l|--location)
            LOCATION="$2"
            shift 2
            ;;
        -v|--validate)
            VALIDATE_ONLY=true
            shift
            ;;
        -m|--method)
            METHOD="$2"
            shift 2
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Update resource group name based on environment
if [[ "$RESOURCE_GROUP_NAME" == "ai-profile-photo-maker" ]]; then
    RESOURCE_GROUP_NAME="ai-profile-photo-maker-$ENVIRONMENT"
fi

# Set parameter file based on environment
PARAMETER_FILE="parameters.${ENVIRONMENT}.json"

# Check if parameter file exists
if [[ ! -f "$PARAMETER_FILE" ]]; then
    print_error "Parameter file not found: $PARAMETER_FILE"
    exit 1
fi

# Function to compile Bicep to ARM template
compile_bicep() {
    print_status "Compiling Bicep template to ARM JSON..."
    
    if command -v bicep &> /dev/null; then
        bicep build main.bicep --outfile main.json
        if [[ $? -eq 0 ]]; then
            print_success "Bicep compilation successful"
        else
            print_error "Bicep compilation failed"
            exit 1
        fi
    else
        print_error "Bicep CLI not found. Please install it first."
        print_status "Install: curl -Lo bicep https://github.com/Azure/bicep/releases/latest/download/bicep-linux-x64"
        exit 1
    fi
}

# Function to deploy using Azure PowerShell
deploy_with_powershell() {
    print_status "Deploying using Azure PowerShell Core..."
    
    # Check if PowerShell is available
    if ! command -v pwsh &> /dev/null; then
        print_error "PowerShell Core not found. Please install it first."
        print_status "Install: wget -q https://github.com/PowerShell/PowerShell/releases/download/v7.4.4/powershell_7.4.4-1.deb_amd64.deb && sudo dpkg -i powershell_7.4.4-1.deb_amd64.deb"
        exit 1
    fi
    
    # Create PowerShell deployment script
    cat > deploy_arm.ps1 << 'EOF'
param(
    [string]$ResourceGroupName,
    [string]$Location,
    [string]$TemplateFile,
    [string]$ParameterFile,
    [string]$DeploymentName,
    [bool]$ValidateOnly = $false
)

# Import Az module
Import-Module Az -Force -ErrorAction Stop

# Check if logged in
$context = Get-AzContext
if (-not $context) {
    Write-Error "Not logged in to Azure. Please run 'Connect-AzAccount' first."
    exit 1
}

Write-Host "Using subscription: $($context.Subscription.Name)" -ForegroundColor Green

# Ensure resource group exists
$rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
if (-not $rg) {
    Write-Host "Creating resource group: $ResourceGroupName" -ForegroundColor Yellow
    New-AzResourceGroup -Name $ResourceGroupName -Location $Location
}
else {
    Write-Host "Resource group exists: $ResourceGroupName" -ForegroundColor Green
}

# Validate or deploy
if ($ValidateOnly) {
    Write-Host "Validating ARM template..." -ForegroundColor Yellow
    $result = Test-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile $TemplateFile -TemplateParameterFile $ParameterFile
    if ($result) {
        Write-Error "Template validation failed:"
        $result | ForEach-Object { Write-Error $_.Message }
        exit 1
    }
    else {
        Write-Host "Template validation passed!" -ForegroundColor Green
    }
}
else {
    Write-Host "Deploying ARM template..." -ForegroundColor Yellow
    $deployment = New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile $TemplateFile -TemplateParameterFile $ParameterFile -Name $DeploymentName -Verbose
    
    if ($deployment.ProvisioningState -eq "Succeeded") {
        Write-Host "Deployment completed successfully!" -ForegroundColor Green
        Write-Host "Deployment Name: $DeploymentName" -ForegroundColor Cyan
        
        # Show outputs
        if ($deployment.Outputs) {
            Write-Host "Deployment Outputs:" -ForegroundColor Cyan
            $deployment.Outputs | ConvertTo-Json -Depth 10
        }
    }
    else {
        Write-Error "Deployment failed with state: $($deployment.ProvisioningState)"
        exit 1
    }
}
EOF

    # Execute PowerShell script
    pwsh -File deploy_arm.ps1 -ResourceGroupName "$RESOURCE_GROUP_NAME" -Location "$LOCATION" -TemplateFile "$TEMPLATE_FILE" -ParameterFile "$PARAMETER_FILE" -DeploymentName "$DEPLOYMENT_NAME" -ValidateOnly:$VALIDATE_ONLY
    
    # Cleanup
    rm -f deploy_arm.ps1
}

# Function to deploy using REST API
deploy_with_rest_api() {
    print_status "Deploying using Azure REST API..."
    
    # Check if logged in with Azure CLI for token
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Please run 'az login' first to get access token."
        exit 1
    fi
    
    # Get access token
    ACCESS_TOKEN=$(az account get-access-token --query accessToken -o tsv)
    SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    
    if [[ -z "$ACCESS_TOKEN" ]]; then
        print_error "Failed to get access token"
        exit 1
    fi
    
    print_status "Using subscription: $SUBSCRIPTION_ID"
    
    # Ensure resource group exists
    print_status "Checking resource group: $RESOURCE_GROUP_NAME"
    
    RG_CHECK=$(curl -s -w "%{http_code}" -o /tmp/rg_check.json \
        -H "Authorization: Bearer $ACCESS_TOKEN" \
        -H "Content-Type: application/json" \
        "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP_NAME?api-version=2021-04-01")
    
    if [[ "$RG_CHECK" == "404" ]]; then
        print_status "Creating resource group: $RESOURCE_GROUP_NAME"
        RG_CREATE=$(curl -s -w "%{http_code}" -o /tmp/rg_create.json \
            -X PUT \
            -H "Authorization: Bearer $ACCESS_TOKEN" \
            -H "Content-Type: application/json" \
            -d "{\"location\":\"$LOCATION\"}" \
            "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP_NAME?api-version=2021-04-01")
        
        if [[ "$RG_CREATE" == "200" ]] || [[ "$RG_CREATE" == "201" ]]; then
            print_success "Resource group created successfully"
        else
            print_error "Failed to create resource group. HTTP status: $RG_CREATE"
            cat /tmp/rg_create.json
            exit 1
        fi
    elif [[ "$RG_CHECK" == "200" ]]; then
        print_success "Resource group already exists"
    else
        print_error "Failed to check resource group. HTTP status: $RG_CHECK"
        exit 1
    fi
    
    # Prepare deployment payload
    TEMPLATE_CONTENT=$(cat "$TEMPLATE_FILE")
    PARAMETERS_CONTENT=$(cat "$PARAMETER_FILE" | jq '.parameters')
    
    cat > deployment_payload.json << EOF
{
    "properties": {
        "template": $TEMPLATE_CONTENT,
        "parameters": $PARAMETERS_CONTENT,
        "mode": "Incremental"
    }
}
EOF
    
    if [[ "$VALIDATE_ONLY" == "true" ]]; then
        print_status "Validating deployment via REST API..."
        VALIDATION_RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/validation.json \
            -X POST \
            -H "Authorization: Bearer $ACCESS_TOKEN" \
            -H "Content-Type: application/json" \
            -d @deployment_payload.json \
            "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP_NAME/providers/Microsoft.Resources/deployments/$DEPLOYMENT_NAME/validate?api-version=2021-04-01")
        
        if [[ "$VALIDATION_RESPONSE" == "200" ]]; then
            print_success "Template validation passed!"
        else
            print_error "Template validation failed. HTTP status: $VALIDATION_RESPONSE"
            cat /tmp/validation.json | jq .
            exit 1
        fi
    else
        print_status "Starting deployment via REST API..."
        DEPLOY_RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/deployment.json \
            -X PUT \
            -H "Authorization: Bearer $ACCESS_TOKEN" \
            -H "Content-Type: application/json" \
            -d @deployment_payload.json \
            "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP_NAME/providers/Microsoft.Resources/deployments/$DEPLOYMENT_NAME?api-version=2021-04-01")
        
        if [[ "$DEPLOY_RESPONSE" == "200" ]] || [[ "$DEPLOY_RESPONSE" == "201" ]]; then
            print_success "Deployment started successfully"
            print_status "Deployment Name: $DEPLOYMENT_NAME"
            
            # Monitor deployment progress
            print_status "Monitoring deployment progress..."
            while true; do
                sleep 30
                STATUS_RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/status.json \
                    -H "Authorization: Bearer $ACCESS_TOKEN" \
                    "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP_NAME/providers/Microsoft.Resources/deployments/$DEPLOYMENT_NAME?api-version=2021-04-01")
                
                if [[ "$STATUS_RESPONSE" == "200" ]]; then
                    PROVISIONING_STATE=$(cat /tmp/status.json | jq -r '.properties.provisioningState')
                    print_status "Deployment status: $PROVISIONING_STATE"
                    
                    if [[ "$PROVISIONING_STATE" == "Succeeded" ]]; then
                        print_success "Deployment completed successfully!"
                        cat /tmp/status.json | jq '.properties.outputs' 2>/dev/null || echo "No outputs available"
                        break
                    elif [[ "$PROVISIONING_STATE" == "Failed" ]]; then
                        print_error "Deployment failed!"
                        cat /tmp/status.json | jq '.properties.error' 2>/dev/null || cat /tmp/status.json
                        exit 1
                    fi
                else
                    print_warning "Failed to get deployment status. HTTP status: $STATUS_RESPONSE"
                fi
            done
        else
            print_error "Failed to start deployment. HTTP status: $DEPLOY_RESPONSE"
            cat /tmp/deployment.json | jq . 2>/dev/null || cat /tmp/deployment.json
            exit 1
        fi
    fi
    
    # Cleanup
    rm -f deployment_payload.json /tmp/*.json
}

# Function to generate Azure Portal deployment URL
generate_portal_deployment() {
    print_status "Generating Azure Portal deployment URL..."
    
    # Base64 encode the template and parameters
    TEMPLATE_B64=$(base64 -w 0 "$TEMPLATE_FILE")
    PARAMETERS_B64=$(base64 -w 0 "$PARAMETER_FILE")
    
    # Get subscription ID if available
    SUBSCRIPTION_ID=""
    if command -v az &> /dev/null && az account show &> /dev/null; then
        SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    fi
    
    # Generate URL
    PORTAL_URL="https://portal.azure.com/#create/Microsoft.Template"
    
    if [[ -n "$SUBSCRIPTION_ID" ]]; then
        PORTAL_URL="${PORTAL_URL}/subscription/${SUBSCRIPTION_ID}"
    fi
    
    PORTAL_URL="${PORTAL_URL}/resourceGroup/${RESOURCE_GROUP_NAME}"
    
    print_success "Azure Portal Deployment URL generated!"
    echo ""
    echo "🌐 Open this URL in your browser:"
    echo "   $PORTAL_URL"
    echo ""
    echo "📋 Manual steps in Azure Portal:"
    echo "   1. Click the URL above to open Azure Portal"
    echo "   2. Select subscription: $(az account show --query name -o tsv 2>/dev/null || echo "Your subscription")"
    echo "   3. Resource group: $RESOURCE_GROUP_NAME"
    echo "   4. Upload template file: $TEMPLATE_FILE"
    echo "   5. Upload parameters file: $PARAMETER_FILE"
    echo "   6. Review and create"
    echo ""
    echo "📂 Files ready for upload:"
    echo "   Template: $(pwd)/$TEMPLATE_FILE"
    echo "   Parameters: $(pwd)/$PARAMETER_FILE"
}

# Function to cleanup on failure
cleanup() {
    if [[ $? -ne 0 ]]; then
        print_error "Deployment failed. Check the error messages above."
        print_status "Alternative deployment methods:"
        print_status "1. Try: $0 -m rest (use REST API)"
        print_status "2. Try: $0 -m portal (use Azure Portal)"
        print_status "3. Check Azure Portal for deployment status"
    fi
}

# Set trap for cleanup
trap cleanup EXIT

# Main execution
main() {
    print_status "Starting Azure infrastructure deployment (Azure CLI bypass)..."
    print_status "Method: $METHOD"
    print_status "Environment: $ENVIRONMENT"
    print_status "Resource Group: $RESOURCE_GROUP_NAME"
    print_status "Location: $LOCATION"
    
    # Compile Bicep template
    compile_bicep
    
    # Check if ARM template exists
    if [[ ! -f "$TEMPLATE_FILE" ]]; then
        print_error "ARM template file not found: $TEMPLATE_FILE"
        exit 1
    fi
    
    # Execute based on method
    case $METHOD in
        pwsh)
            deploy_with_powershell
            ;;
        rest)
            deploy_with_rest_api
            ;;
        portal)
            generate_portal_deployment
            ;;
        *)
            print_error "Unknown deployment method: $METHOD"
            print_status "Available methods: pwsh, rest, portal"
            exit 1
            ;;
    esac
    
    if [[ "$METHOD" != "portal" ]]; then
        print_success "Deployment process completed successfully!"
        print_status "Your AI Profile Photo Maker infrastructure is ready in Azure."
    fi
}

# Run main function
main "$@"