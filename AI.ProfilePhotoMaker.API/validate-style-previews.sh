#!/bin/bash

# Validate Style Preview Images Upload
# Tests all uploaded style preview images for accessibility and performance

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🔍 Style Preview Validation Script${NC}"
echo -e "${BLUE}===================================${NC}"
echo ""

# Azure Storage details
STORAGE_ACCOUNT="aipmstv16j74jubocuukg"
CONTAINER="style-previews"
BASE_URL="https://${STORAGE_ACCOUNT}.blob.core.windows.net/${CONTAINER}"

# Style files to test
STYLES=(
    "academic" "artistic" "author" "casual" "consultant" "corporate"
    "creative" "digital-nomad" "edgy-urban" "entrepreneur" "executive"
    "fashion" "fitness" "glamour" "influencer" "legal" "linkedin"
    "medical" "spiritual" "startup" "tech-professional"
)

echo -e "${YELLOW}Testing ${#STYLES[@]} style preview images...${NC}"
echo ""

# Initialize counters
TOTAL=0
SUCCESS=0
FAILED=0
TOTAL_SIZE=0
MIN_SIZE=999999999
MAX_SIZE=0
MIN_TIME=999999
MAX_TIME=0
TOTAL_TIME=0

echo "STATUS   SIZE        TIME    FILE"
echo "------   ---------   ------  ----------------"

for style in "${STYLES[@]}"; do
    TOTAL=$((TOTAL + 1))
    URL="${BASE_URL}/${style}.jpg"
    
    # Test the URL and capture response time
    START_TIME=$(date +%s%N)
    RESPONSE=$(curl -s -I "$URL" 2>/dev/null || echo "ERROR")
    END_TIME=$(date +%s%N)
    
    # Calculate response time in milliseconds
    RESPONSE_TIME=$(( (END_TIME - START_TIME) / 1000000 ))
    TOTAL_TIME=$((TOTAL_TIME + RESPONSE_TIME))
    
    if echo "$RESPONSE" | grep -q "HTTP/1.1 200 OK"; then
        # Get content length
        SIZE=$(echo "$RESPONSE" | grep -i "content-length" | cut -d' ' -f2 | tr -d '\r')
        
        if [[ -n "$SIZE" && "$SIZE" -gt 0 ]]; then
            SUCCESS=$((SUCCESS + 1))
            TOTAL_SIZE=$((TOTAL_SIZE + SIZE))
            
            # Track min/max sizes
            if [[ $SIZE -lt $MIN_SIZE ]]; then MIN_SIZE=$SIZE; fi
            if [[ $SIZE -gt $MAX_SIZE ]]; then MAX_SIZE=$SIZE; fi
            
            # Track min/max times
            if [[ $RESPONSE_TIME -lt $MIN_TIME ]]; then MIN_TIME=$RESPONSE_TIME; fi
            if [[ $RESPONSE_TIME -gt $MAX_TIME ]]; then MAX_TIME=$RESPONSE_TIME; fi
            
            printf "✅ PASS  %9s   %4sms  %s.jpg\n" "$(numfmt --to=iec --suffix=B $SIZE)" "$RESPONSE_TIME" "$style"
        else
            FAILED=$((FAILED + 1))
            printf "❌ FAIL  %9s   %4sms  %s.jpg (no size)\n" "0B" "$RESPONSE_TIME" "$style"
        fi
    else
        FAILED=$((FAILED + 1))
        printf "❌ FAIL  %9s   %4sms  %s.jpg (not accessible)\n" "0B" "$RESPONSE_TIME" "$style"
    fi
done

echo ""
echo -e "${BLUE}=== Validation Summary ===${NC}"
echo ""

# Success rate
SUCCESS_RATE=$((SUCCESS * 100 / TOTAL))
if [[ $SUCCESS_RATE -ge 95 ]]; then
    echo -e "📊 Success Rate: ${GREEN}${SUCCESS_RATE}%${NC} (${SUCCESS}/${TOTAL})"
elif [[ $SUCCESS_RATE -ge 80 ]]; then
    echo -e "📊 Success Rate: ${YELLOW}${SUCCESS_RATE}%${NC} (${SUCCESS}/${TOTAL})"
else
    echo -e "📊 Success Rate: ${RED}${SUCCESS_RATE}%${NC} (${SUCCESS}/${TOTAL})"
fi

# Size statistics
if [[ $SUCCESS -gt 0 ]]; then
    AVG_SIZE=$((TOTAL_SIZE / SUCCESS))
    echo -e "📏 Total Size: ${GREEN}$(numfmt --to=iec --suffix=B $TOTAL_SIZE)${NC}"
    echo -e "📏 Average Size: ${GREEN}$(numfmt --to=iec --suffix=B $AVG_SIZE)${NC}"
    echo -e "📏 Size Range: ${GREEN}$(numfmt --to=iec --suffix=B $MIN_SIZE)${NC} - ${GREEN}$(numfmt --to=iec --suffix=B $MAX_SIZE)${NC}"
fi

# Performance statistics
if [[ $SUCCESS -gt 0 ]]; then
    AVG_TIME=$((TOTAL_TIME / TOTAL))
    echo -e "⚡ Average Response Time: ${GREEN}${AVG_TIME}ms${NC}"
    echo -e "⚡ Response Time Range: ${GREEN}${MIN_TIME}ms${NC} - ${GREEN}${MAX_TIME}ms${NC}"
    
    if [[ $AVG_TIME -lt 200 ]]; then
        echo -e "⚡ Performance: ${GREEN}Excellent${NC} (< 200ms)"
    elif [[ $AVG_TIME -lt 500 ]]; then
        echo -e "⚡ Performance: ${YELLOW}Good${NC} (< 500ms)"
    else
        echo -e "⚡ Performance: ${RED}Slow${NC} (> 500ms)"
    fi
fi

echo ""

# Final result
if [[ $SUCCESS_RATE -ge 95 ]]; then
    echo -e "${GREEN}🎉 Validation PASSED! Style preview images are successfully deployed and accessible.${NC}"
    exit 0
elif [[ $SUCCESS_RATE -ge 80 ]]; then
    echo -e "${YELLOW}⚠️  Validation PARTIAL. Most images accessible but some issues detected.${NC}"
    exit 1
else
    echo -e "${RED}❌ Validation FAILED. Significant issues with image accessibility.${NC}"
    exit 2
fi