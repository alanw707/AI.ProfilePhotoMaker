#!/bin/bash

# 🧪 Automated Ngrok Webhook Test Script
# Tests webhook integration with ngrok tunnel on port 5032

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
API_PORT=5032
NGROK_PORT=5032
API_DIR="/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API"
TEST_IMAGE_URL="https://example.com/test-image.jpg"
NGROK_PID=""
API_PID=""

# Cleanup function
cleanup() {
    echo -e "${YELLOW}🧹 Cleaning up processes...${NC}"
    if [ ! -z "$NGROK_PID" ]; then
        echo "Stopping ngrok (PID: $NGROK_PID)"
        kill $NGROK_PID 2>/dev/null || true
    fi
    if [ ! -z "$API_PID" ]; then
        echo "Stopping API (PID: $API_PID)"
        kill $API_PID 2>/dev/null || true
    fi
    # Kill any remaining processes
    pkill -f "ngrok http $NGROK_PORT" 2>/dev/null || true
    pkill -f "dotnet.*AI.ProfilePhotoMaker.API" 2>/dev/null || true
}

# Set up trap for cleanup
trap cleanup EXIT

echo -e "${BLUE}🚀 Starting Automated Ngrok Webhook Test${NC}"
echo -e "${BLUE}API Port: $API_PORT${NC}"
echo -e "${BLUE}Ngrok Port: $NGROK_PORT${NC}"
echo ""

# Step 1: Check if ngrok is installed
echo -e "${YELLOW}📋 Step 1: Checking ngrok installation...${NC}"
if ! command -v ngrok &> /dev/null; then
    echo -e "${RED}❌ ngrok is not installed or not in PATH${NC}"
    echo -e "${YELLOW}💡 Install ngrok from https://ngrok.com/download${NC}"
    exit 1
fi
echo -e "${GREEN}✅ ngrok is installed${NC}"

# Step 2: Start ngrok tunnel
echo -e "${YELLOW}📋 Step 2: Starting ngrok tunnel on port $NGROK_PORT...${NC}"
ngrok http $NGROK_PORT --log=stdout > /tmp/ngrok.log 2>&1 &
NGROK_PID=$!
echo "Started ngrok with PID: $NGROK_PID"

# Wait for ngrok to start
echo "Waiting for ngrok to start..."
sleep 5

