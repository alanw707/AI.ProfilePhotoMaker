import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, finalize } from 'rxjs/operators';
import { throwError, timer } from 'rxjs';
import { SecureAuthService } from '../services/secure-auth.service';

/**
 * Enhanced authentication interceptor with automatic token refresh and security headers
 * Implements OWASP security recommendations for HTTP requests
 */
export const secureAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const secureAuthService = inject(SecureAuthService);

  // Skip auth for public endpoints
  if (isPublicEndpoint(req.url)) {
    return addSecurityHeaders(req, next);
  }

  // Handle authenticated requests with secure session management
  return secureAuthService.handleRequest(req, next);
};

/**
 * Add security headers to all requests
 */
function addSecurityHeaders(req: any, next: any) {
  const secureRequest = req.clone({
    setHeaders: {
      // OWASP security headers
      'X-Content-Type-Options': 'nosniff',
      'X-Frame-Options': 'DENY',
      'X-XSS-Protection': '1; mode=block',
      'Referrer-Policy': 'strict-origin-when-cross-origin',
      'X-Requested-With': 'XMLHttpRequest',
      // Skip ngrok browser warning
      'ngrok-skip-browser-warning': 'true',
      // Content security policy for API requests
      'Content-Security-Policy': "default-src 'self'",
    },
  });

  return next(secureRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      // Log security-relevant errors (without sensitive data)
      if (error.status === 401) {
        console.warn('🔒 Unauthorized request to:', sanitizeUrl(req.url));
      } else if (error.status === 403) {
        console.warn('🔒 Forbidden request to:', sanitizeUrl(req.url));
      } else if (error.status >= 500) {
        console.error(
          '🔒 Server error for request to:',
          sanitizeUrl(req.url),
          'Status:',
          error.status
        );
      }

      return throwError(() => error);
    })
  );
}

/**
 * Check if endpoint is public (doesn't require authentication)
 */
function isPublicEndpoint(url: string): boolean {
  const publicEndpoints = [
    '/auth/login',
    '/auth/register',
    '/auth/refresh-token',
    '/auth/validate-session',
    '/health',
    '/api/public',
    '/swagger',
    // Add other public endpoints as needed
  ];

  return publicEndpoints.some(endpoint => url.includes(endpoint));
}

/**
 * Sanitize URL for logging (remove sensitive parameters)
 */
function sanitizeUrl(url: string): string {
  try {
    const urlObj = new URL(url, window.location.origin);

    // Remove sensitive query parameters
    const sensitiveParams = ['token', 'auth', 'key', 'secret', 'password'];
    sensitiveParams.forEach(param => {
      if (urlObj.searchParams.has(param)) {
        urlObj.searchParams.set(param, '[REDACTED]');
      }
    });

    return urlObj.toString();
  } catch {
    // If URL parsing fails, just return a sanitized version
    return url.split('?')[0] + (url.includes('?') ? '?[PARAMETERS_REDACTED]' : '');
  }
}

/**
 * Legacy auth interceptor for backward compatibility
 * This can be removed once SecureAuthService is fully integrated
 */
export const legacyAuthInterceptor: HttpInterceptorFn = (req, next) => {
  // Get the auth token from localStorage (check both possible keys for compatibility)
  const authToken = localStorage.getItem('auth_token') || localStorage.getItem('authToken');

  // Clone the request and add headers
  let modifiedReq = req.clone({
    setHeaders: {
      // Add ngrok header to skip browser warning
      'ngrok-skip-browser-warning': 'true',
      // Basic security headers
      'X-Requested-With': 'XMLHttpRequest',
    },
  });

  // Add Authorization header if token exists
  if (authToken) {
    modifiedReq = modifiedReq.clone({
      setHeaders: {
        Authorization: `Bearer ${authToken}`,
        'ngrok-skip-browser-warning': 'true',
        'X-Requested-With': 'XMLHttpRequest',
      },
    });
  }

  return next(modifiedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // Basic error logging for legacy interceptor
      if (error.status === 401 && authToken) {
        console.warn('🔒 Legacy auth token may be expired');
      }
      return throwError(() => error);
    })
  );
};
