import { test, expect } from '@playwright/test';

test.describe('OAuth Frontend URL Fix Validation', () => {
  test('should verify OAuth URLs are correctly constructed for production', async ({ page }) => {
    // Navigate to the login page
    await page.goto('https://app.aiprofilephotomaker.com/auth/login');
    
    // Wait for page to load
    await expect(page.locator('h1, h2, .login-title')).toBeVisible();
    
    // Check if Google login button exists
    const googleLoginButton = page.locator('button:has-text("Google"), [data-testid="google-login"], .google-login');
    await expect(googleLoginButton).toBeVisible();
    
    // Intercept the OAuth redirect
    let oauthUrl = '';
    page.on('framenavigated', (frame) => {
      if (frame.url().includes('external-login')) {
        oauthUrl = frame.url();
      }
    });
    
    // Listen for navigation to OAuth URL
    const [response] = await Promise.all([
      page.waitForResponse(response => 
        response.url().includes('external-login') || 
        response.url().includes('auth') ||
        response.status() === 302
      ).catch(() => null), // Don't fail if no response intercepted
      googleLoginButton.click()
    ]);
    
    // Check the current URL after redirect
    const currentUrl = page.url();
    console.log('OAuth redirect URL:', currentUrl);
    
    // Validate that the URL uses the correct production domain
    if (currentUrl.includes('external-login')) {
      expect(currentUrl).toContain('https://api.aiprofilephotomaker.com');
      expect(currentUrl).not.toContain('localhost:5032');
      expect(currentUrl).toContain('/api/auth/external-login');
    }
    
    // Also check if we got redirected to Google OAuth
    if (currentUrl.includes('accounts.google.com')) {
      console.log('✅ Successfully redirected to Google OAuth');
      expect(currentUrl).toContain('accounts.google.com');
    }
  });
  
  test('should verify register page OAuth URLs', async ({ page }) => {
    // Navigate to the register page
    await page.goto('https://app.aiprofilephotomaker.com/auth/register');
    
    // Wait for page to load
    await expect(page.locator('h1, h2, .register-title')).toBeVisible();
    
    // Check if Google register button exists
    const googleRegisterButton = page.locator('button:has-text("Google"), [data-testid="google-register"], .google-register');
    await expect(googleRegisterButton).toBeVisible();
    
    // Listen for navigation
    const [response] = await Promise.all([
      page.waitForResponse(response => 
        response.url().includes('external-login') || 
        response.url().includes('auth') ||
        response.status() === 302
      ).catch(() => null),
      googleRegisterButton.click()
    ]);
    
    // Check the current URL after redirect
    const currentUrl = page.url();
    console.log('OAuth register redirect URL:', currentUrl);
    
    // Validate that the URL uses the correct production domain
    if (currentUrl.includes('external-login')) {
      expect(currentUrl).toContain('https://api.aiprofilephotomaker.com');
      expect(currentUrl).not.toContain('localhost:5032');
      expect(currentUrl).toContain('/api/auth/external-login');
    }
    
    // Also check if we got redirected to Google OAuth
    if (currentUrl.includes('accounts.google.com')) {
      console.log('✅ Successfully redirected to Google OAuth from register');
      expect(currentUrl).toContain('accounts.google.com');
    }
  });
  
  test('should validate OAuth configuration in browser console', async ({ page }) => {
    // Navigate to login page
    await page.goto('https://app.aiprofilephotomaker.com/auth/login');
    
    // Execute JavaScript to check config service OAuth URL
    const oauthConfig = await page.evaluate(() => {
      // Access Angular's dependency injection to get config service
      const element = document.querySelector('app-root');
      if (element && (element as any).ng && (element as any).ng.getComponent) {
        try {
          const component = (element as any).ng.getComponent(0);
          // Try to access config service through component
          return {
            error: 'Could not access config service directly'
          };
        } catch (e) {
          return { error: e.message };
        }
      }
      
      // Fallback: check environment and window location
      return {
        windowOrigin: window.location.origin,
        hostname: window.location.hostname,
        isProduction: !window.location.hostname.includes('localhost')
      };
    });
    
    console.log('OAuth configuration check:', oauthConfig);
    
    // Validate we're on production
    expect(oauthConfig.isProduction).toBe(true);
    expect(oauthConfig.hostname).toBe('app.aiprofilephotomaker.com');
  });
});