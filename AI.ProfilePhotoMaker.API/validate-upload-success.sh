#!/bin/bash

# Style Preview Upload Success Validation Script
# Run this script after uploading to validate all files are accessible

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}🔍 Style Preview Upload Validation${NC}"
echo -e "${BLUE}=================================${NC}"

# Get the storage account URL from environment or prompt
if [ -z "$AZURE_STORAGE_BASE_URL" ]; then
    echo -e "${YELLOW}Enter the Azure Storage base URL (e.g., https://yourstore.blob.core.windows.net):${NC}"
    read -r AZURE_STORAGE_BASE_URL
fi

if [ -z "$AZURE_STORAGE_BASE_URL" ]; then
    echo -e "${RED}❌ Azure Storage base URL is required${NC}"
    exit 1
fi

echo -e "${BLUE}Testing against: $AZURE_STORAGE_BASE_URL${NC}"
echo ""

# Expected files to validate
EXPECTED_FILES=(
    "academic.jpg"
    "artistic.jpg" 
    "author.jpg"
    "casual.jpg"
    "consultant.jpg"
    "corporate.jpg"
    "creative.jpg"
    "digital-nomad.jpg"
    "edgy-urban.jpg"
    "entrepreneur.jpg"
    "executive.jpg"
    "fashion.jpg"
    "fitness.jpg"
    "glamour.jpg"
    "influencer.jpg"
    "legal.jpg"
    "linkedin.jpg"
    "medical.jpg"
    "spiritual.jpg"
    "startup.jpg"
    "tech-professional.jpg"
)

# Counters
SUCCESS_COUNT=0
FAILED_COUNT=0
TOTAL_COUNT=${#EXPECTED_FILES[@]}

echo -e "${YELLOW}Validating $TOTAL_COUNT files...${NC}"
echo ""

# Test each file
for file in "${EXPECTED_FILES[@]}"; do
    URL="$AZURE_STORAGE_BASE_URL/style-previews/$file"
    
    echo -n "Testing $file... "
    
    # Use curl to test accessibility
    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$URL")
    
    if [ "$HTTP_STATUS" = "200" ]; then
        echo -e "${GREEN}✅ OK ($HTTP_STATUS)${NC}"
        ((SUCCESS_COUNT++))
    else
        echo -e "${RED}❌ FAILED ($HTTP_STATUS)${NC}"
        ((FAILED_COUNT++))
        
        # Log the failed URL for debugging
        echo "   Failed URL: $URL" >&2
    fi
done

echo ""
echo -e "${BLUE}=== Validation Summary ===${NC}"
echo -e "Total files: $TOTAL_COUNT"
echo -e "${GREEN}Successful: $SUCCESS_COUNT${NC}"
if [ $FAILED_COUNT -gt 0 ]; then
    echo -e "${RED}Failed: $FAILED_COUNT${NC}"
else
    echo -e "Failed: $FAILED_COUNT"
fi

# Calculate success rate
SUCCESS_RATE=$(echo "scale=1; $SUCCESS_COUNT * 100 / $TOTAL_COUNT" | bc -l 2>/dev/null || echo "N/A")
echo -e "Success Rate: ${SUCCESS_RATE}%"

echo ""

if [ $FAILED_COUNT -eq 0 ]; then
    echo -e "${GREEN}🎉 All style preview files are accessible!${NC}"
    echo -e "${GREEN}✅ Upload validation completed successfully${NC}"
    exit 0
else
    echo -e "${RED}❌ $FAILED_COUNT files failed validation${NC}"
    echo -e "${YELLOW}💡 Check the URLs above and retry upload if needed${NC}"
    exit 1
fi