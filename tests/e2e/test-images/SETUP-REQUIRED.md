# Test Image Setup Required

The E2E tests expect a sample image at `tests/e2e/test-images/sample-selfie.jpg`.

## Quick Setup

1. Download a placeholder image:
   ```bash
   curl -o "tests/e2e/test-images/sample-selfie.jpg" "https://via.placeholder.com/400x400/4A90E2/FFFFFF?text=Test+Image"
   ```

2. Or create manually:
   - Size: 400x400 pixels
   - Format: JPEG
   - Content: Any test image (do not use personal photos)

## Note

Tests will be skipped until this file exists.
