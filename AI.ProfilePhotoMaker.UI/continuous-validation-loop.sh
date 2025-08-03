#!/bin/bash

# Automated Validation Loop for Styles API
# Continuously tests the styles API until database fix is verified successful

API_URL="https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/style"
INTERVAL=30  # seconds
MAX_DURATION=900  # 15 minutes
SUCCESS_THRESHOLD=20
START_TIME=$(date +%s)
TEST_COUNT=0
LAST_STATUS=""
LOG_FILE="validation-loop-$(date +%Y%m%d-%H%M%S).log"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Function to log with timestamp
log_with_timestamp() {
    local message="$1"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo -e "$message" | tee -a "$LOG_FILE"
    echo "[$timestamp] $message" >> "$LOG_FILE"
}

# Function to get elapsed time
get_elapsed_time() {
    local current_time=$(date +%s)
    local elapsed=$((current_time - START_TIME))
    printf "%02d:%02d" $((elapsed / 60)) $((elapsed % 60))
}

# Function to test API
test_api() {
    local test_start=$(date +%s%3N)  # milliseconds
    
    # Make API call with timeout
    local response=$(curl -s --max-time 10 "$API_URL" 2>/dev/null)
    local curl_exit_code=$?
    
    local test_end=$(date +%s%3N)
    local response_time=$((test_end - test_start))
    
    # Initialize result variables
    local http_status="NETWORK_ERROR"
    local is_valid_json=false
    local style_count=0
    local error_message=""
    local success=false
    
    if [ $curl_exit_code -eq 0 ] && [ -n "$response" ]; then
        # Check if response is valid JSON
        if echo "$response" | python3 -m json.tool > /dev/null 2>&1; then
            is_valid_json=true
            http_status="200"
            
            # Extract style count and check response structure
            local json_result=$(echo "$response" | python3 -c "
import sys, json
try:
    data = json.load(sys.stdin)
    style_count = 0
    success = False
    error_msg = ''
    
    if 'success' in data and data['success'] == True:
        success = True
        if 'data' in data and isinstance(data['data'], list):
            style_count = len(data['data'])
        else:
            error_msg = 'Invalid data structure'
    else:
        if 'error' in data and data['error']:
            error_msg = str(data['error'])
        else:
            error_msg = 'API returned success=false'
    
    print(f'{style_count}|{success}|{error_msg}')
except Exception as e:
    print(f'0|False|JSON parsing error: {e}')
")
            
            IFS='|' read -r style_count success error_message <<< "$json_result"
        else
            # Invalid JSON - likely HTTP 500 error
            http_status="500"
            error_message="Invalid JSON response (likely HTTP 500)"
        fi
    else
        error_message="Network error or timeout"
    fi
    
    # Return results
    echo "$http_status|$is_valid_json|$style_count|$success|$error_message|$response_time"
}

# Function to display status update
display_status() {
    local http_status="$1"
    local is_valid_json="$2"
    local style_count="$3"
    local success="$4"
    local error_message="$5"
    local response_time="$6"
    local elapsed_time="$7"
    
    echo
    log_with_timestamp "${CYAN}=== TEST #$TEST_COUNT | ELAPSED: $elapsed_time ===${NC}"
    log_with_timestamp "${BLUE}🌐 API:${NC} $API_URL"
    
    if [ "$http_status" = "200" ] && [ "$is_valid_json" = "true" ]; then
        log_with_timestamp "${GREEN}✅ HTTP Status:${NC} $http_status (Valid JSON)"
        log_with_timestamp "${GREEN}⚡ Response Time:${NC} ${response_time}ms"
        
        if [ "$success" = "True" ]; then
            log_with_timestamp "${GREEN}📊 Style Count:${NC} $style_count"
            
            if [ "$style_count" -ge "$SUCCESS_THRESHOLD" ]; then
                log_with_timestamp "${GREEN}🎉 SUCCESS CRITERIA MET!${NC}"
                return 0  # Success
            else
                log_with_timestamp "${YELLOW}⚠️  Insufficient styles${NC} (need $SUCCESS_THRESHOLD+)"
            fi
        else
            log_with_timestamp "${RED}❌ API Success:${NC} false"
            if [ -n "$error_message" ]; then
                log_with_timestamp "${RED}🚨 Error:${NC} $error_message"
            fi
        fi
    else
        if [ "$http_status" = "500" ]; then
            log_with_timestamp "${RED}❌ HTTP Status:${NC} 500 (Server Error)"
        else
            log_with_timestamp "${RED}❌ HTTP Status:${NC} $http_status"
        fi
        log_with_timestamp "${RED}⚡ Response Time:${NC} ${response_time}ms"
        if [ -n "$error_message" ]; then
            log_with_timestamp "${RED}🚨 Error:${NC} $error_message"
        fi
    fi
    
    return 1  # Continue testing
}

# Function to generate final report
generate_final_report() {
    local final_status="$1"
    local total_tests="$2"
    local total_time="$3"
    
    echo
    log_with_timestamp "${CYAN}================================${NC}"
    log_with_timestamp "${CYAN}    FINAL VALIDATION REPORT${NC}"
    log_with_timestamp "${CYAN}================================${NC}"
    
    log_with_timestamp "${BLUE}📊 Test Summary:${NC}"
    log_with_timestamp "  • Total Tests: $total_tests"
    log_with_timestamp "  • Total Duration: $total_time"
    log_with_timestamp "  • Test Interval: ${INTERVAL}s"
    log_with_timestamp "  • API Endpoint: $API_URL"
    
    if [ "$final_status" = "SUCCESS" ]; then
        log_with_timestamp "${GREEN}🎉 RESULT: DATABASE FIX VERIFIED SUCCESSFUL${NC}"
        log_with_timestamp "${GREEN}✅ Styles API is now fully functional${NC}"
        log_with_timestamp "${GREEN}✅ Application ready for production use${NC}"
    else
        log_with_timestamp "${RED}❌ RESULT: DATABASE FIX NOT YET APPLIED${NC}"
        log_with_timestamp "${YELLOW}⏳ Continue manual fix via Azure Portal${NC}"
        log_with_timestamp "${YELLOW}🔧 Re-run this script after applying database changes${NC}"
    fi
    
    log_with_timestamp "${BLUE}📁 Log File:${NC} $LOG_FILE"
    echo
}

# Main execution
echo
log_with_timestamp "${CYAN}🚀 STARTING AUTOMATED VALIDATION LOOP${NC}"
log_with_timestamp "${CYAN}=====================================${NC}"
log_with_timestamp "${BLUE}📡 API Endpoint:${NC} $API_URL"
log_with_timestamp "${BLUE}⏱️  Test Interval:${NC} ${INTERVAL}s"
log_with_timestamp "${BLUE}⏰ Max Duration:${NC} ${MAX_DURATION}s (15 minutes)"
log_with_timestamp "${BLUE}🎯 Success Criteria:${NC} ${SUCCESS_THRESHOLD}+ styles"
log_with_timestamp "${BLUE}📝 Log File:${NC} $LOG_FILE"
echo

# Main testing loop
while true; do
    current_time=$(date +%s)
    elapsed_total=$((current_time - START_TIME))
    
    # Check if we've exceeded maximum duration
    if [ $elapsed_total -ge $MAX_DURATION ]; then
        log_with_timestamp "${YELLOW}⏰ Maximum test duration reached (15 minutes)${NC}"
        break
    fi
    
    TEST_COUNT=$((TEST_COUNT + 1))
    elapsed_time=$(get_elapsed_time)
    
    # Test the API
    IFS='|' read -r http_status is_valid_json style_count success error_message response_time <<< "$(test_api)"
    
    # Display status and check for success
    if display_status "$http_status" "$is_valid_json" "$style_count" "$success" "$error_message" "$response_time" "$elapsed_time"; then
        # Success criteria met
        generate_final_report "SUCCESS" "$TEST_COUNT" "$elapsed_time"
        
        # Run the original verification script for detailed output
        echo
        log_with_timestamp "${CYAN}🔍 Running detailed verification...${NC}"
        ./verify-styles-fix.sh
        
        exit 0
    fi
    
    # Wait for next test (unless this is the last iteration)
    remaining_time=$((MAX_DURATION - elapsed_total))
    if [ $remaining_time -gt $INTERVAL ]; then
        log_with_timestamp "${BLUE}⏳ Waiting ${INTERVAL}s for next test...${NC}"
        sleep $INTERVAL
    else
        break
    fi
done

# Generate final report for timeout/failure
elapsed_time=$(get_elapsed_time)
generate_final_report "TIMEOUT" "$TEST_COUNT" "$elapsed_time"

log_with_timestamp "${YELLOW}💡 Next Steps:${NC}"
log_with_timestamp "  1. Apply database fix via Azure Portal"
log_with_timestamp "  2. Re-run this validation script"
log_with_timestamp "  3. Or run: ./verify-styles-fix.sh for single test"

exit 1