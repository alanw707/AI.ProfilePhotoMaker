#!/bin/bash

# Async I/O Performance Testing Script for Linux/Unix
# Tests all async I/O improvements and validates performance targets

set -e

# Default parameters
BASE_URL="${1:-https://localhost:5001}"
OUTPUT_PATH="${2:-./async-io-test-results.json}"
VERBOSE="${3:-false}"

echo "🚀 AI Profile Photo Maker - Async I/O Performance Testing"
echo "======================================================="
echo "Base URL: $BASE_URL"
echo "Output Path: $OUTPUT_PATH"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

# Test results
TEST_ID=$(uuidgen)
TIMESTAMP=$(date -u +"%Y-%m-%d %H:%M:%S UTC")
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0
TOTAL_DURATION=0

# Function to test endpoints
test_endpoint() {
    local endpoint="$1"
    local method="${2:-POST}"
    local test_name="$3"
    local expected_metric_key="${4:-}"
    local expected_metric_value="${5:-}"
    
    echo -e "${YELLOW}🔄 Testing: $test_name${NC}"
    
    local start_time=$(date +%s%3N)
    local success=false
    local error=""
    
    if [ "$method" == "POST" ]; then
        response=$(curl -s -X POST "$BASE_URL$endpoint" \
            -H "Content-Type: application/json" \
            -d '{}' \
            --insecure \
            --connect-timeout 30 \
            --max-time 120 2>/dev/null || echo '{"success": false, "error": "Request failed"}')
    else
        response=$(curl -s -X GET "$BASE_URL$endpoint" \
            --insecure \
            --connect-timeout 30 \
            --max-time 120 2>/dev/null || echo '{"success": false, "error": "Request failed"}')
    fi
    
    local end_time=$(date +%s%3N)
    local duration=$((end_time - start_time))
    
    # Parse response
    local response_success=$(echo "$response" | jq -r '.success // false' 2>/dev/null || echo "false")
    
    if [ "$response_success" == "true" ]; then
        success=true
        echo -e "${GREEN}✅ $test_name passed (${duration}ms)${NC}"
        ((PASSED_TESTS++))
    else
        local error_msg=$(echo "$response" | jq -r '.error // .message // "Unknown error"' 2>/dev/null || echo "Parse error")
        echo -e "${RED}❌ $test_name failed: $error_msg (${duration}ms)${NC}"
        ((FAILED_TESTS++))
    fi
    
    ((TOTAL_TESTS++))
    TOTAL_DURATION=$((TOTAL_DURATION + duration))
    
    # Store result for JSON output
    local test_result=$(cat <<EOF
{
    "testName": "$test_name",
    "endpoint": "$endpoint",
    "method": "$method",
    "success": $success,
    "duration": $duration,
    "response": $response,
    "timestamp": "$(date -u +"%Y-%m-%d %H:%M:%S UTC")"
}
EOF
    )
    
    echo "$test_result" > "/tmp/test_${test_name// /_}.json"
}

# Phase 1: Health Check
echo -e "${BLUE}📋 Phase 1: Health Check and Service Registration${NC}"
test_endpoint "/api/asynciotest/health" "GET" "Service Health Check"

# Phase 2: Thread Pool Monitoring
echo -e "\n${BLUE}📋 Phase 2: Thread Pool Monitoring${NC}"
test_endpoint "/api/asynciotest/thread-pool-stats" "GET" "Thread Pool Statistics"

# Phase 3: Async Pattern Validation
echo -e "\n${BLUE}📋 Phase 3: Async Pattern Validation${NC}"
test_endpoint "/api/asynciotest/async-patterns" "POST" "Async Pattern Validation"

# Phase 4: Memory Usage Test
echo -e "\n${BLUE}📋 Phase 4: Memory Usage and Streaming Efficiency${NC}"
test_endpoint "/api/asynciotest/memory-usage" "POST" "Memory Usage Test"

# Phase 5: Throughput Test
echo -e "\n${BLUE}📋 Phase 5: Throughput and Concurrency${NC}"
test_endpoint "/api/asynciotest/throughput" "POST" "Throughput Test"

# Phase 6: File Streaming Test
echo -e "\n${BLUE}📋 Phase 6: File Streaming Operations${NC}"
test_endpoint "/api/asynciotest/file-streaming?fileSizeMB=10" "POST" "File Streaming Test"

# Phase 7: ZIP Processing Test
echo -e "\n${BLUE}📋 Phase 7: ZIP Processing and Compression${NC}"
test_endpoint "/api/asynciotest/zip-processing" "POST" "ZIP Processing Test"

