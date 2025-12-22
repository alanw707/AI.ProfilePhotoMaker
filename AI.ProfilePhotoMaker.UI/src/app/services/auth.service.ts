import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  BehaviorSubject,
  map,
  Observable,
  catchError,
  of,
  shareReplay,
  finalize,
  switchMap,
} from 'rxjs';
import { Router } from '@angular/router';
import { ConfigService } from './config.service';
import { environment } from '../../environments/environment';

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
  ageConfirmed: boolean;
  turnstileToken?: string;
}

export interface AuthResponseDto {
  token: string;
  email: string;
  firstName: string;
  lastName: string;
}

export interface ProfileCompletionDto {
  firstName: string;
  lastName: string;
  gender: string;
  ethnicity: string;
  ageConfirmed: boolean;
}

export interface ProfileCompletionCheckDto {
  isCompleted: boolean;
  hasFirstName: boolean;
  hasLastName: boolean;
  hasGender: boolean;
  hasEthnicity: boolean;
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

export interface AccountStatusResponse {
  success: boolean;
  data: { emailConfirmed: boolean };
  error: { code: string; message: string } | null;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';

  private _isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasValidSession());
  public isAuthenticated$ = this._isAuthenticatedSubject.asObservable();

  private _currentUserSubject = new BehaviorSubject<AuthResponseDto | null>(this.getCurrentUser());
  public currentUser$ = this._currentUserSubject.asObservable();

  // De-duplication holder for profile completion checks
  private _profileStatusInFlight$?: Observable<ProfileCompletionCheckDto>;
  // De-duplication holder for session validation checks
  private _validateSessionInFlight$?: Observable<any>;
  // De-duplication holder for account status checks
  private _accountStatusInFlight$?: Observable<AccountStatusResponse>;

  constructor(
    private _http: HttpClient,
    private _config: ConfigService,
    private _router: Router
  ) {
    // Initialize with secure session check (only on protected routes)
    this.initializeSecureAuth();
  }

  /**
   * Initialize secure authentication with improved session management
   */
  private _sessionProbed = false;

  private initializeSecureAuth(): void {
    // Only probe on protected areas; skip on the public home page
    const path = window.location.pathname || '';
    if (this._isProtectedPath(path)) {
      this.probeSession();
    }
  }

  // Idempotent probe; safe to call multiple times
  public probeSession(): void {
    if (this._sessionProbed) {
      return;
    }
    this._sessionProbed = true;

    this.validateSession()
      .pipe(
        map(() => true),
        catchError(() => of(false))
      )
      .subscribe({
        next: (isAuth: boolean) => {
          this._isAuthenticatedSubject.next(isAuth);
          if (isAuth) {
            // If we don't have a hydrated user yet, fetch profile details
            const current = this._currentUserSubject.value;
            const needsHydration = !current || (!current.firstName && !current.lastName);
            if (needsHydration) {
              this.hydrateUserFromProfile();
            }
          }
        },
      });
  }

  public probeSessionForUrl(url: string): void {
    if (this._sessionProbed) {
      return;
    }
    const path = url.split('?')[0];
    if (this._isProtectedPath(path)) {
      this.probeSession();
    }
  }

