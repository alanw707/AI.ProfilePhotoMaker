import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, map, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';
import { ConfigService } from './config.service';

export interface LoginDto {
  email: string;
  password: string;
}

export interface RegisterDto {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  gender: string;
  ethnicity: string;
}

export interface AuthResponseDto {
  token: string;
  email: string;
  firstName: string;
  lastName: string;
}

export interface ApiAuthResponseDto {
  isSuccess: boolean;
  message: string;
  token: string;
  expiration: string;
  email?: string;
  firstName?: string;
  lastName?: string;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';

  private _isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());
  public isAuthenticated$ = this._isAuthenticatedSubject.asObservable();

  private _currentUserSubject = new BehaviorSubject<AuthResponseDto | null>(this.getCurrentUser());
  public currentUser$ = this._currentUserSubject.asObservable();

  constructor(
    private _http: HttpClient,
    private _config: ConfigService,
    private _router: Router
  ) {
    // Initialize with secure session check
    this.initializeSecureAuth();
  }

  /**
   * Initialize secure authentication with improved session management
   */
  private initializeSecureAuth(): void {
    // Check existing token validity
    this.checkTokenValidity();

    // Set up periodic token validation (reduced frequency for better performance)
    setInterval(
      () => {
        if (localStorage.getItem(this.TOKEN_KEY)) {
          this.checkTokenValidity();
        }
      },
      10 * 60 * 1000
    ); // Check every 10 minutes instead of 5
  }

  /**
   * Enhanced token validity check with better error handling
   */
  private checkTokenValidity(): void {
    const token = localStorage.getItem(this.TOKEN_KEY);
    if (!token) {
      return;
    }

    try {
      if (this.isTokenExpired(token)) {
        console.warn('🔒 Token expired, initiating secure logout');
        this.secureLogout('token_expired');
      }
    } catch (error) {
      console.error('🔒 Token validation error:', error);
      this.secureLogout('token_invalid');
    }
  }

  handleOAuthCallback(token: string, expiration?: string): void {
    if (token) {
      try {
        // Store the token using consistent key
        localStorage.setItem(this.TOKEN_KEY, token);
        localStorage.setItem('authToken', token); // Keep both for compatibility

        if (expiration) {
          localStorage.setItem('tokenExpiration', expiration);
        }

        // Extract user info from token
        const user = this.extractUserFromToken(token);

        if (user && user.firstName && user.lastName) {
          // Complete user data from JWT
          localStorage.setItem('currentUser', JSON.stringify(user));
          this._currentUserSubject.next(user);
          this._isAuthenticatedSubject.next(true);
        } else {
          // Incomplete JWT data, fetch from profile API
          this._isAuthenticatedSubject.next(true);

          // Fetch user profile data from API to get complete firstName/lastName
          this.fetchUserProfileForOAuth(token);
        }
      } catch (error) {
        console.error('Error in OAuth callback handling:', error);
        this._isAuthenticatedSubject.next(false);
      }
    }
  }

  private fetchUserProfileForOAuth(token: string): void {
    let tempUser: AuthResponseDto;

    try {
      // Validate token format before processing
      if (!token || typeof token !== 'string') {
        console.error('🔒 Invalid token provided for OAuth profile fetch');
        return;
      }

      const tokenParts = token.split('.');
      if (tokenParts.length !== 3) {
        console.error('🔒 Invalid JWT token format for OAuth profile fetch');
        return;
      }

      // Create a minimal user object with just email for now
      const payload = JSON.parse(atob(tokenParts[1]));

      if (!payload || typeof payload !== 'object') {
        console.error('🔒 Invalid token payload for OAuth profile fetch');
        return;
      }

      const email =
        payload?.['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
        payload?.['email'] ||
        '';

      // Temporary user object while we fetch profile
      tempUser = {
        token,
        email,
        firstName: '',
        lastName: '',
      };

      localStorage.setItem('currentUser', JSON.stringify(tempUser));
      this._currentUserSubject.next(tempUser);
    } catch (error) {
      console.error('🔒 Error creating temporary user for OAuth:', error);
      // Create fallback user with minimal data
      tempUser = {
        token,
        email: '',
        firstName: 'User',
        lastName: '',
      };
      localStorage.setItem('currentUser', JSON.stringify(tempUser));
      this._currentUserSubject.next(tempUser);
      return;
    }

    // Fetch user profile from API to get firstName/lastName
    this._http
      .get<{
        firstName?: string;
        lastName?: string;
        email?: string;
      }>(this._config.buildApiEndpoint('profile'))
      .subscribe({
        next: response => {
          // Handle response that contains data directly (not wrapped in success/data structure)
          if (response && (response.firstName || response.lastName)) {
            const completeUser = {
              token,
              email: tempUser.email,
              firstName: response.firstName || '',
              lastName: response.lastName || '',
            };

            localStorage.setItem('currentUser', JSON.stringify(completeUser));
            this._currentUserSubject.next(completeUser);
          } else {
            // Keep the temp user with email username as firstName
            const fallbackUser = {
              ...tempUser,
              firstName: tempUser.email ? tempUser.email.split('@')[0] : 'User',
            };
            localStorage.setItem('currentUser', JSON.stringify(fallbackUser));
            this._currentUserSubject.next(fallbackUser);
          }
        },
        error: error => {
          console.error('Failed to fetch user profile:', error);
          // Fallback to email username
          const fallbackUser = {
            ...tempUser,
            firstName: tempUser.email ? tempUser.email.split('@')[0] : 'User',
          };
          localStorage.setItem('currentUser', JSON.stringify(fallbackUser));
          this._currentUserSubject.next(fallbackUser);
        },
      });
  }

  private extractUserFromToken(token: string): AuthResponseDto | null {
    try {
      // Validate token format before processing
      if (!token || typeof token !== 'string') {
        console.error('🔒 Invalid token format provided');
        return null;
      }

      const tokenParts = token.split('.');
      if (tokenParts.length !== 3) {
        console.error('🔒 Invalid JWT token format');
        return null;
      }

      const payload = JSON.parse(atob(tokenParts[1]));

      // Ensure payload is an object
      if (!payload || typeof payload !== 'object') {
        console.error('🔒 Invalid token payload');
        return null;
      }

      // Check .NET ClaimTypes standard URIs first, then fallback to short names
      const email =
        payload?.['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
        payload?.['email'] ||
        '';

      const firstName =
        payload?.['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'] ||
        payload?.['given_name'] ||
        payload?.['givenname'] ||
        payload?.['firstName'] ||
        '';

      const lastName =
        payload?.['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'] ||
        payload?.['family_name'] ||
        payload?.['surname'] ||
        payload?.['lastName'] ||
        '';

      // If no firstName/lastName in JWT, return null to force profile API lookup
      if (!firstName && !lastName) {
        console.log('🔒 JWT missing user details, fetching from profile API');
        return null;
      }

      return {
        token,
        email,
        firstName,
        lastName,
      };
    } catch (error) {
      console.error('🔒 Failed to extract user from token:', error);
      return null;
    }
  }

  login(credentials: LoginDto): Observable<AuthResponseDto> {
    return this._http.post<ApiAuthResponseDto>(this._config.authLoginUrl, credentials).pipe(
      map(apiResponse => {
        if (!apiResponse.isSuccess) {
          throw new Error(apiResponse.message);
        }
        // Use API response data directly (firstName/lastName come from the API response, not JWT)
        const authResponse = {
          token: apiResponse.token,
          email: apiResponse.email || '',
          firstName: apiResponse.firstName || '',
          lastName: apiResponse.lastName || '',
        } as AuthResponseDto;
        return authResponse;
      }),
      tap(response => this.setSecureSession(response))
    );
  }

  register(userData: RegisterDto): Observable<AuthResponseDto> {
    return this._http.post<ApiAuthResponseDto>(this._config.authRegisterUrl, userData).pipe(
      map(apiResponse => {
        if (!apiResponse.isSuccess) {
          throw new Error(apiResponse.message);
        }
        return {
          token: apiResponse.token,
          email: apiResponse.email || '',
          firstName: apiResponse.firstName || '',
          lastName: apiResponse.lastName || '',
        } as AuthResponseDto;
      }),
      tap(response => this.setSecureSession(response))
    );
  }

  /**
   * Enhanced logout with secure session cleanup
   */
  logout(): void {
    this.secureLogout('user_initiated');
  }

  /**
   * Secure logout with reason tracking and proper cleanup
   */
  private secureLogout(reason: string): void {
    console.log(`🔒 Secure logout initiated - reason: ${reason}`);

    try {
      // Clear all authentication data securely
      this.clearAllAuthData();

      // Update reactive state safely
      try {
        this._isAuthenticatedSubject.next(false);
      } catch (subjectError) {
        console.error('🔒 Error updating authentication subject:', subjectError);
      }

      try {
        this._currentUserSubject.next(null);
      } catch (subjectError) {
        console.error('🔒 Error updating current user subject:', subjectError);
      }

      // Navigate to login with reason
      this.navigateToLogin(reason);

      console.log('🔒 Secure logout completed successfully');
    } catch (error) {
      console.error('🔒 Error during secure logout:', error);

      // Force clear even if error occurs
      try {
        localStorage.clear();
      } catch (clearError) {
        console.error('🔒 Error clearing localStorage:', clearError);
      }

      // Fallback navigation
      try {
        this._router.navigate(['/auth/login']);
      } catch (navigationError) {
        console.error('🔒 Error navigating to login:', navigationError);
        // Ultimate fallback - force page reload
        window.location.href = '/auth/login';
      }
    }
  }

  /**
   * Clear all authentication-related data
   */
  private clearAllAuthData(): void {
    const authKeys = [
      this.TOKEN_KEY,
      'authToken',
      'tokenExpiration',
      'current_user',
      'currentUser',
      'token',
      'user',
      'auth',
      'secure_session',
      'refresh_token',
    ];

    authKeys.forEach(key => {
      try {
        localStorage.removeItem(key);
      } catch (error) {
        console.error(`🔒 Error removing ${key} from localStorage:`, error);
      }
    });
  }

  /**
   * Navigate to login page with logout reason
   */
  private navigateToLogin(reason: string): void {
    const queryParams = reason !== 'user_initiated' ? { reason } : {};

    this._router.navigate(['/auth/login'], { queryParams }).then(success => {
      if (success) {
        console.log('✅ Successfully navigated to login page');
      } else {
        console.error('❌ Failed to navigate to login page');
        // Fallback - force page reload to login
        window.location.href = '/auth/login';
      }
    });
  }

  /**
   * Force logout - clears all auth data (useful for debugging)
   */
  forceLogout(): void {
    console.log('🔒 Force logout initiated');
    try {
      localStorage.clear();
      this._isAuthenticatedSubject.next(false);
      this._currentUserSubject.next(null);
      this._router.navigate(['/auth/login']);
      console.log('🔒 Force logout completed');
    } catch (error) {
      console.error('🔒 Error during force logout:', error);
      // Force page reload as fallback
      window.location.href = '/auth/login';
    }
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return this.hasToken();
  }

  getCurrentUserId(): string | null {
    const token = this.getToken();
    if (!token || typeof token !== 'string') {
      return null;
    }

    try {
      const tokenParts = token.split('.');
      if (tokenParts.length !== 3) {
        console.error('🔒 Invalid JWT token format for user ID extraction');
        return null;
      }

      const payload = JSON.parse(atob(tokenParts[1]));

      // Ensure payload is an object
      if (!payload || typeof payload !== 'object') {
        console.error('🔒 Invalid token payload for user ID extraction');
        return null;
      }

      // .NET Identity uses 'nameid' claim for NameIdentifier
      const userId =
        payload?.nameid ||
        payload?.['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
        payload?.sub ||
        payload?.userId;
      return userId || null;
    } catch (error) {
      console.error('🔒 Failed to extract user ID from token:', error);
      return null;
    }
  }

  /**
   * Set session with enhanced security
   */
  private setSecureSession(authResult: AuthResponseDto): void {
    try {
      // Store token and user data
      localStorage.setItem(this.TOKEN_KEY, authResult.token);
      localStorage.setItem('currentUser', JSON.stringify(authResult));

      // Clean up old storage keys for security
      localStorage.removeItem('current_user');
      localStorage.removeItem('authData'); // Remove any legacy keys

      // Update reactive state
      this._isAuthenticatedSubject.next(true);
      this._currentUserSubject.next(authResult);

      console.log('🔒 Secure session established successfully');
    } catch (error) {
      console.error('🔒 Failed to establish secure session:', error);
      throw new Error('Session establishment failed');
    }
  }

  private hasToken(): boolean {
    const token = localStorage.getItem(this.TOKEN_KEY);

    if (!token) {
      return false;
    }

    const isExpired = this.isTokenExpired(token);

    if (isExpired) {
      console.warn('🔒 Auth token has expired');
      this.logout();
      return false;
    }

    return true;
  }

  private getCurrentUser(): AuthResponseDto | null {
    // Try both storage keys for backwards compatibility
    let userStr = localStorage.getItem('currentUser');
    if (!userStr) {
      userStr = localStorage.getItem('current_user');
    }

    if (userStr) {
      try {
        return JSON.parse(userStr);
      } catch {
        return null;
      }
    }

    return null;
  }

  private isTokenExpired(token: string): boolean {
    try {
      // Validate token format before processing
      if (!token || typeof token !== 'string') {
        console.error('🔒 Invalid token format for expiry check');
        return true;
      }

      const tokenParts = token.split('.');
      if (tokenParts.length !== 3) {
        console.error('🔒 Invalid JWT token format for expiry check');
        return true;
      }

      const payload = JSON.parse(atob(tokenParts[1]));

      // Ensure payload is an object and has exp claim
      if (!payload || typeof payload !== 'object' || typeof payload.exp !== 'number') {
        console.error('🔒 Invalid token payload or missing expiry claim');
        return true;
      }

      const exp = payload.exp * 1000; // Convert to milliseconds
      const isExpired = Date.now() >= exp;

      if (isExpired) {
        console.warn('🔒 Token has expired');
      }

      return isExpired;
    } catch (error) {
      console.error('🔒 Error checking token expiry:', error);
      return true; // If we can't parse the token, consider it expired
    }
  }
}
