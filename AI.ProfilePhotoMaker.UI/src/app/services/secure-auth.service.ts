import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, throwError, timer, EMPTY, of } from 'rxjs';
import { catchError, switchMap, tap, filter, take, finalize } from 'rxjs/operators';
import { Router } from '@angular/router';
import { ConfigService } from './config.service';
import { AuthService, AuthResponseDto, ApiAuthResponseDto } from './auth.service';

export interface TokenRefreshResponse {
  success: boolean;
  token?: string;
  expiration?: string;
  error?: string;
}

export interface SecureSession {
  token: string;
  refreshToken?: string;
  expiresAt: number;
  lastActivity: number;
  sessionId: string;
}

/**
 * Enhanced authentication service with secure session management and token refresh
 * Implements OWASP authentication security recommendations
 */
@Injectable({
  providedIn: 'root',
})
export class SecureAuthService {
  private readonly TOKEN_KEY = 'auth_token';
  private readonly REFRESH_TOKEN_KEY = 'refresh_token';
  private readonly SESSION_KEY = 'secure_session';
  private readonly TOKEN_REFRESH_THRESHOLD = 5 * 60 * 1000; // 5 minutes before expiry
  private readonly SESSION_TIMEOUT = 30 * 60 * 1000; // 30 minutes inactivity
  private readonly MAX_RETRY_ATTEMPTS = 3;

  private refreshTokenInProgress = false;
  private refreshTokenSubject = new BehaviorSubject<string | null>(null);
  private sessionCheckInterval?: number;

  constructor(
    private http: HttpClient,
    private config: ConfigService,
    private router: Router,
    private authService: AuthService
  ) {
    this.initializeSecureSession();
    this.startSessionMonitoring();
  }

  /**
   * Initialize secure session management
   */
  private initializeSecureSession(): void {
    const session = this.getSecureSession();
    if (session) {
      // Check if session is still valid
      if (this.isSessionValid(session)) {
        this.updateLastActivity();
        this.scheduleTokenRefresh(session.token);
      } else {
        console.warn('🔒 Session expired, clearing auth data');
        this.clearSecureSession();
      }
    }
  }

  /**
   * Start monitoring session activity and automatic token refresh
   */
  private startSessionMonitoring(): void {
    // Check session validity every minute
    this.sessionCheckInterval = window.setInterval(() => {
      const session = this.getSecureSession();
      if (session) {
        // Check for inactivity timeout
        const inactiveTime = Date.now() - session.lastActivity;
        if (inactiveTime > this.SESSION_TIMEOUT) {
          console.warn('🔒 Session timed out due to inactivity');
          this.handleSessionTimeout();
        }

        // Check if token needs refresh
        const timeUntilExpiry = session.expiresAt - Date.now();
        if (timeUntilExpiry <= this.TOKEN_REFRESH_THRESHOLD && timeUntilExpiry > 0) {
          console.log('🔄 Token approaching expiry, initiating refresh...');
          this.refreshToken().subscribe({
            error: error => console.error('🔒 Token refresh failed:', error),
          });
        }
      }
    }, 60 * 1000); // Every minute
  }

  /**
   * Enhanced request handler with automatic token refresh and retry logic
   */
  handleRequest(originalRequest: any, next: any): Observable<any> {
    const session = this.getSecureSession();

    if (!session) {
      // Fallback to traditional localStorage auth token for compatibility
      const authToken = localStorage.getItem('auth_token') || localStorage.getItem('authToken');
      if (authToken) {
        const fallbackRequest = originalRequest.clone({
          setHeaders: {
            Authorization: `Bearer ${authToken}`,
            'X-Requested-With': 'XMLHttpRequest',
            'ngrok-skip-browser-warning': 'true',
          },
        });
        return next(fallbackRequest);
      }
      return next(originalRequest);
    }

    // Update activity timestamp
    this.updateLastActivity();

    // Add secure headers
    const secureRequest = originalRequest.clone({
      setHeaders: {
        Authorization: `Bearer ${session.token}`,
        'X-Session-ID': session.sessionId,
        'X-Requested-With': 'XMLHttpRequest',
        'ngrok-skip-browser-warning': 'true',
      },
    });

    return next(secureRequest).pipe(
      catchError((error: HttpErrorResponse) => {
        // Handle 401 Unauthorized with token refresh
        if (error.status === 401 && !originalRequest.url.includes('auth/')) {
          return this.handle401Error(originalRequest, next);
        }

        // Handle other authentication errors
        if (error.status === 403) {
          console.warn('🔒 Access forbidden - insufficient permissions');
          return throwError(() => error);
        }

        return throwError(() => error);
      })
    );
  }

