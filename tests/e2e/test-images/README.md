# Test Images Directory

This directory contains test images for E2E validation of the image upload functionality.

## Required Test Images

### sample-selfie.jpg
A sample portrait image for testing image upload functionality.

**Requirements:**
- Format: JPEG
- Size: 200x200 to 1000x1000 pixels
- File size: < 5MB
- Content: Any portrait/selfie image (can be placeholder)

**Creating a test image:**

### Option 1: Download placeholder image
```bash
curl -o sample-selfie.jpg "https://via.placeholder.com/400x400/4A90E2/FFFFFF?text=Test+Image"
```

### Option 2: Use ImageMagick (if available)
```bash
convert -size 400x400 xc:lightblue -pointsize 30 -gravity center \
  -annotate +0+0 "Test Image\n$(date +%Y-%m-%d)" sample-selfie.jpg
```

### Option 3: Manual creation
1. Use any image editing software (GIMP, Photoshop, etc.)
2. Create a 400x400 pixel image
3. Add text "Test Image" in the center
4. Save as `sample-selfie.jpg` in this directory

## Usage in Tests

The E2E tests reference this image via:
```javascript
testImagePath: path.join(__dirname, 'test-images', 'sample-selfie.jpg')
```

## Security Note

- Test images should not contain sensitive or personal information
- Use placeholder or generated images only
- Do not commit actual personal photos to the repository

## Validation

The test image will be:
1. Uploaded to the application
2. Validated for accessibility via generated URL
3. Used to verify storage service configuration
4. Automatically cleaned up after testing