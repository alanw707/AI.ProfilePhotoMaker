import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, map } from 'rxjs';
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
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';
  
  private isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();
  
  private currentUserSubject = new BehaviorSubject<AuthResponseDto | null>(this.getCurrentUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private config: ConfigService) {
    console.log('AuthService constructor - checking initial auth state');
    console.log('localStorage keys on init:', Object.keys(localStorage));
    console.log('TOKEN_KEY:', this.TOKEN_KEY);
    console.log('Current token:', localStorage.getItem(this.TOKEN_KEY));
    
    // Check immediately on service creation
    this.checkTokenValidity();
    
    // Check token validity periodically (every 5 minutes) - but only if we have a token
    setInterval(() => {
      if (localStorage.getItem(this.TOKEN_KEY)) {
        this.checkTokenValidity();
      }
    }, 5 * 60 * 1000);
  }
  
  private checkTokenValidity(): void {
    const token = localStorage.getItem(this.TOKEN_KEY);
    console.log('checkTokenValidity - token exists:', !!token);
    if (token && this.isTokenExpired(token)) {
      console.log('Token expired during check, logging out');
      this.logout();
    } else if (token) {
      console.log('Token is valid');
    }
  }

  handleOAuthCallback(token: string, expiration?: string): void {
    console.log('handleOAuthCallback called with token length:', token?.length);
    console.log('Expiration:', expiration);
    
    if (token) {
      // Store the token using consistent key
      localStorage.setItem(this.TOKEN_KEY, token);
      localStorage.setItem('authToken', token); // Keep both for compatibility
      if (expiration) {
        localStorage.setItem('tokenExpiration', expiration);
      }
      
      // Extract user info from token
      const user = this.extractUserFromToken(token);
      console.log('Extracted user from token:', user);
      
      if (user && user.firstName && user.lastName) {
        // Complete user data from JWT
        localStorage.setItem('currentUser', JSON.stringify(user));
        this.currentUserSubject.next(user);
        this.isAuthenticatedSubject.next(true);
        console.log('OAuth callback processed with complete user data from JWT');
      } else {
        // Incomplete JWT data, fetch from profile API
        console.log('Incomplete user data from JWT, fetching from profile API');
        this.isAuthenticatedSubject.next(true);
        
        // Fetch user profile data from API to get complete firstName/lastName
        this.fetchUserProfileForOAuth(token);
      }
    } else {
      console.error('No token provided to handleOAuthCallback');
    }
  }

  private fetchUserProfileForOAuth(token: string): void {
    // Create a minimal user object with just email for now
    const payload = JSON.parse(atob(token.split('.')[1]));
    const email = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || payload['email'] || '';
    
    // Temporary user object while we fetch profile
    const tempUser = {
      token: token,
      email: email,
      firstName: '',
      lastName: ''
    };
    
    localStorage.setItem('currentUser', JSON.stringify(tempUser));
    this.currentUserSubject.next(tempUser);
    
    // Fetch user profile from API to get firstName/lastName
    console.log('Fetching user profile from API for complete user data');
    this.http.get<any>(`${this.config.baseUrl}/profile`).subscribe({
      next: (response) => {
        console.log('Full profile API response:', response);
        console.log('Response firstName:', response.firstName);
        console.log('Response lastName:', response.lastName);
        
        // Handle response that contains data directly (not wrapped in success/data structure)
        if (response && (response.firstName || response.lastName)) {
          const completeUser = {
            token: token,
            email: email,
            firstName: response.firstName || '',
            lastName: response.lastName || ''
          };
          
          console.log('SUCCESS: Updated user with profile data:', completeUser);
          localStorage.setItem('currentUser', JSON.stringify(completeUser));
          this.currentUserSubject.next(completeUser);
        } else {
          console.log('FALLBACK: Profile API response does not contain firstName/lastName');
          console.log('Response keys:', Object.keys(response || {}));
          // Keep the temp user with email username as firstName
          const fallbackUser = {
            ...tempUser,
            firstName: email.split('@')[0]
          };
          localStorage.setItem('currentUser', JSON.stringify(fallbackUser));
          this.currentUserSubject.next(fallbackUser);
        }
      },
      error: (error) => {
        console.error('Failed to fetch user profile:', error);
        // Fallback to email username
        const fallbackUser = {
          ...tempUser,
          firstName: email.split('@')[0]
        };
        localStorage.setItem('currentUser', JSON.stringify(fallbackUser));
        this.currentUserSubject.next(fallbackUser);
      }
    });
  }

  private extractUserFromToken(token: string): AuthResponseDto | null {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      console.log('JWT Payload for debugging:', payload);
      console.log('Available payload keys:', Object.keys(payload));
      
      // Check .NET ClaimTypes standard URIs first, then fallback to short names
      const email = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || 
                   payload['email'] || '';
      
      const firstName = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'] || 
                       payload['given_name'] || 
                       payload['givenname'] || 
                       payload['firstName'] || '';
                       
      const lastName = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'] || 
                      payload['family_name'] || 
                      payload['surname'] || 
                      payload['lastName'] || '';
      
      console.log('Extracted values - email:', email, 'firstName:', firstName, 'lastName:', lastName);
      
      // If no firstName/lastName in JWT, return null to force profile API lookup
      if (!firstName && !lastName) {
        console.log('No firstName/lastName in JWT, returning null to force profile lookup');
        return null;
      }
      
      return {
        token: token,
        email: email,
        firstName: firstName,
        lastName: lastName
      };
    } catch (error) {
      console.error('Failed to extract user from token:', error);
      return null;
    }
  }

  login(credentials: LoginDto): Observable<AuthResponseDto> {
    return this.http.post<ApiAuthResponseDto>(this.config.authLoginUrl, credentials)
      .pipe(
        map(apiResponse => {
          console.log('Login API response:', apiResponse);
          if (!apiResponse.isSuccess) {
            throw new Error(apiResponse.message);
          }
          // Use API response data directly (firstName/lastName come from the API response, not JWT)
          const authResponse = {
            token: apiResponse.token,
            email: apiResponse.email || '',
            firstName: apiResponse.firstName || '',
            lastName: apiResponse.lastName || ''
          } as AuthResponseDto;
          console.log('Mapped auth response:', authResponse);
          return authResponse;
        }),
        tap(response => this.setSession(response))
      );
  }

  register(userData: RegisterDto): Observable<AuthResponseDto> {
    return this.http.post<ApiAuthResponseDto>(this.config.authRegisterUrl, userData)
      .pipe(
        map(apiResponse => {
          if (!apiResponse.isSuccess) {
            throw new Error(apiResponse.message);
          }
          return {
            token: apiResponse.token,
            email: apiResponse.email || '',
            firstName: apiResponse.firstName || '',
            lastName: apiResponse.lastName || ''
          } as AuthResponseDto;
        }),
        tap(response => this.setSession(response))
      );
  }

  logout(): void {
    console.log('Logging out - clearing all auth data');
    console.log('Before logout - localStorage keys:', Object.keys(localStorage));
    
    // Clear all possible auth-related keys
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem('authToken');
    localStorage.removeItem('tokenExpiration');
    localStorage.removeItem('current_user');
    localStorage.removeItem('currentUser');
    
    // Clear any other potential auth keys
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    localStorage.removeItem('auth');
    
    // Update subjects
    this.isAuthenticatedSubject.next(false);
    this.currentUserSubject.next(null);
    
    console.log('After logout - localStorage keys:', Object.keys(localStorage));
    console.log('isAuthenticated after logout:', this.isAuthenticated());
  }

  // Public method to force clear all auth data - useful for debugging
  forceLogout(): void {
    console.log('Force logout called');
    localStorage.clear();
    this.isAuthenticatedSubject.next(false);
    this.currentUserSubject.next(null);
    console.log('All localStorage cleared');
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return this.hasToken();
  }

  private setSession(authResult: AuthResponseDto): void {
    console.log('Setting auth session:', authResult);
    localStorage.setItem(this.TOKEN_KEY, authResult.token);
    localStorage.setItem('currentUser', JSON.stringify(authResult));
    // Clean up old storage keys
    localStorage.removeItem('current_user');
    this.isAuthenticatedSubject.next(true);
    this.currentUserSubject.next(authResult);
    console.log('Auth session set, isAuthenticated:', true);
  }

  private hasToken(): boolean {
    const token = localStorage.getItem(this.TOKEN_KEY);
    console.log('hasToken check - token exists:', !!token);
    if (!token) return false;
    
    const isExpired = this.isTokenExpired(token);
    console.log('Token expired:', isExpired);
    
    if (isExpired) {
      console.log('Token expired, logging out');
      this.logout();
      return false;
    }
    
    console.log('Token is valid');
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
