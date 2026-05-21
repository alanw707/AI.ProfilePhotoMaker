import { test, expect } from '@playwright/test'

test.describe('File Upload Icon Positioning Tests', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the Photo Workspace page
    await page.goto('http://localhost:4200/app/enhance')
    
    // Wait for the page to load
    await page.waitForSelector('.file-upload-section')
  })

  test('should position info icon within thumbnail bounds', async ({ page }) => {
    // Upload a test image to trigger the file preview
    const fileInput = page.locator('input[type="file"]')
    await fileInput.setInputFiles('cypress/fixtures/test-image.jpg')
    
    // Wait for quality check to complete
    await page.waitForSelector('.selected-files-preview')
    
    // Find a file card with invalid status (red border)
    const invalidCard = page.locator('.selected-file-item-enhanced.file-invalid').first()
    await expect(invalidCard).toBeVisible()
    
    // Get thumbnail and info icon elements
    const thumbnail = invalidCard.locator('img').first()
    const infoIcon = invalidCard.locator('.file-info-popup .info-btn')
    
    // Get bounding boxes
    const thumbBox = await thumbnail.boundingBox()
    const iconBox = await infoIcon.boundingBox()
    
    // Verify icon is positioned within thumbnail bounds
    expect(thumbBox).toBeTruthy()
    expect(iconBox).toBeTruthy()
    
    if (thumbBox && iconBox) {
      expect(iconBox.x).toBeGreaterThan(thumbBox.x)
      expect(iconBox.y).toBeGreaterThan(thumbBox.y)
      expect(iconBox.x + iconBox.width).toBeLessThan(thumbBox.x + thumbBox.width)
      expect(iconBox.y + iconBox.height).toBeLessThan(thumbBox.y + thumbBox.height)
    }
  })

  test('should have subtle styling for info icon', async ({ page }) => {
    // Upload a test image
    const fileInput = page.locator('input[type="file"]')
    await fileInput.setInputFiles('cypress/fixtures/test-image.jpg')
    
    await page.waitForSelector('.selected-files-preview')
    
    const infoBtn = page.locator('.info-btn').first()
    await expect(infoBtn).toBeVisible()
    
    // Check computed styles
    const styles = await infoBtn.evaluate((el) => {
      const computed = window.getComputedStyle(el)
      return {
        backgroundColor: computed.backgroundColor,
        width: computed.width,
        height: computed.height,
        opacity: computed.opacity,
        borderWidth: computed.borderWidth
      }
    })
    
    // Should have subtle dark background (not bright red)
    expect(styles.backgroundColor).toContain('rgba(0, 0, 0')
    
    // Should have reduced size (18-22px range)
    const width = parseInt(styles.width)
    expect(width).toBeGreaterThanOrEqual(18)
    expect(width).toBeLessThanOrEqual(22)
    
    // Should have subtle opacity
    expect(parseFloat(styles.opacity)).toBeLessThan(1)
    
    // Should have thinner border
    expect(styles.borderWidth).toBe('1px')
  })

  test('should have proper hover states', async ({ page }) => {
    // Upload a test image
    const fileInput = page.locator('input[type="file"]')
    await fileInput.setInputFiles('cypress/fixtures/test-image.jpg')
    
    await page.waitForSelector('.selected-files-preview')
    
    const infoBtn = page.locator('.info-btn').first()
    await expect(infoBtn).toBeVisible()
    
    // Get initial styles
    const initialStyles = await infoBtn.evaluate((el) => {
      const computed = window.getComputedStyle(el)
      return {
        opacity: computed.opacity,
        backgroundColor: computed.backgroundColor
      }
    })
    
    // Hover over the button
    await infoBtn.hover()
    
    // Get hover styles
    const hoverStyles = await infoBtn.evaluate((el) => {
      const computed = window.getComputedStyle(el)
      return {
        opacity: computed.opacity,
        backgroundColor: computed.backgroundColor,
        boxShadow: computed.boxShadow
      }
    })
    
    // Should have full opacity on hover
    expect(parseFloat(hoverStyles.opacity)).toBe(1)
    
    // Should have enhanced shadow
    expect(hoverStyles.boxShadow).toContain('rgba(0, 0, 0, 0.3)')
    
    // Background should be darker on hover
    expect(hoverStyles.backgroundColor).toContain('rgba(0, 0, 0, 0.85)')
  })

  test('should toggle popup on click', async ({ page }) => {
    // Upload a test image
    const fileInput = page.locator('input[type="file"]')
    await fileInput.setInputFiles('cypress/fixtures/test-image.jpg')
    
    await page.waitForSelector('.selected-files-preview')
    
    const infoBtn = page.locator('.info-btn').first()
    await expect(infoBtn).toBeVisible()
    
    // Click info button
    await infoBtn.click()
    
    // Verify popup appears
    const popup = page.locator('.popup-details')
    await expect(popup).toBeVisible()
    
    // Click again to close
    await infoBtn.click()
    
    // Verify popup is hidden
    await expect(popup).not.toBeVisible()
  })

  test('should be accessible via keyboard', async ({ page }) => {
    // Upload a test image
    const fileInput = page.locator('input[type="file"]')
    await fileInput.setInputFiles('cypress/fixtures/test-image.jpg')
    
    await page.waitForSelector('.selected-files-preview')
    
    const infoBtn = page.locator('.info-btn').first()
    await expect(infoBtn).toBeVisible()
    
    // Focus on info button using tab
    await infoBtn.focus()
    
    // Verify focus styles
    await expect(infoBtn).toBeFocused()
    
    // Press Enter to activate
    await page.keyboard.press('Enter')
    
    // Verify popup appears
    const popup = page.locator('.popup-details')
    await expect(popup).toBeVisible()
  })

  test('should maintain positioning across screen sizes', async ({ page }) => {
    // Test mobile viewport
    await page.setViewportSize({ width: 375, height: 667 })
    
    // Upload a test image
    const fileInput = page.locator('input[type="file"]')
    await fileInput.setInputFiles('cypress/fixtures/test-image.jpg')
    
    await page.waitForSelector('.selected-files-preview')
    
    // Verify mobile positioning
    const invalidCard = page.locator('.selected-file-item-enhanced.file-invalid').first()
    await expect(invalidCard).toBeVisible()
    
    const thumbnail = invalidCard.locator('img').first()
    const infoIcon = invalidCard.locator('.file-info-popup .info-btn')
    
    await expect(infoIcon).toBeVisible()
    
    // Should still be positioned within thumbnail bounds
    const thumbBox = await thumbnail.boundingBox()
    const iconBox = await infoIcon.boundingBox()
    
    if (thumbBox && iconBox) {
      expect(iconBox.x).toBeGreaterThan(thumbBox.x)
      expect(iconBox.y).toBeGreaterThan(thumbBox.y)
    }
    
    // Test desktop viewport
    await page.setViewportSize({ width: 1920, height: 1080 })
    
    // Verify desktop positioning still works
    await expect(infoIcon).toBeVisible()
    
    // Re-check positioning on desktop
    const desktopThumbBox = await thumbnail.boundingBox()
    const desktopIconBox = await infoIcon.boundingBox()
    
    if (desktopThumbBox && desktopIconBox) {
      expect(desktopIconBox.x).toBeGreaterThan(desktopThumbBox.x)
      expect(desktopIconBox.y).toBeGreaterThan(desktopThumbBox.y)
    }
  })

  test('should have proper z-index stacking', async ({ page }) => {
    // Upload a test image
    const fileInput = page.locator('input[type="file"]')
    await fileInput.setInputFiles('cypress/fixtures/test-image.jpg')
    
    await page.waitForSelector('.selected-files-preview')
    
    const invalidCard = page.locator('.selected-file-item-enhanced.file-invalid').first()
    await expect(invalidCard).toBeVisible()
    
    // Get z-index values
    const thumbnail = invalidCard.locator('img').first()
    const infoIcon = invalidCard.locator('.file-info-popup .info-btn')
    const removeBtn = invalidCard.locator('.remove-file-btn')
    
    const thumbnailZIndex = await thumbnail.evaluate((el) => window.getComputedStyle(el).zIndex)
    const infoIconZIndex = await infoIcon.evaluate((el) => window.getComputedStyle(el).zIndex)
    const removeBtnZIndex = await removeBtn.evaluate((el) => window.getComputedStyle(el).zIndex)
    
    // Info icon should be above thumbnail but below remove button
    expect(parseInt(infoIconZIndex)).toBeGreaterThan(parseInt(thumbnailZIndex) || 0)
    expect(parseInt(removeBtnZIndex)).toBeGreaterThan(parseInt(infoIconZIndex))
  })
})