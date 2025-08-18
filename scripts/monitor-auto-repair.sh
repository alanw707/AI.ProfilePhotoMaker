#!/bin/bash

# Auto-Repair Monitoring Script
# Monitors auto-repair operations and provides real-time status

set -e

# Configuration
MONITOR_DURATION=${1:-3600}  # Default: 1 hour
LOG_FILE="auto-repair-monitor.log"
ALERT_THRESHOLD_ERROR_RATE=10  # 10% error rate triggers alert
ALERT_THRESHOLD_FREQUENCY=50   # 50 repairs per hour triggers alert

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Counters
REPAIR_COUNT=0
SUCCESS_COUNT=0
ERROR_COUNT=0
DRY_RUN_COUNT=0
START_TIME=$(date +%s)

echo -e "${BLUE}🔧 Auto-Repair Monitoring Started${NC}"
echo "=================================="
echo "Duration: $MONITOR_DURATION seconds"
echo "Log file: $LOG_FILE"
echo "Started at: $(date)"
echo ""

# Function to log with timestamp
log_message() {
    local level=$1
    local message=$2
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[$timestamp] [$level] $message" >> "$LOG_FILE"
}

# Function to display statistics
show_stats() {
    local current_time=$(date +%s)
    local elapsed=$((current_time - START_TIME))
    local hours_elapsed=$(echo "scale=2; $elapsed / 3600" | bc -l 2>/dev/null || echo "0")
    
    local error_rate=0
    if [ $REPAIR_COUNT -gt 0 ]; then
        error_rate=$(echo "scale=2; $ERROR_COUNT * 100 / $REPAIR_COUNT" | bc -l 2>/dev/null || echo "0")
    fi
    
    local repairs_per_hour=0
    if [ $elapsed -gt 0 ]; then
        repairs_per_hour=$(echo "scale=2; $REPAIR_COUNT * 3600 / $elapsed" | bc -l 2>/dev/null || echo "0")
    fi
    
    echo -e "\n${CYAN}📊 Current Statistics${NC}"
    echo "-------------------"
    echo -e "Elapsed Time: ${hours_elapsed}h"
    echo -e "Total Repairs: $REPAIR_COUNT"
    echo -e "  ├─ Successful: ${GREEN}$SUCCESS_COUNT${NC}"
    echo -e "  ├─ Errors: ${RED}$ERROR_COUNT${NC}"
    echo -e "  └─ Dry Runs: ${YELLOW}$DRY_RUN_COUNT${NC}"
    echo -e "Error Rate: $error_rate%"
    echo -e "Frequency: $repairs_per_hour repairs/hour"
    
    # Alert checks
    if (( $(echo "$error_rate > $ALERT_THRESHOLD_ERROR_RATE" | bc -l 2>/dev/null || echo "0") )); then
        echo -e "${RED}🚨 ALERT: High error rate ($error_rate%)${NC}"
        log_message "ALERT" "High error rate: $error_rate%"
    fi
    
    if (( $(echo "$repairs_per_hour > $ALERT_THRESHOLD_FREQUENCY" | bc -l 2>/dev/null || echo "0") )); then
        echo -e "${RED}🚨 ALERT: High repair frequency ($repairs_per_hour/hour)${NC}"
        log_message "ALERT" "High repair frequency: $repairs_per_hour/hour"
    fi
}

# Function to check application logs for auto-repair events
check_app_logs() {
    # Check various log sources for auto-repair activity
    local log_patterns=(
        "Auto-repair triggered"
        "Database repair failed"
        "repairImageDatabase"
        "🔧.*repair"
        "validateAndCleanupImages.*repair"
    )
    
    # Check Docker logs if available
    if command -v docker >/dev/null 2>&1; then
        for container in $(docker ps --format "{{.Names}}" 2>/dev/null | grep -i "profile\|photo\|api" || true); do
            for pattern in "${log_patterns[@]}"; do
                local matches=$(docker logs --since="1m" "$container" 2>/dev/null | grep -i "$pattern" || true)
                if [ ! -z "$matches" ]; then
                    echo -e "${YELLOW}📱 Container $container:${NC}"
                    echo "$matches" | tail -5
                    
                    # Count repair events
                    if echo "$matches" | grep -q "Auto-repair triggered"; then
                        REPAIR_COUNT=$((REPAIR_COUNT + 1))
                        log_message "INFO" "Auto-repair triggered in container $container"
                    fi
                    
                    if echo "$matches" | grep -q "repair.*success\|repair.*complete"; then
                        SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
                        log_message "SUCCESS" "Auto-repair successful in container $container"
                    fi
                    
                    if echo "$matches" | grep -q "repair.*fail\|repair.*error"; then
                        ERROR_COUNT=$((ERROR_COUNT + 1))
                        log_message "ERROR" "Auto-repair failed in container $container"
                    fi
                    
                    if echo "$matches" | grep -q "DRY-RUN\|dry.run"; then
                        DRY_RUN_COUNT=$((DRY_RUN_COUNT + 1))
                        log_message "DRY_RUN" "Auto-repair dry-run in container $container"
                    fi
                fi
            done
        done
    fi
    
    # Check system logs
    if command -v journalctl >/dev/null 2>&1; then
        for pattern in "${log_patterns[@]}"; do
            local matches=$(journalctl --since="1 minute ago" -u "*profile*" -u "*photo*" --no-pager -q 2>/dev/null | grep -i "$pattern" || true)
            if [ ! -z "$matches" ]; then
                echo -e "${YELLOW}📋 System logs:${NC}"
                echo "$matches" | tail -3
            fi
        done
    fi
    
    # Check application log files
    local app_log_paths=(
        "/var/log/profilephotomaker/"
        "./logs/"
        "./AI.ProfilePhotoMaker.API/logs/"
        "/home/alanw/projects/AI.ProfilePhotoMaker/logs/"
    )
    
    for log_path in "${app_log_paths[@]}"; do
        if [ -d "$log_path" ]; then
            for pattern in "${log_patterns[@]}"; do
                local matches=$(find "$log_path" -name "*.log" -mmin -1 -exec grep -l "$pattern" {} \; 2>/dev/null || true)
                if [ ! -z "$matches" ]; then
                    echo -e "${YELLOW}📁 Log files ($log_path):${NC}"
                    echo "$matches"
                fi
            done
        fi
    done
}