  /**
   * Handle 401 errors with automatic token refresh and retry
   */
  private handle401Error(request: any, next: any): Observable<any> {
    if (!this.refreshTokenInProgress) {
      this.refreshTokenInProgress = true;
      this.refreshTokenSubject.next(null);

      return this.refreshToken().pipe(
        switchMap((refreshResponse: TokenRefreshResponse) => {
          if (refreshResponse.success && refreshResponse.token) {
            this.refreshTokenSubject.next(refreshResponse.token);

            // Retry original request with new token
            const retryRequest = request.clone({
              setHeaders: {
                Authorization: `Bearer ${refreshResponse.token}`,
              },
            });

            return next(retryRequest);
          } else {
            // Refresh failed, redirect to login
            this.handleAuthenticationFailure();
            return throwError(() => new Error('Token refresh failed'));
          }
        }),
        catchError(error => {
          this.handleAuthenticationFailure();
          return throwError(() => error);
        }),
        finalize(() => {
          this.refreshTokenInProgress = false;
        })
      );
    } else {
      // Token refresh in progress, wait for it to complete
      return this.refreshTokenSubject.pipe(
        filter(token => token !== null),
        take(1),
        switchMap(token => {
          const retryRequest = request.clone({
            setHeaders: {
              Authorization: `Bearer ${token}`,
            },
          });
          return next(retryRequest);
        })
      );
    }
  }

  /**
   * Refresh JWT token securely
   */
  private refreshToken(): Observable<TokenRefreshResponse> {
    const session = this.getSecureSession();
    if (!session) {
      return throwError(() => new Error('No active session'));
    }

    const refreshData = {
      token: session.token,
      sessionId: session.sessionId,
    };

    return this.http.post<ApiAuthResponseDto>('/auth/refresh-token', refreshData).pipe(
      tap(response => {
        if (response.isSuccess && response.token) {
          console.log('🔄 Token refreshed successfully');
          this.updateSecureSession(response.token, response.expiration);
        }
      }),
      catchError((error: HttpErrorResponse) => {
        console.error('🔒 Token refresh failed:', error);

        // If refresh fails, try to get a new token using existing session
        if (error.status === 401 || error.status === 403) {
          return this.handleRefreshFailure();
        }

        return throwError(() => ({
          success: false,
          error: error.message || 'Token refresh failed',
        }));
      }),
      switchMap(response => {
        if ('isSuccess' in response && response.isSuccess) {
          return of({
            success: true,
            token: response.token,
            expiration: response.expiration,
          });
        } else if ('message' in response) {
          return throwError(() => ({ success: false, error: response.message }));
        } else {
          return throwError(() => ({ success: false, error: 'Token refresh failed' }));
        }
      })
    );
  }

  /**
   * Handle token refresh failure with fallback strategies
   */
  private handleRefreshFailure(): Observable<TokenRefreshResponse> {
    console.warn('🔒 Token refresh failed, attempting session recovery...');

    // Try to validate current session with server
    return this.validateSession().pipe(
      switchMap(isValid => {
        if (isValid) {
          // Session is still valid, maybe just a network issue
          return timer(2000).pipe(switchMap(() => this.refreshToken()));
        } else {
          // Session invalid, force re-authentication
          return throwError(() => ({ success: false, error: 'Session invalid' }));
        }
      }),
      catchError(() => {
        this.handleAuthenticationFailure();
        return throwError(() => ({ success: false, error: 'Session recovery failed' }));
      })
    );
  }

  /**
   * Validate current session with server
   */
  private validateSession(): Observable<boolean> {
    const session = this.getSecureSession();
    if (!session) {
      return of(false);
    }

    return this.http
      .post<{ valid: boolean }>('/auth/validate-session', {
        sessionId: session.sessionId,
      })
      .pipe(
        switchMap(response => of(response.valid)),
        catchError(() => of(false))
      );
  }

  /**
   * Create secure session with enhanced security
   */
  createSecureSession(authResponse: AuthResponseDto, refreshToken?: string): void {
    const expiresAt = this.extractTokenExpiry(authResponse.token);
    const sessionId = this.generateSecureSessionId();

    const secureSession: SecureSession = {
      token: authResponse.token,
      refreshToken,
      expiresAt,
      lastActivity: Date.now(),
      sessionId,
    };

    // Store session data securely
    this.storeSecureSession(secureSession);

    // Update auth service state
    this.authService.handleOAuthCallback(authResponse.token);

    // Schedule token refresh
    this.scheduleTokenRefresh(authResponse.token);

    console.log('🔒 Secure session created successfully');
  }

  /**
   * Update last activity timestamp
   */
  updateLastActivity(): void {
    const session = this.getSecureSession();
    if (session) {
      session.lastActivity = Date.now();
      this.storeSecureSession(session);
    }
  }

  /**
   * Handle session timeout
   */
  private handleSessionTimeout(): void {
    console.warn('🔒 Session timeout - redirecting to login');
    this.clearSecureSession();
    this.router.navigate(['/auth/login'], {
      queryParams: { reason: 'session_timeout' },
    });
  }

