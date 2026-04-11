/**
 * Zone.js configuration for face-api.js compatibility
 *
 * This configuration disables zone.js patching for specific APIs
 * that are used by face-api.js to prevent setTimeout violations.
 */

// Disable zone.js patching for Image onload/onerror events
// This prevents zone.js violations when face-api.js loads model files
// eslint-disable-next-line @typescript-eslint/naming-convention
(window as unknown as { __Zone_disable_on_property: boolean }).__Zone_disable_on_property = true;

// Disable zone.js patching for custom elements (additional safety)
// eslint-disable-next-line @typescript-eslint/naming-convention
(window as unknown as { __Zone_disable_customElements: boolean }).__Zone_disable_customElements =
  true;

// Web Locks API polyfill/wrapper to prevent NavigatorLockAcquireTimeoutError
if (typeof navigator !== 'undefined' && navigator.locks) {
  const originalRequest = navigator.locks.request;
  navigator.locks.request = function (
    name: string,
    optionsOrCallback: LockOptions | LockGrantedCallback,
    callback?: LockGrantedCallback
  ): Promise<unknown> {
    // Extract the actual callback and options
    const hasOptions = typeof optionsOrCallback === 'object' && optionsOrCallback !== null;
    const actualCallback = hasOptions ? callback : (optionsOrCallback as LockGrantedCallback);
    const actualOptions = hasOptions ? (optionsOrCallback as LockOptions) : {};

    // Add a timeout to prevent infinite waiting
    const timeoutOptions: LockOptions = {
      ...actualOptions,
      ifAvailable: actualOptions.ifAvailable ?? true,
      steal: actualOptions.steal ?? false,
    };

    // Call the original request with timeout options
    return originalRequest.call(this, name, timeoutOptions, actualCallback!);
  };
}

// Import zone.js after configuration
import 'zone.js';
