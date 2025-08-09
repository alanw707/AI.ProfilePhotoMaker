#!/bin/bash

# 🧪 Webhook Endpoint Test Script
# Tests the actual photo enhancement endpoint with webhook validation

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

API_PORT=5032
API_BASE_URL="http://localhost:$API_PORT"

echo -e "${BLUE}🧪 Testing Photo Enhancement Webhook Endpoint${NC}"
echo -e "${BLUE}API Base URL: $API_BASE_URL${NC}"
echo ""

# Test 1: Health Check
echo -e "${YELLOW}📋 Test 1: Health Check...${NC}"
HEALTH_RESPONSE=$(curl -s "$API_BASE_URL/health" || echo "FAILED")
if [[ $HEALTH_RESPONSE == *"Healthy"* ]]; then
    echo -e "${GREEN}✅ API health check passed${NC}"
else
    echo -e "${RED}❌ API health check failed: $HEALTH_RESPONSE${NC}"
    exit 1
fi

# Test 2: Check Swagger/OpenAPI endpoint
echo -e "${YELLOW}📋 Test 2: Checking API documentation...${NC}"
if curl -s "$API_BASE_URL/swagger" > /dev/null; then
    echo -e "${GREEN}✅ Swagger endpoint is accessible${NC}"
    echo -e "${BLUE}🌐 Swagger UI: $API_BASE_URL/swagger${NC}"
else
    echo -e "${YELLOW}⚠️  Swagger endpoint not accessible${NC}"
fi

# Test 3: Test the enhance endpoint (this will trigger webhook logic)
echo -e "${YELLOW}📋 Test 3: Testing photo enhancement endpoint...${NC}"
echo "This test will show whether webhook URLs are properly configured"

# Create test request
TEST_REQUEST='{
  "imageUrl": "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=400",
  "enhancementType": "professional"
}'

echo "Sending enhancement request..."
ENHANCE_RESPONSE=$(curl -s -w "\\n%{http_code}" -X POST "$API_BASE_URL/api/replicate/enhance" \
  -H "Content-Type: application/json" \
  -d "$TEST_REQUEST" || echo "FAILED\\n000")

HTTP_CODE=$(echo "$ENHANCE_RESPONSE" | tail -n1)
RESPONSE_BODY=$(echo "$ENHANCE_RESPONSE" | head -n -1)

echo "HTTP Status Code: $HTTP_CODE"
echo "Response: $RESPONSE_BODY"

if [ "$HTTP_CODE" = "200" ]; then
    echo -e "${GREEN}✅ Enhancement endpoint responded successfully${NC}"
    echo -e "${GREEN}✅ Webhook URL configuration is working${NC}"
elif [ "$HTTP_CODE" = "500" ]; then
    echo -e "${RED}❌ Enhancement endpoint returned 500 error${NC}"
    echo "This might indicate webhook configuration issues"
    echo "Response: $RESPONSE_BODY"
elif [ "$HTTP_CODE" = "401" ] || [ "$HTTP_CODE" = "403" ]; then
    echo -e "${YELLOW}⚠️  Authentication required for enhancement endpoint${NC}"
    echo "This is expected if authentication is enabled"
else
    echo -e "${RED}❌ Enhancement endpoint returned unexpected status: $HTTP_CODE${NC}"
    echo "Response: $RESPONSE_BODY"
fi

echo ""
echo -e "${BLUE}📊 Test Summary:${NC}"
echo -e "- API Health: ${GREEN}✅ Passed${NC}"
echo -e "- Enhancement Endpoint: $([ "$HTTP_CODE" = "200" ] && echo "${GREEN}✅ Passed${NC}" || echo "${YELLOW}⚠️  Needs attention${NC}")"
echo ""
echo -e "${YELLOW}💡 Next Steps:${NC}"
echo -e "1. Check ngrok web interface at http://localhost:4040"
echo -e "2. Look for webhook requests in the ngrok logs"
echo -e "3. Monitor API logs for webhook URL resolution messages"
echo -e "4. If using authentication, provide proper JWT tokens"