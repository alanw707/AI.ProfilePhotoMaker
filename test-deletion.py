#!/usr/bin/env python3
"""
Direct deletion test script for leftover enhanced images.
This bypasses authentication to test the core deletion mechanism.
"""

import os
import sys
import requests
import json

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
    for i, file in enumerate(files[:5], 1):  # Show first 5
        print(f"   {i}. {file}")
    if len(files) > 5:
        print(f"   ... and {len(files) - 5} more files")
    return files

def test_direct_file_deletion(filename):
    """Test direct file system deletion"""
    filepath = os.path.join(GENERATED_PATH, filename)
    
    if not os.path.exists(filepath):
        print(f"❌ File does not exist: {filepath}")
        return False
    
    try:
        file_size = os.path.getsize(filepath)
        os.remove(filepath)
        print(f"✅ Successfully deleted {filename} ({file_size} bytes)")
        return True
    except Exception as e:
        print(f"❌ Failed to delete {filename}: {e}")
        return False

def test_storage_service_config():
    """Test which storage service is configured"""
    try:
        # Check if Azure Storage connection string is configured
        response = requests.get(f"{API_BASE}/api/debug/url-test", timeout=5)
        if response.status_code == 200:
            data = response.json()
            print("🔧 Storage service configuration:")
            print(f"   Environment: {data.get('data', {}).get('Environment', 'Unknown')}")
            return True
    except Exception as e:
        print(f"⚠️  Could not check storage configuration: {e}")
        return False

def main():
    print("🧪 Testing Enhanced Image Deletion")
    print("=" * 50)
    
    # Step 1: List leftover files
    print("\n1️⃣  Listing leftover files...")
    files = list_leftover_files()
    
    if not files:
        print("✅ No leftover files found!")
        return
    
    # Step 2: Check storage configuration
    print("\n2️⃣  Checking storage service configuration...")
    test_storage_service_config()
    
    # Step 3: Test direct deletion of first few files
    print("\n3️⃣  Testing direct file deletion...")
    test_files = files[:3]  # Test first 3 files
    
    for filename in test_files:
        print(f"\n🗑️  Testing deletion of: {filename}")
        success = test_direct_file_deletion(filename)
        
        if success:
            print(f"   ✅ File successfully deleted from filesystem")
        else:
            print(f"   ❌ File deletion failed")
    
    # Step 4: Recount remaining files
    print("\n4️⃣  Checking remaining files...")
    remaining_files = list_leftover_files()
    deleted_count = len(files) - len(remaining_files)
    
    print(f"\n📊 Summary:")
    print(f"   🗂️  Original files: {len(files)}")
    print(f"   🗑️  Deleted files: {deleted_count}")
    print(f"   📁 Remaining files: {len(remaining_files)}")
    
    if deleted_count > 0:
        print("\n✅ Direct file deletion works! The issue is likely in:")
        print("   1. Storage service configuration mismatch")
        print("   2. Authentication/authorization")
        print("   3. Frontend->Backend communication")
    else:
        print("\n❌ Direct file deletion failed - investigate filesystem permissions")

if __name__ == "__main__":
    main()