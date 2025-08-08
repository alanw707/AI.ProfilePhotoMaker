import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Simple authentication interceptor that adds the Bearer token to all non-public requests
 * This replaces the complex SecureAuthInterceptor to fix authentication issues
 */
export const simpleAuthInterceptor: HttpInterceptorFn = (req, next) => {
  // Skip auth for public endpoints
  if (isPublicEndpoint(req.url)) {
    return next(req);
  }

  // Get the auth token from localStorage (check both keys for compatibility)
  const authToken = localStorage.getItem('auth_token') || localStorage.getItem('authToken');

  // If we have a token, add it to the request
  if (authToken) {
    const authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${authToken}`,
        'X-Requested-With': 'XMLHttpRequest',
        'ngrok-skip-browser-warning': 'true',
      },
    });
    return next(authReq);
  }

  // No token, proceed with original request
  return next(req);
};

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
  ];

  return publicEndpoints.some(endpoint => url.includes(endpoint));
}