  /**
   * Handle authentication failure
   */
  private handleAuthenticationFailure(): void {
    console.error('🔒 Authentication failure - clearing session');
    this.clearSecureSession();
    this.authService.logout();
  }

  /**
   * Schedule automatic token refresh
   */
  private scheduleTokenRefresh(token: string): void {
    const expiryTime = this.extractTokenExpiry(token);
    const refreshTime = expiryTime - this.TOKEN_REFRESH_THRESHOLD;
    const delay = refreshTime - Date.now();

    if (delay > 0) {
      timer(delay).subscribe(() => {
        if (this.getSecureSession()) {
          console.log('🔄 Scheduled token refresh triggered');
          this.refreshToken().subscribe({
            error: error => console.error('🔒 Scheduled refresh failed:', error),
          });
        }
      });
    }
  }

  /**
   * Extract token expiry time
   */
  private extractTokenExpiry(token: string): number {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000; // Convert to milliseconds
    } catch (error) {
      console.error('🔒 Failed to extract token expiry:', error);
      return Date.now() + 60 * 60 * 1000; // Default to 1 hour
    }
  }

  /**
   * Generate secure session ID
   */
  private generateSecureSessionId(): string {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return Array.from(array, byte => byte.toString(16).padStart(2, '0')).join('');
  }

  /**
   * Check if session is valid
   */
  private isSessionValid(session: SecureSession): boolean {
    const now = Date.now();

    // Check token expiry
    if (now >= session.expiresAt) {
      return false;
    }

    // Check inactivity timeout
    if (now - session.lastActivity > this.SESSION_TIMEOUT) {
      return false;
    }

    return true;
  }

  /**
   * Store session securely
   */
  private storeSecureSession(session: SecureSession): void {
    try {
      const encryptedSession = this.encryptSessionData(session);
      localStorage.setItem(this.SESSION_KEY, encryptedSession);

      // Also store token for backward compatibility with existing auth service
      localStorage.setItem(this.TOKEN_KEY, session.token);
    } catch (error) {
      console.error('🔒 Failed to store secure session:', error);
    }
  }

  /**
   * Get secure session
   */
  private getSecureSession(): SecureSession | null {
    try {
      const encryptedSession = localStorage.getItem(this.SESSION_KEY);
      if (!encryptedSession) {
        return null;
      }

      return this.decryptSessionData(encryptedSession);
    } catch (error) {
      console.error('🔒 Failed to retrieve secure session:', error);
      this.clearSecureSession();
      return null;
    }
  }

  /**
   * Update secure session with new token
   */
  private updateSecureSession(newToken: string, expiration?: string): void {
    const session = this.getSecureSession();
    if (session) {
      session.token = newToken;
      session.expiresAt = expiration
        ? new Date(expiration).getTime()
        : this.extractTokenExpiry(newToken);
      session.lastActivity = Date.now();

      this.storeSecureSession(session);
      this.scheduleTokenRefresh(newToken);
    }
  }

  /**
   * Clear secure session
   */
  clearSecureSession(): void {
    localStorage.removeItem(this.SESSION_KEY);
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);

    if (this.sessionCheckInterval) {
      clearInterval(this.sessionCheckInterval);
    }

    console.log('🔒 Secure session cleared');
  }

  /**
   * Simple encryption for session data (not cryptographically secure, but better than plain storage)
   * In production, consider using Web Crypto API or server-side session management
   */
  private encryptSessionData(session: SecureSession): string {
    // Simple base64 encoding with basic obfuscation
    // In production, use proper encryption
    const sessionJson = JSON.stringify(session);
    return btoa(sessionJson + '::secure_session');
  }

  /**
   * Decrypt session data
   */
  private decryptSessionData(encryptedData: string): SecureSession {
    try {
      const decoded = atob(encryptedData);
      const sessionJson = decoded.replace('::secure_session', '');
      return JSON.parse(sessionJson);
    } catch (error) {
      throw new Error('Failed to decrypt session data');
    }
  }

  /**
   * Get current session status for debugging
   */
  getSessionStatus(): {
    hasSession: boolean;
    timeUntilExpiry?: number;
    inactiveTime?: number;
    sessionId?: string;
  } {
    const session = this.getSecureSession();
    if (!session) {
      return { hasSession: false };
    }

    return {
      hasSession: true,
      timeUntilExpiry: session.expiresAt - Date.now(),
      inactiveTime: Date.now() - session.lastActivity,
      sessionId: session.sessionId,
    };
  }

  /**
   * Force session refresh for testing
   */
  forceTokenRefresh(): Observable<TokenRefreshResponse> {
    return this.refreshToken();
  }

  /**
   * Cleanup on service destruction
   */
  ngOnDestroy(): void {
    if (this.sessionCheckInterval) {
      clearInterval(this.sessionCheckInterval);
    }
  }
}
