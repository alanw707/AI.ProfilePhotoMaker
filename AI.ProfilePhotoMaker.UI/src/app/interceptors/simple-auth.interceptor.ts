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

  const headers: Record<string, string> = {
    'X-Requested-With': 'XMLHttpRequest',
    'ngrok-skip-browser-warning': 'true',
  };

  const fallbackToken = getDevelopmentFallbackToken();
  if (fallbackToken) {
    headers['Authorization'] = `Bearer ${fallbackToken}`;
  }

  const passReq = req.clone({
    setHeaders: headers,
    withCredentials: true,
  });
  return next(passReq);
};

function getDevelopmentFallbackToken(): string | null {
  if (typeof window === 'undefined') {
    return null;
  }

  const host = window.location.hostname.toLowerCase();
  if (host !== 'localhost' && host !== '127.0.0.1' && !host.endsWith('.ngrok-free.app')) {
    return null;
  }

  try {
    const currentUser = localStorage.getItem('currentUser');
    if (currentUser) {
      const parsed = JSON.parse(currentUser) as { token?: string };
      if (parsed.token) {
        return parsed.token;
      }
    }

    return localStorage.getItem('auth_token') || localStorage.getItem('authToken');
  } catch {
    return null;
  }
}

/**
 * Check if endpoint is public (doesn't require authentication)
 */
function isPublicEndpoint(url: string): boolean {
  const publicEndpoints = [
    '/auth/login',
    '/auth/register',
    '/auth/refresh-token',
    '/health',
    '/api/public',
    '/swagger',
    '/api/blog',
  ];

  return publicEndpoints.some(endpoint => url.includes(endpoint));
}