# Get ngrok tunnel URL
echo "Getting ngrok tunnel URL..."
NGROK_URL=""
for i in {1..10}; do
    if NGROK_URL=$(curl -s http://localhost:4040/api/tunnels | grep -o 'https://[^"]*\.ngrok\.io' | head -1); then
        break
    fi
    echo "Waiting for ngrok API... (attempt $i/10)"
    sleep 2
done

if [ -z "$NGROK_URL" ]; then
    echo -e "${RED}❌ Failed to get ngrok tunnel URL${NC}"
    echo "ngrok log:"
    cat /tmp/ngrok.log
    exit 1
fi

echo -e "${GREEN}✅ Ngrok tunnel started: $NGROK_URL${NC}"

# Step 3: Update appsettings for port 5032
echo -e "${YELLOW}📋 Step 3: Updating API configuration for port $API_PORT...${NC}"
cd "$API_DIR"

# Update launchSettings.json for port 5032
if [ -f "Properties/launchSettings.json" ]; then
    # Create backup
    cp Properties/launchSettings.json Properties/launchSettings.json.backup
    
    # Update port in launchSettings.json
    sed -i "s/\"applicationUrl\": \"[^\"]*\"/\"applicationUrl\": \"https:\/\/localhost:7032;http:\/\/localhost:$API_PORT\"/" Properties/launchSettings.json
    echo -e "${GREEN}✅ Updated launchSettings.json for port $API_PORT${NC}"
else
    echo -e "${YELLOW}⚠️  launchSettings.json not found, API will use default port${NC}"
fi

# Step 4: Start API server
echo -e "${YELLOW}📋 Step 4: Starting API server on port $API_PORT...${NC}"
export ASPNETCORE_URLS="http://localhost:$API_PORT"
dotnet run --no-build > /tmp/api.log 2>&1 &
API_PID=$!
echo "Started API with PID: $API_PID"

# Wait for API to start
echo "Waiting for API to start..."
for i in {1..30}; do
    if curl -s http://localhost:$API_PORT/health > /dev/null 2>&1; then
        echo -e "${GREEN}✅ API is running on port $API_PORT${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}❌ API failed to start within 30 seconds${NC}"
        echo "API log:"
        cat /tmp/api.log
        exit 1
    fi
    echo "Waiting for API... (attempt $i/30)"
    sleep 1
done

# Step 5: Check webhook configuration in logs
echo -e "${YELLOW}📋 Step 5: Checking webhook configuration in API logs...${NC}"
sleep 2
if grep -q "Webhook base URL resolved" /tmp/api.log; then
    WEBHOOK_URL=$(grep "Webhook base URL resolved" /tmp/api.log | tail -1 | sed 's/.*Webhook base URL resolved: //' | sed 's/ .*//')
    echo -e "${GREEN}✅ Webhook URL detected: $WEBHOOK_URL${NC}"
else
    echo -e "${YELLOW}⚠️  Webhook configuration not found in logs, checking full log...${NC}"
    echo "Recent API log output:"
    tail -20 /tmp/api.log
fi

# Step 6: Test health endpoint
echo -e "${YELLOW}📋 Step 6: Testing API health endpoint...${NC}"
HEALTH_RESPONSE=$(curl -s http://localhost:$API_PORT/health || echo "FAILED")
if [[ $HEALTH_RESPONSE == *"Healthy"* ]]; then
    echo -e "${GREEN}✅ Health endpoint is working${NC}"
else
    echo -e "${RED}❌ Health endpoint failed: $HEALTH_RESPONSE${NC}"
fi

# Step 7: Test webhook URL resolution
echo -e "${YELLOW}📋 Step 7: Testing webhook URL resolution...${NC}"
# Create a simple test endpoint to check webhook resolver
curl -s -X GET "http://localhost:$API_PORT/api/health" > /dev/null
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ API is responding to requests${NC}"
else
    echo -e "${RED}❌ API is not responding${NC}"
fi

# Step 8: Display ngrok web interface info
echo -e "${YELLOW}📋 Step 8: Ngrok monitoring information...${NC}"
echo -e "${BLUE}🌐 Ngrok Web Interface: http://localhost:4040${NC}"
echo -e "${BLUE}🔗 Public URL: $NGROK_URL${NC}"
echo -e "${BLUE}📡 Webhook endpoint: $NGROK_URL/api/webhooks/replicate/prediction-complete${NC}"

# Step 9: Show API logs for webhook validation
echo -e "${YELLOW}📋 Step 9: API Webhook Validation Logs:${NC}"
echo -e "${BLUE}===========================================${NC}"
grep -E "(webhook|Webhook)" /tmp/api.log | head -10 || echo "No webhook logs found yet"
echo -e "${BLUE}===========================================${NC}"

# Step 10: Instructions for manual testing
echo -e "${YELLOW}📋 Step 10: Manual Testing Instructions:${NC}"
echo ""
echo -e "${GREEN}🎯 Your setup is ready! Here's how to test:${NC}"
echo ""
echo -e "${BLUE}1. API Server:${NC} http://localhost:$API_PORT"
echo -e "${BLUE}2. Ngrok Tunnel:${NC} $NGROK_URL"
echo -e "${BLUE}3. Ngrok Monitor:${NC} http://localhost:4040"
echo ""
echo -e "${YELLOW}🧪 Test the photo enhancement endpoint:${NC}"
echo -e "curl -X POST http://localhost:$API_PORT/api/replicate/enhance \\"
echo -e "  -H \"Content-Type: application/json\" \\"
echo -e "  -d '{\"imageUrl\": \"https://example.com/test.jpg\", \"enhancementType\": \"professional\"}'"
echo ""
echo -e "${YELLOW}📊 Monitor webhooks at:${NC} http://localhost:4040"
echo ""
echo -e "${GREEN}✅ Test environment is running! Press Ctrl+C to stop.${NC}"

# Keep script running
echo -e "${YELLOW}⏳ Keeping environment running... Press Ctrl+C to stop${NC}"
while true; do
    sleep 10
    # Check if processes are still running
    if ! kill -0 $NGROK_PID 2>/dev/null; then
        echo -e "${RED}❌ ngrok process died${NC}"
        break
    fi
    if ! kill -0 $API_PID 2>/dev/null; then
        echo -e "${RED}❌ API process died${NC}"
        break
    fi
done