# Function to check API health and auto-repair status
check_api_status() {
    local api_base_url="http://localhost:5032"
    
    # Try different possible API URLs
    local api_urls=(
        "http://localhost:5032"
        "http://localhost:5000"
        "https://api.aiprofilephotomaker.com"
    )
    
    for url in "${api_urls[@]}"; do
        # Check basic health
        local health_response=$(curl -s -w "%{http_code}" -o /dev/null "$url/health" 2>/dev/null || echo "000")
        if [ "$health_response" == "200" ]; then
            echo -e "${GREEN}✅ API Health ($url): OK${NC}"
            
            # Check if there's a repair status endpoint
            local repair_status=$(curl -s "$url/api/admin/repair-status" 2>/dev/null || echo "")
            if [ ! -z "$repair_status" ]; then
                echo -e "${CYAN}🔧 Repair Status:${NC} $repair_status"
            fi
            break
        else
            echo -e "${RED}❌ API Health ($url): $health_response${NC}"
        fi
    done
}

# Function to monitor database activity
monitor_database() {
    # This would require database credentials and appropriate tools
    # For now, just check if database monitoring tools are available
    
    if command -v sqlcmd >/dev/null 2>&1; then
        echo -e "${CYAN}🗄️  Database monitoring available (SQL Server)${NC}"
    elif command -v psql >/dev/null 2>&1; then
        echo -e "${CYAN}🗄️  Database monitoring available (PostgreSQL)${NC}"
    else
        echo -e "${YELLOW}⚠️  Database monitoring tools not available${NC}"
    fi
}

# Function to monitor system resources
monitor_resources() {
    local cpu_usage=$(top -bn1 | grep "Cpu(s)" | awk '{print $2}' | cut -d'%' -f1 || echo "0")
    local memory_usage=$(free | grep Mem | awk '{printf "%.1f", $3/$2 * 100.0}' || echo "0")
    local disk_usage=$(df / | tail -1 | awk '{print $5}' | cut -d'%' -f1 || echo "0")
    
    echo -e "${CYAN}💻 System Resources${NC}"
    echo "  CPU: $cpu_usage%"
    echo "  Memory: $memory_usage%"
    echo "  Disk: $disk_usage%"
    
    # Alert on high resource usage
    if (( $(echo "$cpu_usage > 80" | bc -l 2>/dev/null || echo "0") )); then
        echo -e "${RED}🚨 ALERT: High CPU usage ($cpu_usage%)${NC}"
        log_message "ALERT" "High CPU usage: $cpu_usage%"
    fi
    
    if (( $(echo "$memory_usage > 85" | bc -l 2>/dev/null || echo "0") )); then
        echo -e "${RED}🚨 ALERT: High memory usage ($memory_usage%)${NC}"
        log_message "ALERT" "High memory usage: $memory_usage%"
    fi
}

# Initialize log file
log_message "START" "Auto-repair monitoring started for $MONITOR_DURATION seconds"

# Main monitoring loop
echo -e "${BLUE}Starting monitoring loop...${NC}"
END_TIME=$((START_TIME + MONITOR_DURATION))

while [ $(date +%s) -lt $END_TIME ]; do
    clear
    echo -e "${BLUE}🔧 Auto-Repair Monitor${NC} - $(date)"
    echo "=================================="
    
    # Check application logs for repair activity
    check_app_logs
    
    # Check API status
    echo ""
    check_api_status
    
    # Monitor system resources
    echo ""
    monitor_resources
    
    # Monitor database (if tools available)
    echo ""
    monitor_database
    
    # Show current statistics
    show_stats
    
    # Log current status
    log_message "STATUS" "Repairs: $REPAIR_COUNT, Success: $SUCCESS_COUNT, Errors: $ERROR_COUNT"
    
    echo ""
    echo -e "${CYAN}Next update in 30 seconds... (Ctrl+C to stop)${NC}"
    sleep 30
done

echo -e "\n${BLUE}🔧 Auto-Repair Monitoring Completed${NC}"
echo "===================================="
echo "Total duration: $MONITOR_DURATION seconds"
echo "Final statistics:"
show_stats

log_message "END" "Auto-repair monitoring completed"

# Generate summary report
echo -e "\n${CYAN}📋 Summary Report${NC}"
echo "----------------"
echo "Log file: $LOG_FILE"
echo "Monitoring period: $(date -d "@$START_TIME" '+%Y-%m-%d %H:%M:%S') to $(date '+%Y-%m-%d %H:%M:%S')"

if [ $ERROR_COUNT -gt 0 ]; then
    echo -e "${RED}⚠️  Issues detected during monitoring period${NC}"
    echo "Review log file for details and consider investigating auto-repair configuration."
elif [ $REPAIR_COUNT -gt 20 ]; then
    echo -e "${YELLOW}⚠️  High repair activity detected${NC}"
    echo "Consider investigating root cause of frequent repairs."
else
    echo -e "${GREEN}✅ Monitoring completed successfully${NC}"
    echo "Auto-repair system appears to be functioning normally."
fi