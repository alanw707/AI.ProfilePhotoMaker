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

  private isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();

  private currentUserSubject = new BehaviorSubject<AuthResponseDto | null>(this.getCurrentUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private http: HttpClient,
    private config: ConfigService,
    private router: Router
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
          this.currentUserSubject.next(user);
          this.isAuthenticatedSubject.next(true);
        } else {
          // Incomplete JWT data, fetch from profile API
          this.isAuthenticatedSubject.next(true);

          // Fetch user profile data from API to get complete firstName/lastName
          this.fetchUserProfileForOAuth(token);
        }
      } catch (error) {
        console.error('Error in OAuth callback handling:', error);
        this.isAuthenticatedSubject.next(false);
      }
    }
  }

  private fetchUserProfileForOAuth(token: string): void {
    // Create a minimal user object with just email for now
    const payload = JSON.parse(atob(token.split('.')[1]));
    const email =
      payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
      payload['email'] ||
      '';

    // Temporary user object while we fetch profile
    const tempUser = {
      token,
      email,
      firstName: '',
      lastName: '',
    };

    localStorage.setItem('currentUser', JSON.stringify(tempUser));
    this.currentUserSubject.next(tempUser);

    // Fetch user profile from API to get firstName/lastName
    this.http.get<any>(`${this.config.baseUrl}/profile`).subscribe({
      next: response => {
        // Handle response that contains data directly (not wrapped in success/data structure)
        if (response && (response.firstName || response.lastName)) {
          const completeUser = {
            token,
            email,
            firstName: response.firstName || '',
            lastName: response.lastName || '',
          };

          localStorage.setItem('currentUser', JSON.stringify(completeUser));
          this.currentUserSubject.next(completeUser);
        } else {
          // Keep the temp user with email username as firstName
          const fallbackUser = {
            ...tempUser,
            firstName: email.split('@')[0],
          };
          localStorage.setItem('currentUser', JSON.stringify(fallbackUser));
          this.currentUserSubject.next(fallbackUser);
        }
      },
      error: error => {
        console.error('Failed to fetch user profile:', error);
        // Fallback to email username
        const fallbackUser = {
          ...tempUser,
          firstName: email.split('@')[0],
        };
        localStorage.setItem('currentUser', JSON.stringify(fallbackUser));
        this.currentUserSubject.next(fallbackUser);
      },
    });
  }

  private extractUserFromToken(token: string): AuthResponseDto | null {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      // Remove sensitive payload logging in production

      // Check .NET ClaimTypes standard URIs first, then fallback to short names
      const email =
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
        payload['email'] ||
        '';

      const firstName =
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'] ||
        payload['given_name'] ||
        payload['givenname'] ||
        payload['firstName'] ||
        '';

      const lastName =
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'] ||
        payload['family_name'] ||
        payload['surname'] ||
        payload['lastName'] ||
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
      console.error('Failed to extract user from token:', error);
      return null;
    }
  }

  login(credentials: LoginDto): Observable<AuthResponseDto> {
    return this.http.post<ApiAuthResponseDto>(this.config.authLoginUrl, credentials).pipe(
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
    return this.http.post<ApiAuthResponseDto>(this.config.authRegisterUrl, userData).pipe(
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

      // Update reactive state
      this.isAuthenticatedSubject.next(false);
      this.currentUserSubject.next(null);

      // Navigate to login with reason
      this.navigateToLogin(reason);

      console.log('🔒 Secure logout completed successfully');
    } catch (error) {
      console.error('🔒 Error during secure logout:', error);
      // Force clear even if error occurs
      localStorage.clear();
      this.router.navigate(['/auth/login']);
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
      localStorage.removeItem(key);
    });
  }

  /**
   * Navigate to login page with logout reason
   */
  private navigateToLogin(reason: string): void {
    const queryParams = reason !== 'user_initiated' ? { reason } : {};

    this.router.navigate(['/auth/login'], { queryParams }).then(success => {
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
      this.isAuthenticatedSubject.next(false);
      this.currentUserSubject.next(null);
      this.router.navigate(['/auth/login']);
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
    if (!token) {
      return null;
    }

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      // .NET Identity uses 'nameid' claim for NameIdentifier
      const userId =
        payload.nameid ||
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
        payload.sub ||
        payload.userId;
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
      this.isAuthenticatedSubject.next(true);
      this.currentUserSubject.next(authResult);

      console.log('🔒 Secure session established successfully');
    } catch (error) {
      console.error('🔒 Failed to establish secure session:', error);
      throw new Error('Session establishment failed');
    }
  }

  private setSession(authResult: AuthResponseDto): void {
    // Deprecated - use setSecureSession instead
    this.setSecureSession(authResult);
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
      const payload = JSON.parse(atob(token.split('.')[1]));
      const exp = payload.exp * 1000; // Convert to milliseconds
      return Date.now() >= exp;
    } catch (error) {
      return true; // If we can't parse the token, consider it expired
    }
  }
}
