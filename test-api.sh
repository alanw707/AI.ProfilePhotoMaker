#!/bin/bash

# AI.ProfilePhotoMaker API Test Script
# Make sure the API is running on localhost:5035

API_BASE="http://localhost:5035/api"
EMAIL="test@example.com"
PASSWORD="TestPassword123!"

echo "=== AI.ProfilePhotoMaker API Test Workflow ==="
echo

# 1. Register User
echo "1. Registering user..."
REGISTER_RESPONSE=$(curl -s -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\",\"firstName\":\"John\",\"lastName\":\"Doe\"}")

echo "Register Response: $REGISTER_RESPONSE"
echo

# 2. Login
echo "2. Logging in..."
LOGIN_RESPONSE=$(curl -s -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")

echo "Login Response: $LOGIN_RESPONSE"

# Extract token (requires jq for JSON parsing)
if command -v jq &> /dev/null; then
    TOKEN=$(echo $LOGIN_RESPONSE | jq -r '.token // empty')
    if [ -z "$TOKEN" ]; then
        echo "Failed to extract token"
        exit 1
    fi
    echo "Token extracted: ${TOKEN:0:20}..."
else
    echo "Note: Install 'jq' to automatically extract token"
    echo "Manually copy the token from the login response above"
    read -p "Enter the token: " TOKEN
fi
echo

# 3. Check styles
echo "3. Getting available styles..."
STYLES_RESPONSE=$(curl -s -X GET "$API_BASE/profile/styles")
echo "Styles: $STYLES_RESPONSE"
echo

# 4. Try to get profile (should fail)
echo "4. Trying to get profile (should be 404)..."
PROFILE_RESPONSE=$(curl -s -X GET "$API_BASE/profile" \
  -H "Authorization: Bearer $TOKEN")
echo "Profile Response: $PROFILE_RESPONSE"
echo

# 5. Create profile
echo "5. Creating profile..."
CREATE_PROFILE_RESPONSE=$(curl -s -X POST "$API_BASE/profile" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"firstName":"John","lastName":"Doe","gender":"Male","ethnicity":"Caucasian"}')
echo "Create Profile Response: $CREATE_PROFILE_RESPONSE"
echo

# 6. Get profile
echo "6. Getting profile..."
GET_PROFILE_RESPONSE=$(curl -s -X GET "$API_BASE/profile" \
  -H "Authorization: Bearer $TOKEN")
echo "Get Profile Response: $GET_PROFILE_RESPONSE"
echo

# 7. Update profile
echo "7. Updating profile..."
UPDATE_PROFILE_RESPONSE=$(curl -s -X PUT "$API_BASE/profile" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"firstName":"John","lastName":"Smith","gender":"Male","ethnicity":"Caucasian"}')
echo "Update Profile Response: $UPDATE_PROFILE_RESPONSE"
echo

# 8. Test file upload (requires test images)
echo "8. Testing file upload..."
if [ -f "test-image.jpg" ]; then
    UPLOAD_RESPONSE=$(curl -s -X POST "$API_BASE/profile/upload" \
      -H "Authorization: Bearer $TOKEN" \
      -F "images=@test-image.jpg")
    echo "Upload Response: $UPLOAD_RESPONSE"
else
    echo "Skipping upload test - no test-image.jpg found"
    echo "To test uploads, create a test-image.jpg file and run this script again"
fi
echo

# 9. Get images
echo "9. Getting user images..."
IMAGES_RESPONSE=$(curl -s -X GET "$API_BASE/profile/images" \
  -H "Authorization: Bearer $TOKEN")
echo "Images Response: $IMAGES_RESPONSE"
echo

echo "=== Test Complete ==="
echo "Next steps:"
echo "1. Try the Swagger UI at: http://localhost:5035/swagger"
echo "2. Upload real images using a tool like Postman"
echo "3. Configure Replicate.com API keys to test image generation"