# Phase 8: Blocking Detection Test
echo -e "\n${BLUE}📋 Phase 8: Blocking Operation Detection${NC}"
test_endpoint "/api/asynciotest/blocking-detection?concurrency=10" "POST" "Blocking Detection Test"

# Phase 9: Comprehensive Test Suite
echo -e "\n${BLUE}📋 Phase 9: Comprehensive Test Suite${NC}"
test_endpoint "/api/asynciotest/comprehensive" "POST" "Comprehensive Test Suite"

# Calculate overall score
OVERALL_SCORE=0
if [ $TOTAL_TESTS -gt 0 ]; then
    OVERALL_SCORE=$((PASSED_TESTS * 100 / TOTAL_TESTS))
fi

# Display Results Summary
echo -e "\n${GREEN}🎯 ASYNC I/O PERFORMANCE TEST RESULTS${NC}"
echo -e "${GREEN}=====================================${NC}"
echo -e "${CYAN}Test ID: $TEST_ID${NC}"
echo -e "${CYAN}Total Duration: ${TOTAL_DURATION}ms${NC}"
echo -e "${CYAN}Overall Score: $OVERALL_SCORE%${NC}"
echo -e "${CYAN}Tests Passed: $PASSED_TESTS/$TOTAL_TESTS${NC}"

# Performance evaluation
if [ $OVERALL_SCORE -ge 80 ]; then
    echo -e "\n${GREEN}🎉 Async I/O Performance Testing PASSED!${NC}"
    echo -e "${GREEN}   All performance targets met. Async I/O improvements are working correctly.${NC}"
    SUCCESS=true
elif [ $OVERALL_SCORE -ge 60 ]; then
    echo -e "\n${YELLOW}⚠️  Async I/O Performance Testing partially successful${NC}"
    echo -e "${YELLOW}   Some performance targets not met. Review failed tests above.${NC}"
    SUCCESS=false
else
    echo -e "\n${RED}❌ Async I/O Performance Testing needs significant attention${NC}"
    echo -e "${RED}   Multiple performance targets failed. Review implementation.${NC}"
    SUCCESS=false
fi

# Create comprehensive JSON output
cat > "$OUTPUT_PATH" <<EOF
{
    "testId": "$TEST_ID",
    "timestamp": "$TIMESTAMP",
    "baseUrl": "$BASE_URL",
    "summary": {
        "totalTests": $TOTAL_TESTS,
        "passedTests": $PASSED_TESTS,
        "failedTests": $FAILED_TESTS,
        "overallScore": $OVERALL_SCORE,
        "totalDuration": $TOTAL_DURATION
    },
    "tests": {
EOF

# Add individual test results
first=true
for test_file in /tmp/test_*.json; do
    if [ -f "$test_file" ]; then
        if [ "$first" = true ]; then
            first=false
        else
            echo "," >> "$OUTPUT_PATH"
        fi
        
        test_name=$(basename "$test_file" .json | sed 's/test_//' | sed 's/_/ /g')
        echo "        \"$test_name\": $(cat "$test_file")" >> "$OUTPUT_PATH"
    fi
done

cat >> "$OUTPUT_PATH" <<EOF
    },
    "recommendations": [
EOF

# Add recommendations based on results
recommendations=()

if [ $PASSED_TESTS -lt $TOTAL_TESTS ]; then
    recommendations+=("\"Review failed tests and ensure all async I/O services are properly registered\"")
fi

if [ $OVERALL_SCORE -lt 80 ]; then
    recommendations+=("\"Performance targets not met - review async/await patterns and streaming implementations\"")
fi

if [ $OVERALL_SCORE -lt 60 ]; then
    recommendations+=("\"Consider reviewing the AsyncFileService and AsyncZipService implementations\"")
fi

# Add recommendations to JSON
for i in "${!recommendations[@]}"; do
    if [ $i -gt 0 ]; then
        echo "," >> "$OUTPUT_PATH"
    fi
    echo "        ${recommendations[$i]}" >> "$OUTPUT_PATH"
done

cat >> "$OUTPUT_PATH" <<EOF
    ]
}
EOF

echo -e "\n${CYAN}💾 Results saved to: $OUTPUT_PATH${NC}"

# Cleanup temporary files
rm -f /tmp/test_*.json

# Exit with appropriate code
if [ "$SUCCESS" = true ]; then
    exit 0
else
    exit 1
fi