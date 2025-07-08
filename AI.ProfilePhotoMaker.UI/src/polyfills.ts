/**
 * Zone.js configuration for face-api.js compatibility
 * 
 * This configuration disables zone.js patching for specific APIs
 * that are used by face-api.js to prevent setTimeout violations.
 */

// Disable zone.js patching for Image onload/onerror events
// This prevents zone.js violations when face-api.js loads model files
(window as any).__Zone_disable_on_property = true;

// Disable zone.js patching for XMLHttpRequest
// This prevents violations when face-api.js downloads model files from CDN
(window as any).__Zone_disable_XMLHttpRequest = true;

// Disable zone.js patching for custom elements (additional safety)
(window as any).__Zone_disable_customElements = true;

// Import zone.js after configuration
import 'zone.js';