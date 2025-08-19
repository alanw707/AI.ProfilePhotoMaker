import { test, expect } from '@playwright/test';
import { readdir, stat } from 'fs/promises';
import { join } from 'path';

/**
 * End-to-end test for hybrid deletion endpoint
 * Tests the newly implemented hybrid deletion approach that handles both
 * Azure Blob Storage and local filesystem deletion
 */

const API_BASE = 'http://localhost:5032';
const APP_BASE = 'http://localhost:4200'; 
const USER_ID = 'b99678bd-cb87-40c1-a7bf-b889f1e00c08';
const GENERATED_PATH = '/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API/generated/' + USER_ID;

test.describe('Enhanced Image Hybrid Deletion', () => {
  let authToken: string;
  let initialFileCount: number;
  let testFiles: string[] = [];

  test.beforeAll(async ({ request }) => {
    // Step 1: Count initial leftover files
    try {
      const files = await readdir(GENERATED_PATH);
      const imageFiles = files.filter(f => f.match(/\.(png|jpg|jpeg)$/i));
      initialFileCount = imageFiles.length;
      testFiles = imageFiles.slice(0, 5); // Test first 5 files
      
      console.log(`📁 Found ${initialFileCount} leftover files`);
      console.log(`🧪 Will test deletion of: ${testFiles.join(', ')}`);
    } catch (error) {
      console.log(`❌ Could not read generated directory: ${error}`);
      initialFileCount = 0;
    }
  });

  test('should authenticate and obtain JWT token', async ({ page }) => {
    // Navigate to the app
    await page.goto(APP_BASE);
    
    // Try to get token from localStorage or authenticate
    // This might need adjustment based on actual auth flow
    authToken = await page.evaluate(() => {
      return localStorage.getItem('authToken') || localStorage.getItem('token') || localStorage.getItem('jwt');
    });

    if (!authToken) {
      // If no token in localStorage, try to authenticate
      // This part depends on your actual authentication flow
      console.log('⚠️ No auth token found in localStorage');
      console.log('💡 You may need to manually authenticate first');
      
      // For testing purposes, we'll try without auth and expect 401
      // The test will demonstrate the endpoint structure is correct
    }

    console.log(`🔑 Auth token: ${authToken ? 'Found' : 'Not found'}`);
  });

  test('should test hybrid deletion endpoint directly', async ({ request }) => {
    console.log(`\n🧪 Testing hybrid deletion endpoint for ${testFiles.length} files...`);
    
    for (const fileName of testFiles) {
      console.log(`\n🗑️ Testing deletion of: ${fileName}`);
      
      // Check if file exists before deletion
      const filePath = join(GENERATED_PATH, fileName);
      let fileExistsBefore = false;
      try {
        await stat(filePath);
        fileExistsBefore = true;
        console.log(`   ✅ File exists before deletion`);
      } catch {
        console.log(`   ⚠️ File does not exist: ${fileName}`);
        continue;
      }

      // Make DELETE request to hybrid deletion endpoint
      const response = await request.delete(`${API_BASE}/api/image/enhanced/${fileName}`, {
        headers: authToken ? {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        } : {}
      });

      console.log(`   📡 API Response: ${response.status()}`);
      
      if (response.ok()) {
        const result = await response.json();
        console.log(`   📋 Response body:`, result);
        
        // Verify file was actually deleted
        let fileExistsAfter = false;
        try {
          await stat(filePath);
          fileExistsAfter = true;
        } catch {
          fileExistsAfter = false;
        }

        if (!fileExistsAfter) {
          console.log(`   ✅ File successfully deleted from filesystem`);
        } else {
          console.log(`   ❌ File still exists after deletion attempt`);
        }
      } else {
        const errorText = await response.text();
        console.log(`   ❌ Deletion failed: ${errorText}`);
        
        if (response.status() === 401) {
          console.log(`   🔑 Authentication required - this is expected without proper JWT token`);
        }
      }
    }
  });

  test('should verify overall file count reduction', async () => {
    console.log(`\n📊 Verifying file count changes...`);
    
    try {
      const files = await readdir(GENERATED_PATH);
      const imageFiles = files.filter(f => f.match(/\.(png|jpg|jpeg)$/i));
      const finalFileCount = imageFiles.length;
      const deletedCount = initialFileCount - finalFileCount;
      
      console.log(`📈 Deletion Summary:`);
      console.log(`   🗂️ Initial files: ${initialFileCount}`);
      console.log(`   🗑️ Files deleted: ${deletedCount}`);  
      console.log(`   📁 Remaining files: ${finalFileCount}`);
      
      if (deletedCount > 0) {
        console.log(`✅ Hybrid deletion endpoint successfully deleted ${deletedCount} files`);
      } else {
        console.log(`⚠️ No files were deleted - check authentication or endpoint logic`);
      }
      
    } catch (error) {
      console.log(`❌ Could not verify final file count: ${error}`);
    }
  });

  test('should test authentication-free direct API call', async ({ request }) => {
    console.log(`\n🔓 Testing endpoint structure without authentication...`);
    
    if (testFiles.length > 0) {
      const fileName = testFiles[0];
      
      // Test the endpoint structure
      const response = await request.delete(`${API_BASE}/api/image/enhanced/${fileName}`);
      
      console.log(`📡 Response status: ${response.status()}`);
      
      if (response.status() === 401) {
        console.log(`✅ Endpoint correctly requires authentication`);
      } else if (response.status() === 404) {
        console.log(`⚠️ Endpoint not found - check route configuration`);
      } else {
        console.log(`📋 Unexpected response: ${await response.text()}`);
      }
    }
  });
});