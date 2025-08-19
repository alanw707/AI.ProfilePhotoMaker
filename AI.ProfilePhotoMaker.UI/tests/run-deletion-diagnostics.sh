#!/bin/bash

# Enhanced Image Deletion Diagnostics Runner
# Comprehensive troubleshooting with iterative testing

echo "🔍 Starting Enhanced Image Deletion Diagnostics..."
echo "=" $(printf '=%.0s' {1..60})

# Function to check if service is running
check_service() {
    local port=$1
    local service_name=$2
    if curl -s "http://localhost:$port" > /dev/null 2>&1; then
        echo "✅ $service_name is running on port $port"
        return 0
    else
        echo "❌ $service_name is not running on port $port"
        return 1
    fi
}

# Function to start Angular dev server
start_frontend() {
    echo "🚀 Starting Angular development server..."
    cd /home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.UI
    
    # Clear any cached builds
    echo "🧹 Clearing cache and rebuilding..."
    rm -rf dist/
    rm -rf .angular/cache/
    npm run build > /dev/null 2>&1
    
    # Start dev server in background
    npm start &
    FRONTEND_PID=$!
    
    # Wait for frontend to be ready
    echo "⏳ Waiting for frontend to start..."
    for i in {1..30}; do
        if check_service 4200 "Angular Dev Server"; then
            break
        fi
        sleep 2
        if [ $i -eq 30 ]; then
            echo "❌ Frontend failed to start after 60 seconds"
            kill $FRONTEND_PID 2>/dev/null
            exit 1
        fi
    done
}

# Function to verify file-upload.service.ts changes
verify_frontend_changes() {
    echo "🔍 Verifying frontend changes are active..."
    
    local service_file="/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.UI/src/app/services/file-upload.service.ts"
    
    if grep -q "getAuthToken()" "$service_file"; then
        echo "✅ Authentication fix is present in file-upload.service.ts"
    else
        echo "❌ Authentication fix NOT found in file-upload.service.ts"
        echo "🔧 Applying authentication fix..."
        
        # Apply the fix if missing
        sed -i 's/headers: {/headers: {\n      '\''Authorization'\'': this.authService.getToken() || '\''\'',/' "$service_file"
    fi
    
    # Check the actual implementation
    echo "📄 Current deleteEnhancedImage implementation:"
    grep -A 15 "deleteEnhancedImage" "$service_file" | head -20
}

# Function to run diagnostic tests
run_diagnostics() {
    echo "🧪 Running comprehensive diagnostic tests..."
    
    cd /home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.UI
    
    # Run with multiple iterations for comprehensive testing
    echo "🔄 Running diagnostic test suite with --loop mode simulation..."
    
    # Test 1: Basic authentication verification
    echo -e "\n🔍 Phase 1: Authentication State Verification"
    npx playwright test enhanced-image-deletion-diagnostics.spec.ts -g "Authentication State Verification" --reporter=line
    
    # Test 2: Frontend build verification
    echo -e "\n🔍 Phase 2: Frontend Build Verification"
    npx playwright test enhanced-image-deletion-diagnostics.spec.ts -g "Frontend Build Verification" --reporter=line
    
    # Test 3: Network analysis
    echo -e "\n🔍 Phase 3: Network Request Analysis"
    npx playwright test enhanced-image-deletion-diagnostics.spec.ts -g "Network Request Analysis" --reporter=line
    
    # Test 4: Real user flow
    echo -e "\n🔍 Phase 4: Real User Flow Testing"
    npx playwright test enhanced-image-deletion-diagnostics.spec.ts -g "Real User Flow Testing" --reporter=line
    
    # Test 5: Iterative testing
    echo -e "\n🔍 Phase 5: Iterative Testing with Different States"
    npx playwright test enhanced-image-deletion-diagnostics.spec.ts -g "Iterative Testing" --reporter=line
    
    echo -e "\n📊 All diagnostic phases completed!"
}

# Function to analyze current file-upload.service.ts
analyze_current_implementation() {
    echo "🔍 Analyzing current file-upload.service.ts implementation..."
    
    local service_file="/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.UI/src/app/services/file-upload.service.ts"
    
    echo -e "\n📄 Full deleteEnhancedImage method:"
    echo "=" $(printf '=%.0s' {1..50})
    
    # Extract the full method
    awk '/deleteEnhancedImage\(.*\) {/,/^  }/' "$service_file"
    
    echo -e "\n🔍 Authentication-related code:"
    echo "=" $(printf '=%.0s' {1..30})
    grep -n -A 3 -B 3 "Authorization\|getToken\|authService" "$service_file" || echo "No authentication code found"
    
    echo -e "\n🔍 HTTP headers setup:"
    echo "=" $(printf '=%.0s' {1..30})
    grep -n -A 5 -B 2 "headers:" "$service_file" || echo "No headers setup found"
}

# Function to cleanup
cleanup() {
    echo -e "\n🧹 Cleaning up..."
    if [ ! -z "$FRONTEND_PID" ]; then
        kill $FRONTEND_PID 2>/dev/null
        echo "✅ Frontend server stopped"
    fi
}

# Set up cleanup trap
trap cleanup EXIT

# Main execution
echo "🎯 Phase 1: Service Status Check"
echo "=" $(printf '=%.0s' {1..40})

# Check if API is running
if ! check_service 5032 "API Server"; then
    echo "⚠️ API Server not running - some tests may fail"
    echo "💡 Start API with: cd /home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API && dotnet run --urls=http://0.0.0.0:5032"
fi

echo -e "\n🎯 Phase 2: Implementation Analysis"
echo "=" $(printf '=%.0s' {1..40})
analyze_current_implementation

echo -e "\n🎯 Phase 3: Frontend Verification"
echo "=" $(printf '=%.0s' {1..40})
verify_frontend_changes

echo -e "\n🎯 Phase 4: Frontend Server Setup"
echo "=" $(printf '=%.0s' {1..40})

# Check if frontend is already running
if check_service 4200 "Angular Dev Server"; then
    echo "✅ Frontend already running, proceeding with tests"
else
    start_frontend
fi

echo -e "\n🎯 Phase 5: Diagnostic Testing"
echo "=" $(printf '=%.0s' {1..40})
run_diagnostics

echo -e "\n🎯 DIAGNOSTIC SUMMARY"
echo "=" $(printf '=%.0s' {1..60})
echo "✅ Comprehensive diagnostic testing completed"
echo "📊 Check the test output above for detailed analysis"
echo "🔍 Look for authentication state, network requests, and UI interactions"
echo "💡 Focus on any ❌ CRITICAL or ⚠️ WARNING messages in the diagnostic report"

echo -e "\n🔧 NEXT STEPS:"
echo "1. Review the diagnostic output for authentication issues"
echo "2. Check network request headers for Authorization presence"
echo "3. Verify token format and expiration"
echo "4. Test with a real authenticated session"
echo "5. Check browser console for JavaScript errors"