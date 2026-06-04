import { test, expect } from '@playwright/test';

const BASE_URL = 'http://localhost:4200';
const STUB_USER = {
  token: '',
  email: 'playwright@example.com',
  firstName: 'Play',
  lastName: 'Wright',
};

async function seedSession(page) {
  await page.addInitScript(user => {
    localStorage.setItem('currentUser', JSON.stringify(user));
  }, STUB_USER);

  await page.route('**/api/auth/validate-session', route => {
    route.fulfill({ status: 200, contentType: 'text/plain', body: 'OK' });
  });

  await page.route('**/api/**', route => {
    if (!route.request().url().includes('/api/auth/validate-session')) {
      const method = route.request().method();
      const body = method === 'GET' ? '{}' : '{"success":true}';
      route.fulfill({ status: 200, contentType: 'application/json', body });
    }
  });
}

test.describe('Enhanced Image Flow Analysis', () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
    await page.goto(BASE_URL + '/app/enhance');
    await page.waitForLoadState('domcontentloaded');
  });

  test('routes OpenAI enhancement requests through the new endpoint', async ({ page }) => {
    let capturedBody: any = null;

    await page.unroute('**/api/enhancement/enhance').catch(() => {});
    await page.route('**/api/enhancement/enhance', async route => {
      capturedBody = await route.request().postDataJSON();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            creditsRemaining: 9,
            enhancementType: 'chibi',
            provider: 'openai',
            dataUrl: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIW2P4//8/AwAI/AL+g0BGVAAAAABJRU5ErkJggg==',
          },
          error: null,
        }),
      });
    });

    const enhanceResult: any = await page.evaluate(async () => {
      const debug = (window as any).__APP_DEBUG__;
      const replicateService = debug?.services?.replicateService;
      if (!replicateService) {
        return { success: false, error: 'ReplicateService unavailable' };
      }

      return await new Promise((resolve, reject) => {
        replicateService
          .enhancePhoto({
            imageUrl: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIW2P4//8/AwAI/AL+g0BGVAAAAABJRU5ErkJggg==',
            enhancementType: 'chibi',
          })
          .subscribe({
            next: resolve,
            error: err => reject(err?.message || err),
          });
      });
    });

    expect(capturedBody?.enhancementType).toBe('chibi');
    expect(enhanceResult?.success).toBe(true);
    expect(enhanceResult?.data?.provider).toBe('openai');
  });

  test('saves enhanced images through FileUploadService.saveEnhancedImage', async ({ page }) => {
    let saveRequestBody: any = null;

    await page.unroute('**/api/image/save-enhanced').catch(() => {});
    await page.route('**/api/image/save-enhanced', async route => {
      saveRequestBody = JSON.parse(route.request().postData() || '{}');
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            url: 'https://cdn.example.com/enhanced-photo.png',
            fileName: 'enhanced-photo.png',
          },
        }),
      });
    });

    const saveResult: any = await page.evaluate(async () => {
      const debug = (window as any).__APP_DEBUG__;
      const fileUploadService = debug?.services?.fileUploadService;
      if (!fileUploadService) {
        return { success: false, error: 'FileUploadService unavailable' };
      }

      return await fileUploadService.saveEnhancedImage(
        'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIW2P4//8/AwAI/AL+g0BGVAAAAABJRU5ErkJggg==',
        'chibi'
      );
    });

    expect(saveRequestBody?.base64ImageData).toContain('data:image/png;base64');
    expect(saveRequestBody?.enhancementType).toBe('chibi');
    expect(saveResult?.success).toBe(true);
  });

  test('debug context exposes file upload service and replicate service', async ({ page }) => {
    const debugInfo = await page.evaluate(() => {
      const debug = (window as any).__APP_DEBUG__;
      return {
        hasFileUpload: !!debug?.services?.fileUploadService,
        hasReplicate: !!debug?.services?.replicateService,
      };
    });

    expect(debugInfo.hasFileUpload).toBe(true);
    expect(debugInfo.hasReplicate).toBe(true);
  });
});
