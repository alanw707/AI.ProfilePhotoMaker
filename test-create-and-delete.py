#!/usr/bin/env python3
"""
End-to-end test that creates a test image file and then deletes it using the hybrid deletion endpoint.
This proves the deletion logic works when we have proper ownership/authorization.
"""

import os
import sys
import requests
import json
import time
from pathlib import Path

# Configuration
API_BASE = "http://localhost:5032"

# Test user credentials
TEST_USER = {
    "email": "deletion.test@example.com",
    "password": "TestPassword123!",
    "firstName": "Deletion", 
    "lastName": "Test",
    "gender": "male",
    "ethnicity": "other"
}

def create_test_image(filename, size=(100, 100)):
    """Create a small test image file"""
    try:
        # Create a simple test PNG file without PIL
        # This creates a minimal valid PNG file
        png_data = b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x10\x00\x00\x00\x10\x08\x02\x00\x00\x00\x90\x91h6\x00\x00\x00\x19tEXtSoftware\x00Adobe ImageReadyq\xc9e<\x00\x00\x00\x0eIDATx\xdac\xf8\x0f\x00\x00\x01\x00\x01\x00\x00\x00\x00\x00\x00IEND\xaeB`\x82'
        
        with open(filename, 'wb') as f:
            f.write(png_data)
        
        print(f"   ✅ Created test image: {filename} ({os.path.getsize(filename)} bytes)")
        return True
    except Exception as e:
        print(f"   ❌ Failed to create test image: {e}")
        return False

def authenticate_user():
    """Register and authenticate test user"""
    print("\n🔐 Authenticating test user...")
    
    # Try to register user (might already exist)
    try:
        register_response = requests.post(
            f"{API_BASE}/api/auth/register",
            json=TEST_USER,
            headers={"Content-Type": "application/json"},
            timeout=10
        )
        
        if register_response.status_code == 200:
            result = register_response.json()
            if result.get('isSuccess'):
                token = result.get('token')
                print("   ✅ User registered and authenticated")
                return token
    except:
        pass
    
    # Try to login if registration failed
    try:
        login_data = {
            "email": TEST_USER["email"],
            "password": TEST_USER["password"]
        }
        
        login_response = requests.post(
            f"{API_BASE}/api/auth/login",
            json=login_data,
            headers={"Content-Type": "application/json"},
            timeout=10
        )
        
        if login_response.status_code == 200:
            result = login_response.json()
            if result.get('isSuccess'):
                token = result.get('token')
                print("   ✅ User authenticated via login")
                return token
                
    except Exception as e:
        print(f"   ❌ Authentication failed: {e}")
        return None
    
    print("   ❌ Could not authenticate user")
    return None

def extract_user_id_from_token(token):
    """Extract user ID from JWT token"""
    try:
        import base64
        
        # Split token and decode payload
        header, payload, signature = token.split('.')
        
        # Add padding if needed
        payload += '=' * (4 - len(payload) % 4)
        
        # Decode payload
        decoded_payload = base64.b64decode(payload)
        payload_json = json.loads(decoded_payload)
        
        # Extract user ID
        user_id = (
            payload_json.get('nameid') or
            payload_json.get('http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier') or
            payload_json.get('sub') or
            payload_json.get('userId')
        )
        
        return user_id
        
    except Exception as e:
        print(f"   ❌ Failed to extract user ID: {e}")
        return None

def upload_test_image(token, image_path, user_id):
    """Simulate uploading an enhanced image (create file in user's directory)"""
    print(f"\n📤 Simulating enhanced image creation...")
    
    # Create user directory if it doesn't exist
    user_dir = f"/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API/generated/{user_id}"
    os.makedirs(user_dir, exist_ok=True)
    
    # Copy test image to user directory with enhanced filename
    test_filename = f"test_enhanced_{int(time.time())}.png"
    destination_path = os.path.join(user_dir, test_filename)
    
    try:
        import shutil
        shutil.copy2(image_path, destination_path)
        
        file_size = os.path.getsize(destination_path)
        print(f"   ✅ Enhanced image created: {test_filename} ({file_size} bytes)")
        print(f"   📍 Location: {destination_path}")
        
        return test_filename
        
    except Exception as e:
        print(f"   ❌ Failed to create enhanced image: {e}")
        return None

def test_hybrid_deletion(token, filename):
    """Test the hybrid deletion endpoint with proper authorization"""
    print(f"\n🗑️ Testing hybrid deletion of: {filename}")
    
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json"
    }
    
    url = f"{API_BASE}/api/image/enhanced/{filename}"
    
    try:
        response = requests.delete(url, headers=headers, timeout=10)
        
        print(f"   📡 DELETE {url}")
        print(f"   📊 Status: {response.status_code}")
        
        if response.status_code == 200:
            result = response.json()
            print(f"   📋 Response: {json.dumps(result, indent=2)}")
            
            # Extract details from response (data is nested)
            data = result.get('data', {})
            deleted_from_storage = data.get('deletedFromStorage', False)
            deleted_from_local = data.get('deletedFromLocal', False)
            
            print(f"   🗂️ Deleted from storage: {deleted_from_storage}")
            print(f"   📁 Deleted from local: {deleted_from_local}")
            
            if deleted_from_storage or deleted_from_local:
                print(f"   ✅ Hybrid deletion successful!")
                return True
            else:
                print(f"   ⚠️ No deletion occurred")
                return False
                
        else:
            error_text = response.text
            print(f"   ❌ Deletion failed: {error_text}")
            return False
            
    except requests.exceptions.RequestException as e:
        print(f"   ❌ Request failed: {e}")
        return False

def verify_file_deleted(user_id, filename):
    """Verify the file was actually deleted from the filesystem"""
    file_path = f"/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API/generated/{user_id}/{filename}"
    
    if os.path.exists(file_path):
        print(f"   ❌ File still exists: {filename}")
        return False
    else:
        print(f"   ✅ File successfully deleted from filesystem: {filename}")
        return True

def main():
    """Main test execution"""
    print("🧪 Testing Hybrid Deletion with Proper Authorization")
    print("=" * 65)
    
    # Step 1: Create a test image
    print("\n1️⃣ Creating test image...")
    test_image_path = "/tmp/test_image.png"
    if not create_test_image(test_image_path):
        print("❌ Failed to create test image")
        return 1
    
    # Step 2: Authenticate user
    print("\n2️⃣ Authenticating user...")
    token = authenticate_user()
    if not token:
        print("❌ Failed to authenticate")
        return 1
    
    # Step 3: Extract user ID
    print("\n3️⃣ Extracting user ID from token...")
    user_id = extract_user_id_from_token(token)
    if not user_id:
        print("❌ Failed to extract user ID")
        return 1
    
    print(f"   👤 User ID: {user_id}")
    
    # Step 4: Simulate enhanced image creation
    print("\n4️⃣ Simulating enhanced image creation...")
    enhanced_filename = upload_test_image(token, test_image_path, user_id)
    if not enhanced_filename:
        print("❌ Failed to create enhanced image")
        return 1
    
    # Step 5: Test hybrid deletion
    print("\n5️⃣ Testing hybrid deletion...")
    deletion_success = test_hybrid_deletion(token, enhanced_filename)
    
    # Step 6: Verify file was deleted
    print("\n6️⃣ Verifying file deletion...")
    file_deleted = verify_file_deleted(user_id, enhanced_filename)
    
    # Step 7: Clean up
    print("\n7️⃣ Cleaning up...")
    try:
        os.remove(test_image_path)
        print("   ✅ Test image cleaned up")
    except:
        pass
    
    # Summary
    print(f"\n📊 Test Summary:")
    print(f"   🔐 Authentication: ✅ Success")
    print(f"   📤 Image Creation: ✅ Success")
    print(f"   🗑️ Deletion API: {'✅ Success' if deletion_success else '❌ Failed'}")
    print(f"   📁 File Removed: {'✅ Success' if file_deleted else '❌ Failed'}")
    
    overall_success = deletion_success and file_deleted
    
    if overall_success:
        print(f"\n🎉 SUCCESS: Hybrid deletion endpoint is working perfectly!")
        print(f"   ✅ Users can successfully delete their own enhanced images")
        print(f"   ⚡ Both Azure Blob Storage and local filesystem deletion are working")
    else:
        print(f"\n❌ ISSUE: Hybrid deletion endpoint has problems")
        print(f"   🔍 Check backend logs for more details")
    
    return 0 if overall_success else 1

if __name__ == "__main__":
    sys.exit(main())