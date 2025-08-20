#!/bin/bash

# AI Profile Photo Maker - Development Monitoring Script
# Auto-Delegation with Continuous Validation Loop

echo "👁️  Starting Development Environment Monitor"
echo "==========================================="
echo "Auto-restart enabled | Health checks every 30s"
echo "Press Ctrl+C to stop monitoring"
echo ""

RESTART_COUNT=0
MAX_FAILURES=3
CONSECUTIVE_FAILURES=0

# Create logs directory if it doesn't exist
mkdir -p logs

while true; do
    TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[$TIMESTAMP] 🔍 Health Check..."
    
    # Check API
    if curl -s http://localhost:5032/api/health >/dev/null 2>&1; then
        API_STATUS="✅ Healthy"
        API_HEALTHY=true
    else
        API_STATUS="❌ Down"
        API_HEALTHY=false
    fi
    
    # Check Frontend
    if curl -s http://localhost:4200 >/dev/null 2>&1; then
        FRONTEND_STATUS="✅ Healthy"
        FRONTEND_HEALTHY=true
    else
        FRONTEND_STATUS="❌ Down" 
        FRONTEND_HEALTHY=false
    fi
    
    # Check Database
    DB_PASS=${MSSQL_SA_PASSWORD:-Dev123456!}
    if docker exec aipm-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASS" -C -Q "SELECT 1" &>/dev/null; then
        DB_STATUS="✅ Connected"
        DB_HEALTHY=true
    else
        DB_STATUS="❌ Down"
        DB_HEALTHY=false
    fi
    
    # Check Integration (Frontend proxy to API)
    if curl -s http://localhost:4200/api/health >/dev/null 2>&1; then
        INTEGRATION_STATUS="✅ Working"
        INTEGRATION_HEALTHY=true
    else
        INTEGRATION_STATUS="❌ Failed"
        INTEGRATION_HEALTHY=false
    fi
    
    # Display status
    echo "   🔧 API:         $API_STATUS"
    echo "   🎨 Frontend:    $FRONTEND_STATUS"
    echo "   🗄️  Database:    $DB_STATUS"
    echo "   🔗 Integration: $INTEGRATION_STATUS"
    
    # Check if any service failed
    if [ "$API_HEALTHY" = false ] || [ "$FRONTEND_HEALTHY" = false ] || [ "$DB_HEALTHY" = false ] || [ "$INTEGRATION_HEALTHY" = false ]; then
        CONSECUTIVE_FAILURES=$((CONSECUTIVE_FAILURES + 1))
        echo "   ⚠️  Service failure detected (${CONSECUTIVE_FAILURES}/${MAX_FAILURES})"
        
        if [ $CONSECUTIVE_FAILURES -ge $MAX_FAILURES ]; then
            echo ""
            echo "🚨 MULTIPLE FAILURES DETECTED - ATTEMPTING AUTO-RESTART"
            echo "======================================================="
            
            RESTART_COUNT=$((RESTART_COUNT + 1))
            echo "Restart attempt #${RESTART_COUNT}"
            
            # Stop services
            ./dev-stop.sh
            sleep 5
            
            # Restart services
            ./dev-start.sh
            
            # Reset failure count
            CONSECUTIVE_FAILURES=0
            
            echo "🔄 Auto-restart completed. Resuming monitoring..."
            echo ""
        fi
    else
        # All services healthy - reset failure count
        if [ $CONSECUTIVE_FAILURES -gt 0 ]; then
            echo "   ✅ All services recovered!"
        fi
        CONSECUTIVE_FAILURES=0
    fi
    
    echo ""
    
    # Wait 30 seconds before next check
    sleep 30
done