  private _isProtectedPath(path: string): boolean {
    return path.startsWith('/app') || path.startsWith('/account') || path.startsWith('/admin');
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

  private shouldSetCookieForDevFlow(): boolean {
    if (environment.production) {
      return false;
    }

    // Extra safeguard: only run this helper on local/ngrok/docker-like origins.
    const host = window.location.hostname || '';
    return (
      host.includes('localhost') ||
      host.includes('127.0.0.1') ||
      host.includes('ngrok') ||
      environment.name === 'docker-local'
    );
  }

  private async setCookieForDevFlow(token: string): Promise<void> {
    const setCookieUrl = this._config.getFullUrl('/auth/set-cookie');

    try {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 2500);

      await fetch(setCookieUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token }),
        credentials: 'include',
        signal: controller.signal,
      });

      clearTimeout(timeout);
    } catch {
      // Best-effort only; do not block login/signup if cookie setting fails.
    }
  }

  handleOAuthCallback(_token: string, _expiration?: string): void {
    // Token is handled by HttpOnly cookie; update auth state and hydrate minimal user
    this.validateSession()
      .pipe(
        map(() => true),
        catchError(() => of(false))
      )
      .subscribe({
        next: isAuth => {
          this._isAuthenticatedSubject.next(isAuth);
          if (isAuth) {
            // Seed a minimal user so header switches from Guest immediately
            const existing = this.getCurrentUser();
            const placeholder: AuthResponseDto = existing || {
              token: '',
              email: '',
              firstName: '',
              lastName: '',
            };
            localStorage.setItem('currentUser', JSON.stringify(placeholder));
            this._currentUserSubject.next(placeholder);

            // Hydrate display name from server profile
            this.hydrateUserFromProfile();
          }
        },
      });
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

  // Fetch profile details and update current user state/localStorage
  private _profileHydrationInFlight = false;
  private hydrateUserFromProfile(): void {
    if (this._profileHydrationInFlight) {
      return;
    }
    this._profileHydrationInFlight = true;

    this._http
      .get<{ firstName?: string; lastName?: string }>(this._config.buildApiEndpoint('profile'))
      .pipe(finalize(() => (this._profileHydrationInFlight = false)))
      .subscribe({
        next: resp => {
          const prior = this.getCurrentUser() || {
            token: '',
            email: '',
            firstName: '',
            lastName: '',
          };
          const updated: AuthResponseDto = {
            token: prior.token,
            email: prior.email,
            firstName: resp.firstName || prior.firstName,
            lastName: resp.lastName || prior.lastName,
          };
          localStorage.setItem('currentUser', JSON.stringify(updated));
          this._currentUserSubject.next(updated);
        },
        error: () => {
          // Non-fatal; keep placeholder
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
    return this._http
      .post<ApiAuthResponseDto>(this._config.authLoginUrl, credentials, { withCredentials: true })
      .pipe(
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
        switchMap(async response => {
          // In development only, ensure cookie is set via same-origin endpoint (proxy)
          if (response.token && this.shouldSetCookieForDevFlow()) {
            await this.setCookieForDevFlow(response.token);
          }
          this.setSecureSession(response);
          return response;
        })
      );
  }

  register(userData: RegisterDto): Observable<AuthResponseDto> {
    return this._http
      .post<ApiAuthResponseDto>(this._config.authRegisterUrl, userData, { withCredentials: true })
      .pipe(
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
        switchMap(async response => {
          if (response.token && this.shouldSetCookieForDevFlow()) {
            await this.setCookieForDevFlow(response.token);
          }
          this.setSecureSession(response);
          return response;
        })
      );
  }

  /**
   * Enhanced logout with secure session cleanup
   */
  logout(redirectTo: 'login' | 'home' = 'login'): void {
    this.secureLogout('user_initiated', redirectTo);
  }

  /**
   * Secure logout with reason tracking and proper cleanup
   */
  private secureLogout(reason: string, redirectTo: 'login' | 'home' = 'login'): void {
    console.log(`🔒 Secure logout initiated - reason: ${reason}`);

    try {
      // Best-effort: clear the server-side auth cookie (HttpOnly JWT cookie).
      // Don't block logout UX if this fails.
      try {
        this._http
          .post(this._config.buildApiEndpoint('auth/logout'), {}, { withCredentials: true })
          .pipe(
            catchError(error => {
              console.warn('🔒 Logout cookie clear request failed:', error);
              return of(null);
            })
          )
          .subscribe();
      } catch (error) {
        console.warn('🔒 Logout cookie clear request failed to start:', error);
      }

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

      // Navigate after logout
      if (redirectTo === 'home') {
        this.navigateToHome();
      } else {
        this.navigateToLogin(reason);
      }

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
        this._router.navigate([redirectTo === 'home' ? '/' : '/auth/login']);
      } catch (navigationError) {
        console.error('🔒 Error navigating to login:', navigationError);
        // Ultimate fallback - force page reload
        window.location.href = redirectTo === 'home' ? '/' : '/auth/login';
      }
    }
  }

  /**
   * Clear cached auth state without navigating (useful for guest guard checks).
   */
  public clearAuthCache(): void {
    this.clearAllAuthData();
    this._isAuthenticatedSubject.next(false);
    this._currentUserSubject.next(null);
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
    try {
      const navigationResult = this._router.navigate(['/auth/login'], { queryParams });

      Promise.resolve(navigationResult)
        .then(success => {
          if (success === false) {
            console.error('❌ Failed to navigate to login page');
            window.location.href = '/auth/login';
          } else {
            console.log('✅ Successfully navigated to login page');
          }
        })
        .catch(err => {
          console.error('❌ Error navigating to login page:', err);
          window.location.href = '/auth/login';
        });
    } catch (err) {
      console.error('❌ Failed to navigate to login page', err);
      window.location.href = '/auth/login';
    }
  }

  private navigateToHome(): void {
    try {
      const navigationResult = this._router.navigate(['/']);
      Promise.resolve(navigationResult)
        .then(success => {
          if (success === false) {
            console.error('❌ Failed to navigate to home page');
            window.location.href = '/';
          }
        })
        .catch(err => {
          console.error('❌ Error navigating to home page:', err);
          window.location.href = '/';
        });
    } catch (err) {
      console.error('❌ Failed to navigate to home page', err);
      window.location.href = '/';
    }
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
    return null;
  }

  isAuthenticated(): boolean {
    return this._isAuthenticatedSubject.value;
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
      // Do not store token; rely on HttpOnly cookie set by API
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
    return false;
  }

  private hasValidSession(): boolean {
    // Quick check: if we have user data in localStorage, likely authenticated
    // This prevents false logouts on page refresh while probeSession() runs async
    const user = this.getCurrentUser();
    return user !== null && !!(user.email || user.firstName || user.lastName);
  }

  public updateCachedUserProfile(update: { firstName?: string; lastName?: string; email?: string }): void {
    try {
      const current = this.getCurrentUser() || this._currentUserSubject.value;
      const base: AuthResponseDto = {
        token: current?.token || '',
        email: current?.email || '',
        firstName: current?.firstName || '',
        lastName: current?.lastName || '',
      };

      const nextUser: AuthResponseDto = {
        ...base,
        email: update.email ?? base.email,
        firstName: update.firstName ?? base.firstName,
        lastName: update.lastName ?? base.lastName,
      };

      localStorage.setItem('currentUser', JSON.stringify(nextUser));
      this._currentUserSubject.next(nextUser);
    } catch (error) {
      console.error('Failed to update cached user profile:', error);
    }
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

  // Profile Completion Methods
  validateSession(): Observable<any> {
    // De-duplicate in-flight requests to avoid multiple consecutive calls
    if (this._validateSessionInFlight$) {
      return this._validateSessionInFlight$;
    }

    const req$ = this._http
      .get(this._config.buildApiEndpoint('auth/validate-session'), {
        responseType: 'text' as any,
        withCredentials: true,
      })
      .pipe(
        shareReplay(1),
        // Clear the in-flight reference when it completes or errors
        finalize(() => (this._validateSessionInFlight$ = undefined))
      );

    this._validateSessionInFlight$ = req$;
    return req$;
  }

  checkProfileCompletion(): Observable<ProfileCompletionCheckDto> {
    // De-duplicate in-flight requests to avoid multiple consecutive calls
    if (this._profileStatusInFlight$) {
      return this._profileStatusInFlight$;
    }

    const req$ = this._http
      .get<ProfileCompletionCheckDto>(
        this._config.buildApiEndpoint('auth/profile-completion-status')
      )
      .pipe(
        // Share the result among subscribers and cache the latest
        shareReplay(1),
        // Clear the in-flight reference when it completes or errors
        finalize(() => (this._profileStatusInFlight$ = undefined))
      );

    this._profileStatusInFlight$ = req$;
    return req$;
  }

  completeProfile(
    profileData: ProfileCompletionDto
  ): Observable<{ success: boolean; message: string }> {
    return this._http.post<{ success: boolean; message: string }>(
      this._config.buildApiEndpoint('auth/complete-profile'),
      profileData
    );
  }

  getAccountStatus(): Observable<AccountStatusResponse> {
    if (this._accountStatusInFlight$) {
      return this._accountStatusInFlight$;
    }

    const req$ = this._http
      .get<AccountStatusResponse>(this._config.buildApiEndpoint('auth/account-status'))
      .pipe(
        shareReplay(1),
        finalize(() => (this._accountStatusInFlight$ = undefined))
      );

    this._accountStatusInFlight$ = req$;
    return req$;
  }

  resendConfirmationEmail(): Observable<AccountStatusResponse> {
    return this._http.post<AccountStatusResponse>(
      this._config.buildApiEndpoint('auth/resend-confirmation-email'),
      {}
    );
  }

  devConfirmEmail(): Observable<AccountStatusResponse> {
    return this._http.post<AccountStatusResponse>(this._config.buildApiEndpoint('auth/dev/confirm-email'), {});
  }

  confirmEmail(userId: string, token: string): Observable<AccountStatusResponse> {
    return this._http.post<AccountStatusResponse>(this._config.buildApiEndpoint('auth/confirm-email'), {
      userId,
      token,
    });
  }
}
