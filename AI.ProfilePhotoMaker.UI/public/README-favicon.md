# Favicon and app icon regeneration

Source asset: `Logo.PNG`.

Regenerate icons locally with Python + Pillow:

1. Create a venv and install Pillow.
2. Run a script that:
   - Crops to the alpha bounding box.
   - Renders white backgrounds.
   - Exports sizes: 16, 32, 180, 192, 512.
   - Creates maskable icons with extra padding.
   - Writes multi-size `favicon.ico`.
   - Updates versioned filenames (use the same suffix as in `src/index.html`).

After generating icons:
- Update `src/index.html` icon links to the versioned filenames.
- Update `public/manifest.json` icon entries (any + maskable).
- Keep `favicon.ico` and the non-versioned PNGs updated for browser fallbacks.
