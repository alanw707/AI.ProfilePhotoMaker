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
  // Rely on secure HttpOnly cookie; do not add Authorization header from storage
  const passReq = req.clone({
    setHeaders: {
      'X-Requested-With': 'XMLHttpRequest',
      'ngrok-skip-browser-warning': 'true',
    },
    withCredentials: true,
  });
  return next(passReq);
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
