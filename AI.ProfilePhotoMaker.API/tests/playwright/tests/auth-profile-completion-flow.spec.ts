import { test, expect } from '@playwright/test';

test.describe('Authentication Profile Completion Flow', () => {
  const testConfig = {
    frontendUrl: 'http://localhost:4200',
    backendUrl: 'http://localhost:5032',
    // Test user credentials  
    testUser: {
      email: 'testuser@example.com',
      password: 'TestPassword123!',
      firstName: 'Test',
      lastName: 'User',
      gender: 'Male',
      ethnicity: 'South Asian'
    }
  };

  test.beforeEach(async ({ page }) => {
    // Set up any necessary test state
    await page.goto(testConfig.frontendUrl);
  });

  test('Email Registration Flow - Should collect Gender/Ethnicity during signup', async ({ page }) => {
    console.log('🧪 Testing email registration with complete profile data');
    
    // Navigate to registration page
    await page.goto(`${testConfig.frontendUrl}/auth/register`);
    await expect(page).toHaveTitle(/Sign Up/);

    // Fill out registration form with all required fields
    await page.fill('[data-testid="firstName"], input[name="firstName"], #firstName', testConfig.testUser.firstName);
    await page.fill('[data-testid="lastName"], input[name="lastName"], #lastName', testConfig.testUser.lastName);
    await page.fill('[data-testid="email"], input[name="email"], #email', testConfig.testUser.email);
    await page.fill('[data-testid="password"], input[name="password"], #password', testConfig.testUser.password);
    await page.fill('[data-testid="confirmPassword"], input[name="confirmPassword"], #confirmPassword', testConfig.testUser.password);
    
    // Select Gender
    await page.selectOption('[data-testid="gender"], select[name="gender"], #gender', testConfig.testUser.gender);
    
    // Select Ethnicity  
    await page.selectOption('[data-testid="ethnicity"], select[name="ethnicity"], #ethnicity', testConfig.testUser.ethnicity);

    // Submit registration
    await page.click('[data-testid="submit"], button[type="submit"], .submit-button');

    // Should redirect to Photo Workspace without profile completion step
    await expect(page).toHaveURL(/\/app\/app/enhance/, { timeout: 10000 });
    
    console.log('✅ Email registration completed successfully with full profile data');
  });

  test('OAuth Profile Completion Check - Should redirect incomplete profiles', async ({ page }) => {
    console.log('🧪 Testing OAuth profile completion redirect logic');

    // Mock OAuth user creation with incomplete profile
    // This simulates the backend creating a user with Gender=null, Ethnicity=null
    await page.route('**/api/auth/profile-completion-status', async (route) => {
      const mockResponse = {
        isCompleted: false,
        hasFirstName: true,
        hasLastName: true,
        hasGender: false,
        hasEthnicity: false
      };
      
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockResponse),
      });
    });

    // Mock authentication check to return authenticated
    await page.addInitScript(() => {
      // Mock localStorage with auth token
      localStorage.setItem('auth_token', 'mock-jwt-token');
      localStorage.setItem('currentUser', JSON.stringify({
        token: 'mock-jwt-token',
        email: 'oauth-user@example.com', 
        firstName: 'OAuth',
        lastName: 'User'
      }));
    });

    // Try to access dashboard directly (simulating OAuth callback)
    await page.goto(`${testConfig.frontendUrl}/app/enhance`);
    
    // Should redirect to profile completion page
    await expect(page).toHaveURL(/\/auth\/complete-profile/, { timeout: 10000 });
    
    // Verify profile completion form is displayed
    await expect(page.locator('h2')).toHaveText('Complete Profile');
    await expect(page.locator('#gender')).toBeVisible();
    await expect(page.locator('#ethnicity')).toBeVisible();
    
    console.log('✅ OAuth user correctly redirected to profile completion');
  });

  test('Profile Completion Form - Should complete OAuth user profile', async ({ page }) => {
    console.log('🧪 Testing profile completion form submission');

    // Mock profile completion API calls
    await page.route('**/api/auth/profile-completion-status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          isCompleted: false,
          hasFirstName: true,
          hasLastName: true,
          hasGender: false,
          hasEthnicity: false
        }),
      });
    });

    await page.route('**/api/auth/complete-profile', async (route) => {
      const requestBody = await route.request().postDataJSON();
      
      // Validate the request contains required fields
      expect(requestBody).toHaveProperty('firstName');
      expect(requestBody).toHaveProperty('lastName');
      expect(requestBody).toHaveProperty('gender');
      expect(requestBody).toHaveProperty('ethnicity');
      
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          message: 'Profile completed successfully'
        }),
      });
    });

    // Set up authenticated state
    await page.addInitScript(() => {
      localStorage.setItem('auth_token', 'mock-jwt-token');
      localStorage.setItem('currentUser', JSON.stringify({
        token: 'mock-jwt-token',
        email: 'oauth-user@example.com',
        firstName: 'OAuth',
        lastName: 'User'
      }));
    });

    // Navigate to profile completion page
    await page.goto(`${testConfig.frontendUrl}/auth/complete-profile`);
    
    // Verify form is pre-filled with existing data
    await expect(page.locator('#firstName')).toHaveValue('OAuth');
    await expect(page.locator('#lastName')).toHaveValue('User');
    
    // Fill out missing profile fields
    await page.selectOption('#gender', testConfig.testUser.gender);
    await page.selectOption('#ethnicity', testConfig.testUser.ethnicity);
    
    // Submit profile completion
    await page.click('button[type="submit"]');
    
    // Should redirect to Photo Workspace after completion
    await expect(page).toHaveURL(/\/app\/app/enhance/, { timeout: 10000 });
    
    console.log('✅ Profile completion form submitted successfully');
  });

  test('Form Validation - Should require all profile completion fields', async ({ page }) => {
    console.log('🧪 Testing profile completion form validation');

    // Navigate directly to profile completion page
    await page.goto(`${testConfig.frontendUrl}/auth/complete-profile`);
    
    // Try to submit form without filling required fields
    await page.click('button[type="submit"]');
    
    // Should show validation errors
    await expect(page.locator('text=First name is required')).toBeVisible({ timeout: 3000 });
    await expect(page.locator('text=Last name is required')).toBeVisible({ timeout: 3000 });
    await expect(page.locator('text=Gender is required')).toBeVisible({ timeout: 3000 });
    await expect(page.locator('text=Ethnicity is required')).toBeVisible({ timeout: 3000 });
    
    // Fill out all fields
    await page.fill('#firstName', testConfig.testUser.firstName);
    await page.fill('#lastName', testConfig.testUser.lastName);
    await page.selectOption('#gender', testConfig.testUser.gender);
    await page.selectOption('#ethnicity', testConfig.testUser.ethnicity);
    
    // Validation errors should disappear
    await expect(page.locator('text=First name is required')).not.toBeVisible();
    await expect(page.locator('text=Last name is required')).not.toBeVisible();
    
    console.log('✅ Form validation working correctly');
  });

  test('AppGuard Integration - Should allow access after profile completion', async ({ page }) => {
    console.log('🧪 Testing AppGuard allows access to complete profiles');

    // Mock complete profile status
    await page.route('**/api/auth/profile-completion-status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          isCompleted: true,
          hasFirstName: true,
          hasLastName: true,
          hasGender: true,
          hasEthnicity: true
        }),
      });
    });

    // Set up authenticated state
    await page.addInitScript(() => {
      localStorage.setItem('auth_token', 'mock-jwt-token');
      localStorage.setItem('currentUser', JSON.stringify({
        token: 'mock-jwt-token',
        email: 'complete-user@example.com',
        firstName: 'Complete',
        lastName: 'User'
      }));
    });

    // Try to access dashboard
    await page.goto(`${testConfig.frontendUrl}/app/enhance`);
    
    // Should NOT redirect to profile completion
    await expect(page).toHaveURL(/\/app\/app/enhance/);
    await expect(page).not.toHaveURL(/\/auth\/complete-profile/);
    
    // Try accessing other protected routes
    await page.goto(`${testConfig.frontendUrl}/app/gallery`);
    await expect(page).toHaveURL(/\/app\/gallery/);
    
    await page.goto(`${testConfig.frontendUrl}/app/settings`);
    await expect(page).toHaveURL(/\/app\/settings/);
    
    console.log('✅ Complete profiles can access all protected routes');
  });

  test('OAuth Flow End-to-End - Complete user journey', async ({ page }) => {
    console.log('🧪 Testing complete OAuth user journey with profile completion');

    let profileCompletionCalled = false;
    
    // Mock OAuth callback sequence
    await page.route('**/api/auth/profile-completion-status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          isCompleted: profileCompletionCalled,
          hasFirstName: true,
          hasLastName: true,
          hasGender: profileCompletionCalled,
          hasEthnicity: profileCompletionCalled
        }),
      });
    });

    await page.route('**/api/auth/complete-profile', async (route) => {
      profileCompletionCalled = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          message: 'Profile completed successfully'
        }),
      });
    });

    // Simulate OAuth login callback with token
    await page.addInitScript(() => {
      localStorage.setItem('auth_token', 'oauth-jwt-token');
      localStorage.setItem('currentUser', JSON.stringify({
        token: 'oauth-jwt-token',
        email: 'oauth-newuser@example.com',
        firstName: 'New',
        lastName: 'OAuthUser'
      }));
    });

    // Step 1: OAuth user tries to access dashboard
    await page.goto(`${testConfig.frontendUrl}/app/enhance`);
    
    // Step 2: Should redirect to profile completion
    await expect(page).toHaveURL(/\/auth\/complete-profile/, { timeout: 10000 });
    
    // Step 3: Complete profile
    await page.selectOption('#gender', 'Female');
    await page.selectOption('#ethnicity', 'Hispanic or Latino');
    await page.click('button[type="submit"]');
    
    // Step 4: Should redirect to Photo Workspace after completion
    await expect(page).toHaveURL(/\/app\/app/enhance/, { timeout: 10000 });
    
    // Step 5: Subsequent access should work without profile completion
    await page.goto(`${testConfig.frontendUrl}/app/gallery`);
    await expect(page).toHaveURL(/\/app\/gallery/);
    
    console.log('✅ Complete OAuth user journey with profile completion works end-to-end');
  });

  test.afterEach(async ({ page }) => {
    // Clean up test state
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
  });
});
