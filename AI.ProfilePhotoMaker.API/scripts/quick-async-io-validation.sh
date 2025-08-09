#!/bin/bash

# Quick Async I/O Validation Script
# Performs essential checks to validate async I/O improvements are working

set -e

BASE_URL="${1:-http://localhost:5032}"
echo "🚀 Quick Async I/O Validation"
echo "=============================="
echo "Target URL: $BASE_URL"
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Track results
CHECKS_PASSED=0
CHECKS_FAILED=0
TOTAL_CHECKS=5

# Function to check endpoint
check_endpoint() {
    local endpoint="$1"
    local test_name="$2"
    local expected_success="${3:-true}"
    
    echo -e "${BLUE}🔄 Checking: $test_name${NC}"
    
    response=$(curl -s -w "HTTPSTATUS:%{http_code}" -X GET "$BASE_URL$endpoint" --connect-timeout 10 --max-time 30 2>/dev/null || echo "HTTPSTATUS:000")
    
    http_code=$(echo "$response" | grep -o "HTTPSTATUS:[0-9]*" | cut -d: -f2)
    body=$(echo "$response" | sed 's/HTTPSTATUS:[0-9]*$//')
    
    if [ "$http_code" == "200" ]; then
        echo -e "${GREEN}✅ $test_name: SUCCESS (HTTP $http_code)${NC}"
        ((CHECKS_PASSED++))
        return 0
    else
        echo -e "${RED}❌ $test_name: FAILED (HTTP $http_code)${NC}"
        if [ ! -z "$body" ] && [ "$body" != "HTTPSTATUS:000" ]; then
            echo "   Response: $(echo "$body" | head -c 200)..."
        fi
        ((CHECKS_FAILED++))
        return 1
    fi
}

echo -e "${BLUE}📋 Phase 1: Service Health Checks${NC}"

# Check 1: Application Health
if check_endpoint "/api/health" "Application Health Check"; then
    echo "   API is responding"
else
    echo "   ⚠️  API may not be running or accessible"
fi

# Check 2: Async I/O Service Health  
if check_endpoint "/api/asynciotest/health" "Async I/O Service Registration"; then
    echo "   AsyncFileService and AsyncZipService are registered"
else
    echo "   ⚠️  Async I/O services may not be registered in Program.cs"
    echo "   Add these lines to Program.cs:"
    echo "   builder.Services.AddScoped<IAsyncFileService, AsyncFileService>();"
    echo "   builder.Services.AddScoped<IAsyncZipService, AsyncZipService>();"
fi

# Check 3: Thread Pool Monitoring
if check_endpoint "/api/asynciotest/thread-pool-stats" "Thread Pool Statistics"; then
    echo "   Thread pool monitoring is active"
else
    echo "   ⚠️  Thread pool monitoring may not be working"
fi

echo ""
echo -e "${BLUE}📋 Phase 2: Performance Test Availability${NC}"

# Check 4: Memory Usage Test Endpoint
if check_endpoint "/api/asynciotest/memory-usage" "Memory Usage Test Endpoint" "false"; then
    echo "   Memory usage tests are available"
else
    echo "   ℹ️  Memory test endpoint check (test execution would require POST)"
fi

# Check 5: Comprehensive Test Suite Endpoint
if check_endpoint "/api/asynciotest/comprehensive" "Comprehensive Test Suite" "false"; then
    echo "   Comprehensive test suite is available"
else
    echo "   ℹ️  Comprehensive test endpoint check (test execution would require POST)"
fi

echo ""
echo -e "${BLUE}📋 Final Assessment${NC}"
echo "======================"

OVERALL_SCORE=$((CHECKS_PASSED * 100 / TOTAL_CHECKS))

if [ $OVERALL_SCORE -ge 80 ]; then
    echo -e "${GREEN}🎉 Async I/O Validation: PASSED${NC}"
    echo -e "${GREEN}   Score: $OVERALL_SCORE% ($CHECKS_PASSED/$TOTAL_CHECKS tests passed)${NC}"
    echo ""
    echo -e "${GREEN}✅ Ready to execute full performance tests:${NC}"
    echo "   ./scripts/test-async-io-performance.sh $BASE_URL"
    echo "   curl -X POST $BASE_URL/api/asynciotest/comprehensive"
    echo ""
    exit 0
elif [ $OVERALL_SCORE -ge 60 ]; then
    echo -e "${YELLOW}⚠️  Async I/O Validation: PARTIAL${NC}"
    echo -e "${YELLOW}   Score: $OVERALL_SCORE% ($CHECKS_PASSED/$TOTAL_CHECKS tests passed)${NC}"
    echo ""
    echo -e "${YELLOW}🔧 Next Steps:${NC}"
    if [ $CHECKS_FAILED -gt 0 ]; then
        echo "   1. Fix failed service registrations above"
        echo "   2. Ensure Program.cs includes async I/O services"
        echo "   3. Restart application and re-run validation"
    fi
    echo ""
    exit 1
else
    echo -e "${RED}❌ Async I/O Validation: FAILED${NC}"
    echo -e "${RED}   Score: $OVERALL_SCORE% ($CHECKS_PASSED/$TOTAL_CHECKS tests passed)${NC}"
    echo ""
    echo -e "${RED}🚨 Critical Issues Detected:${NC}"
    echo "   1. Application may not be running"
    echo "   2. Async I/O services not registered"
    echo "   3. Test endpoints not accessible"
    echo ""
    echo -e "${RED}🔧 Required Actions:${NC}"
    echo "   1. Ensure application is running on $BASE_URL"
    echo "   2. Add async I/O service registration to Program.cs:"
    echo "      builder.Services.AddScoped<IAsyncFileService, AsyncFileService>();"
    echo "      builder.Services.AddScoped<IAsyncZipService, AsyncZipService>();"
    echo "   3. Add middleware to pipeline:"
    echo "      app.UseAsyncIoPerformanceMonitoring();"
    echo "   4. Restart application"
    echo ""
    exit 2
fi