import { test, expect, Page } from '@playwright/test';

// Contrast failures found only on the live site: scroll-revealed homepage labels and the
// cookie banner title. Ratios are computed from rendered colours in both themes.
const ratio = (page: Page, selector: string) =>
  page
    .locator(selector)
    .first()
    .evaluate(el => {
      const rgb = (c: string) => (c.match(/[\d.]+/g) || []).slice(0, 4).map(Number);
      const lum = (c: number[]) =>
        c
          .slice(0, 3)
          .map(v => v / 255)
          .map(v => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
          .reduce((t, v, i) => t + v * [0.2126, 0.7152, 0.0722][i], 0);
      const cs = getComputedStyle(el);
      // Composite translucent backgrounds from the element up to the first opaque layer.
      const layers: number[][] = [];
      for (let n: Element | null = el; n; n = n.parentElement) {
        const c = rgb(getComputedStyle(n).backgroundColor);
        const a = c.length > 3 ? c[3] : 1;
        if (a > 0) layers.push([c[0], c[1], c[2], a]);
        if (a >= 1) break;
      }
      let bg = [255, 255, 255];
      for (const [r, g, b2, a] of layers.reverse())
        bg = [r * a + bg[0] * (1 - a), g * a + bg[1] * (1 - a), b2 * a + bg[2] * (1 - a)];
      const fg = lum(rgb(cs.color));
      const b = lum(bg);
      return {
        r: (Math.max(fg, b) + 0.05) / (Math.min(fg, b) + 0.05),
        opacity: Number(cs.opacity),
        anim: cs.animationName,
      };
    });

for (const theme of ['light', 'dark']) {
  test(`cookie banner title and showcase label are readable (${theme})`, async ({ page }) => {
    await page.addInitScript(t => localStorage.setItem('theme', t), theme);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'We use cookies' })).toBeVisible();
    expect((await ratio(page, '.cookie-banner-title')).r).toBeGreaterThanOrEqual(4.5);
    const label = page.locator('.showcase-after .image-label').first();
    await label.scrollIntoViewIfNeeded();
    await expect(label).toBeVisible();
    expect((await ratio(page, '.showcase-after .image-label')).r).toBeGreaterThanOrEqual(4.5);
    const verified = page.locator('.testimonial-verified').first();
    await verified.scrollIntoViewIfNeeded();
    await expect(verified).toBeVisible();
    const v = await ratio(page, '.testimonial-verified');
    expect(v.anim).toBe('none');
    expect(v.opacity).toBe(1);
    expect(v.r).toBeGreaterThanOrEqual(4.5);
  });
}

for (const theme of ['light', 'dark']) {
  test(`404 primary link button and legal links are readable (${theme})`, async ({ page }) => {
    await page.addInitScript(t => localStorage.setItem('theme', t), theme);
    await page.goto('/this-page-does-not-exist');
    const cta = page.locator('a.btn-primary').first();
    await expect(cta).toBeVisible();
    expect((await ratio(page, 'a.btn-primary')).r).toBeGreaterThanOrEqual(4.5);
    await page.goto('/legal/privacy');
    await expect(page.locator('.legal-prose a').first()).toBeAttached();
    expect((await ratio(page, '.legal-prose a')).r).toBeGreaterThanOrEqual(4.5);
  });
}

// Tailwind `dark:` variants must follow the app theme, not the OS: an OS-light visitor who picks
// dark got a white 404 card with bright, near-invisible text.
for (const theme of ['light', 'dark']) {
  test(`404 card text follows the app theme on an OS-light device (${theme})`, async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'light' });
    await page.addInitScript(t => localStorage.setItem('theme', t), theme);
    await page.goto('/this-page-does-not-exist');
    await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible();
    await page.waitForTimeout(800);
    for (const sel of ['h1', 'h1 + p']) {
      expect((await ratio(page, sel)).r, sel).toBeGreaterThanOrEqual(4.5);
    }
  });
}
