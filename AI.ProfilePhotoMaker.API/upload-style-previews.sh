#!/bin/bash

# Upload Style Previews to Azure Blob Storage
# This script provides an easy way to upload local style preview images to Azure

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo -e "${BLUE}🎨 Style Preview Upload Script${NC}"
echo -e "${BLUE}================================${NC}"

# Change to API directory
cd "$SCRIPT_DIR"

# Function to show help
show_help() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --dry-run    Simulate upload without actually uploading files"
    echo "  --force      Force overwrite existing files in Azure"
    echo "  --list       List current status of style preview files"
    echo "  --help       Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                    # Upload all style previews (skip existing)"
    echo "  $0 --dry-run          # Test run without uploading"
    echo "  $0 --force            # Upload and overwrite existing files"
    echo "  $0 --list             # Check current file status"
    echo ""
    echo "Tip: After generating new previews via ../scripts/generate-style-previews.sh,"
    echo "     rerun this script with --force to refresh the blobs."
}

# Function to run dotnet command with error handling
run_upload_command() {
    local args="$1"
    echo -e "${YELLOW}Running: dotnet run -- upload-previews $args${NC}"
    echo ""
    
    if dotnet run -- upload-previews $args; then
        echo ""
        echo -e "${GREEN}✅ Command completed successfully!${NC}"
        return 0
    else
        echo ""
        echo -e "${RED}❌ Command failed. Check the output above for details.${NC}"
        return 1
    fi
}

# Function to run list command
run_list_command() {
    echo -e "${YELLOW}Running: dotnet run -- list-previews${NC}"
    echo ""
    
    if dotnet run -- list-previews; then
        echo ""
        echo -e "${GREEN}✅ Status check completed!${NC}"
        return 0
    else
        echo ""
        echo -e "${RED}❌ Status check failed.${NC}"
        return 1
    fi
}

# Check if .NET is available
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ Error: .NET is not installed or not in PATH${NC}"
    echo "Please install .NET SDK to use this script."
    exit 1
fi

# Check if we're in the right directory
if [[ ! -f "AI.ProfilePhotoMaker.API.csproj" ]]; then
    echo -e "${RED}❌ Error: Not in the API project directory${NC}"
    echo "Please run this script from the AI.ProfilePhotoMaker.API directory."
    exit 1
fi

# Parse command line arguments
case "${1:-}" in
    --help|-h)
        show_help
        exit 0
        ;;
    --dry-run)
        echo -e "${BLUE}🧪 Running in dry-run mode (no actual uploads)${NC}"
        echo ""
        run_upload_command "--dry-run"
        ;;
    --force)
        echo -e "${YELLOW}⚠️  Running in force mode (will overwrite existing files)${NC}"
        echo ""
        read -p "Are you sure you want to overwrite existing files? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            run_upload_command "--force"
        else
            echo -e "${BLUE}Operation cancelled.${NC}"
            exit 0
        fi
        ;;
    --list)
        echo -e "${BLUE}📋 Checking style preview file status${NC}"
        echo ""
        run_list_command
        ;;
    "")
        echo -e "${GREEN}📤 Uploading style previews (skipping existing files)${NC}"
        echo ""
        run_upload_command ""
        ;;
    *)
        echo -e "${RED}❌ Unknown option: $1${NC}"
        echo ""
        show_help
        exit 1
        ;;
esac

echo ""
echo -e "${BLUE}🎉 Script completed!${NC}"
