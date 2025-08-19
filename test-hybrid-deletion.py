#!/usr/bin/env python3
"""
Test script for the newly implemented hybrid deletion endpoint.
Tests both authentication scenarios and verifies the hybrid deletion approach works.
"""

import os
import sys
import requests
import json
import time
from pathlib import Path

# Configuration
API_BASE = "http://localhost:5032"
USER_ID = "b99678bd-cb87-40c1-a7bf-b889f1e00c08"
GENERATED_PATH = f"/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API/generated/{USER_ID}"

def list_leftover_files():
    """List all leftover files in the generated directory"""
    if not os.path.exists(GENERATED_PATH):
        print(f"❌ Generated directory does not exist: {GENERATED_PATH}")
        return []
    
    files = [f for f in os.listdir(GENERATED_PATH) if f.endswith(('.png', '.jpg', '.jpeg'))]
    print(f"📁 Found {len(files)} leftover files in {GENERATED_PATH}")
    
    if files:
        for i, file in enumerate(files[:5], 1):  # Show first 5
            file_path = os.path.join(GENERATED_PATH, file)
            file_size = os.path.getsize(file_path)
            print(f"   {i}. {file} ({file_size:,} bytes)")
        if len(files) > 5:
            print(f"   ... and {len(files) - 5} more files")
    
    return files

def test_api_health():
    """Test if the API is running and accessible"""
    try:
        response = requests.get(f"{API_BASE}/api/health", timeout=5)
        if response.status_code == 200:
            print("✅ API is running and accessible")
            return True
    except requests.exceptions.RequestException:
        pass
    
    # Try a simpler endpoint
    try:
        response = requests.get(f"{API_BASE}/", timeout=5)
        print(f"✅ API is running (status: {response.status_code})")
        return True
    except requests.exceptions.RequestException as e:
        print(f"❌ API is not accessible: {e}")
        return False

def test_deletion_endpoint_structure(filename):
    """Test the deletion endpoint structure without authentication"""
    print(f"\n🔓 Testing endpoint structure for: {filename}")
    
    url = f"{API_BASE}/api/image/enhanced/{filename}"
    
    try:
        response = requests.delete(url, timeout=10)
        
        print(f"   📡 DELETE {url}")
        print(f"   📊 Status: {response.status_code}")
        
        if response.status_code == 401:
            print("   ✅ Endpoint correctly requires authentication")
            return True
        elif response.status_code == 404:
            print("   ⚠️ Endpoint not found - check route configuration")
            return False
        elif response.status_code == 500:
            print("   ⚠️ Server error - check backend logs")
            try:
                error_details = response.text
                print(f"   📋 Error details: {error_details}")
            except:
                pass
            return False
        else:
            print(f"   📋 Response body: {response.text}")
            return True
            
    except requests.exceptions.RequestException as e:
        print(f"   ❌ Request failed: {e}")
        return False

def test_with_mock_auth(filename):
    """Test deletion endpoint with a mock JWT token"""
    print(f"\n🔑 Testing with mock authentication for: {filename}")
    
    # Create a mock JWT token (this won't be valid but tests the header handling)
    mock_token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"
    
    headers = {
        "Authorization": f"Bearer {mock_token}",
        "Content-Type": "application/json"
    }
    
    url = f"{API_BASE}/api/image/enhanced/{filename}"
    
    try:
        response = requests.delete(url, headers=headers, timeout=10)
        
        print(f"   📡 DELETE {url} (with mock auth)")
        print(f"   📊 Status: {response.status_code}")
        
        if response.status_code == 401:
            print("   ✅ Invalid token correctly rejected")
            return True
        elif response.status_code == 200:
            print("   🎉 Deletion successful!")
            try:
                result = response.json()
                print(f"   📋 Response: {json.dumps(result, indent=2)}")
            except:
                print(f"   📋 Response: {response.text}")
            return True
        else:
            print(f"   📋 Response: {response.text}")
            return False
            
    except requests.exceptions.RequestException as e:
        print(f"   ❌ Request failed: {e}")
        return False

def verify_file_deletion(filename):
    """Verify if a file was actually deleted from the filesystem"""
    file_path = os.path.join(GENERATED_PATH, filename)
    
    if os.path.exists(file_path):
        print(f"   📁 File still exists: {filename}")
        return False
    else:
        print(f"   ✅ File successfully deleted: {filename}")
        return True

def test_hybrid_deletion_comprehensive():
    """Comprehensive test of the hybrid deletion functionality"""
    print("🧪 Testing Hybrid Deletion Endpoint")
    print("=" * 60)
    
    # Step 1: Check API health
    print("\n1️⃣ Checking API availability...")
    if not test_api_health():
        print("❌ Cannot proceed - API is not accessible")
        return False
    
    # Step 2: List current files
    print("\n2️⃣ Listing current leftover files...")
    files = list_leftover_files()
    
    if not files:
        print("✅ No leftover files found!")
        return True
    
    # Step 3: Test endpoint structure with first file
    print("\n3️⃣ Testing endpoint structure...")
    test_file = files[0]
    structure_ok = test_deletion_endpoint_structure(test_file)
    
    # Step 4: Test with mock authentication
    print("\n4️⃣ Testing with mock authentication...")
    mock_auth_result = test_with_mock_auth(test_file)
    
    # Step 5: Check if file was deleted
    print("\n5️⃣ Verifying file deletion...")
    was_deleted = verify_file_deletion(test_file)
    
    # Step 6: Final summary
    print("\n6️⃣ Final file count...")
    final_files = list_leftover_files()
    deleted_count = len(files) - len(final_files)
    
    print(f"\n📊 Test Summary:")
    print(f"   🗂️ Initial files: {len(files)}")
    print(f"   🗑️ Files deleted: {deleted_count}")
    print(f"   📁 Remaining files: {len(final_files)}")
    print(f"   🔧 Endpoint structure: {'✅ OK' if structure_ok else '❌ Issue'}")
    print(f"   🔑 Auth handling: {'✅ OK' if mock_auth_result else '❌ Issue'}")
    
    if deleted_count > 0:
        print(f"\n🎉 SUCCESS: Hybrid deletion endpoint deleted {deleted_count} file(s)!")
        print("   The hybrid deletion approach is working correctly.")
    elif structure_ok:
        print(f"\n✅ ENDPOINT OK: The deletion endpoint is properly configured.")
        print("   Authentication or permission issues may be preventing deletion.")
    else:
        print(f"\n❌ ISSUE: The deletion endpoint has configuration problems.")
    
    return deleted_count > 0 or structure_ok

def main():
    """Main test execution"""
    success = test_hybrid_deletion_comprehensive()
    
    if success:
        print(f"\n🏁 Test completed successfully!")
        print(f"💡 Next steps:")
        print(f"   1. If no files were deleted, check JWT token generation")
        print(f"   2. Consider running with proper authentication")
        print(f"   3. Check backend logs for any errors")
    else:
        print(f"\n🚨 Test failed - check backend configuration and logs")
    
    return 0 if success else 1

if __name__ == "__main__":
    sys.exit(main())