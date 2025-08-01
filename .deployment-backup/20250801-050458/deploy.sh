#!/bin/bash

# Azure Infrastructure Deployment Script for AI Profile Photo Maker
# This script deploys the Bicep template to Azure

set -e

# Configuration
RESOURCE_GROUP_NAME="ai-profile-photo-maker"
LOCATION="East US"
TEMPLATE_FILE="main.bicep"
DEPLOYMENT_NAME="ai-profile-deployment-$(date +%Y%m%d-%H%M%S)"

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

Deploy Azure infrastructure for AI Profile Photo Maker

OPTIONS:
    -e, --environment ENV    Environment (prod, staging, dev) [default: prod]
    -g, --resource-group RG  Resource group name [default: ai-profile-photo-maker]
    -l, --location LOC       Azure location [default: East US]
    -v, --validate           Validate template only (no deployment)
    -h, --help               Show this help message

EXAMPLES:
    $0                       Deploy to production
    $0 -e staging           Deploy to staging environment
    $0 -e dev -g my-rg      Deploy to dev environment with custom resource group
    $0 --validate           Validate template without deploying

PREREQUISITES:
    - Azure CLI installed and logged in
    - Bicep CLI installed
    - Appropriate Azure permissions for resource creation

EOF
}

# Parse command line arguments
ENVIRONMENT="prod"
VALIDATE_ONLY=false

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

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(prod|staging|dev)$ ]]; then
    print_error "Environment must be one of: prod, staging, dev"
    exit 1
fi

# Set parameter file based on environment
PARAMETER_FILE="parameters.${ENVIRONMENT}.json"

# Check if parameter file exists
if [[ ! -f "$PARAMETER_FILE" ]]; then
    print_error "Parameter file not found: $PARAMETER_FILE"
    exit 1
fi

# Function to check prerequisites
check_prerequisites() {
    print_status "Checking prerequisites..."
    
    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed. Please install it first."
        exit 1
    fi
    
    # Check Bicep CLI
    if ! command -v bicep &> /dev/null; then
        print_error "Bicep CLI is not installed. Please install it first."
        exit 1
    fi
    
    # Check Azure login
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi
    
    # Check if template file exists
    if [[ ! -f "$TEMPLATE_FILE" ]]; then
        print_error "Template file not found: $TEMPLATE_FILE"
        exit 1
    fi
    
    print_success "Prerequisites check passed"
}

# Function to create resource group
create_resource_group() {
    print_status "Creating resource group: $RESOURCE_GROUP_NAME"
    
    if az group show --name "$RESOURCE_GROUP_NAME" &> /dev/null; then
        print_warning "Resource group already exists: $RESOURCE_GROUP_NAME"
    else
        az group create \
            --name "$RESOURCE_GROUP_NAME" \
            --location "$LOCATION" \
            --output table
        print_success "Resource group created: $RESOURCE_GROUP_NAME"
    fi
}

# Function to validate template
validate_template() {
    print_status "Validating Bicep template..."
    
    az deployment group validate \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --template-file "$TEMPLATE_FILE" \
        --parameters "@$PARAMETER_FILE" \
        --output table
    
    if [[ $? -eq 0 ]]; then
        print_success "Template validation passed"
    else
        print_error "Template validation failed"
        exit 1
    fi
}

# Function to deploy template
deploy_template() {
    print_status "Deploying infrastructure to Azure..."
    print_status "Environment: $ENVIRONMENT"
    print_status "Resource Group: $RESOURCE_GROUP_NAME"
    print_status "Location: $LOCATION"
    print_status "Deployment Name: $DEPLOYMENT_NAME"
    
    az deployment group create \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --template-file "$TEMPLATE_FILE" \
        --parameters "@$PARAMETER_FILE" \
        --name "$DEPLOYMENT_NAME" \
        --output table
    
    if [[ $? -eq 0 ]]; then
        print_success "Deployment completed successfully"
    else
        print_error "Deployment failed"
        exit 1
    fi
}

# Function to show deployment outputs
show_outputs() {
    print_status "Retrieving deployment outputs..."
    
    az deployment group show \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --name "$DEPLOYMENT_NAME" \
        --query properties.outputs \
        --output table
}

# Function to cleanup on failure
cleanup() {
    if [[ $? -ne 0 ]]; then
        print_error "Deployment failed. Check the error messages above."
        print_status "You can check the deployment status in the Azure portal:"
        print_status "https://portal.azure.com/#@/resource/subscriptions/$(az account show --query id -o tsv)/resourcegroups/$RESOURCE_GROUP_NAME/deployments"
    fi
}

# Set trap for cleanup
trap cleanup EXIT

# Main execution
main() {
    print_status "Starting Azure infrastructure deployment..."
    print_status "Environment: $ENVIRONMENT"
    print_status "Resource Group: $RESOURCE_GROUP_NAME"
    print_status "Location: $LOCATION"
    
    # Check prerequisites
    check_prerequisites
    
    # Create resource group
    create_resource_group
    
    # Validate template
    validate_template
    
    # Deploy or validate only
    if [[ "$VALIDATE_ONLY" == "true" ]]; then
        print_success "Template validation completed successfully"
        exit 0
    else
        deploy_template
        show_outputs
    fi
    
    print_success "Deployment process completed successfully!"
    print_status "Your AI Profile Photo Maker infrastructure is now ready in Azure."
}

# Run main function
main "$@"