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
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';
  
  private isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();
  
  private currentUserSubject = new BehaviorSubject<AuthResponseDto | null>(this.getCurrentUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private config: ConfigService, private router: Router) {
    
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
    if (token && this.isTokenExpired(token)) {
      this.logout();
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
    const email = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || payload['email'] || '';
    
    // Temporary user object while we fetch profile
    const tempUser = {
      token,
      email,
      firstName: '',
      lastName: ''
    };
    
    localStorage.setItem('currentUser', JSON.stringify(tempUser));
    this.currentUserSubject.next(tempUser);
    
    // Fetch user profile from API to get firstName/lastName
    this.http.get<any>(`${this.config.baseUrl}/profile`).subscribe({
      next: (response) => {
        // Handle response that contains data directly (not wrapped in success/data structure)
        if (response && (response.firstName || response.lastName)) {
          const completeUser = {
            token,
            email,
            firstName: response.firstName || '',
            lastName: response.lastName || ''
          };
          
          localStorage.setItem('currentUser', JSON.stringify(completeUser));
          this.currentUserSubject.next(completeUser);
        } else {
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
        token,
        email,
        firstName,
        lastName
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
    
    // Navigate to login page after logout
    console.log('Navigating to login page after logout');
    this.router.navigate(['/login']).then(success => {
      if (success) {
        console.log('✅ Successfully navigated to login page');
      } else {
        console.error('❌ Failed to navigate to login page');
      }
    });
  }

  // Public method to force clear all auth data - useful for debugging
  forceLogout(): void {
    console.log('Force logout called');
    localStorage.clear();
    this.isAuthenticatedSubject.next(false);
    this.currentUserSubject.next(null);
    console.log('All localStorage cleared');
    
    // Navigate to login page after force logout
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    const hasToken = this.hasToken();
    console.log('isAuthenticated() called - has token:', hasToken);
    if (hasToken) {
      const token = this.getToken();
      console.log('Token length:', token?.length);
      console.log('Token preview:', token?.substring(0, 50) + '...');
    }
    return hasToken;
  }

  getCurrentUserId(): string | null {
    const token = this.getToken();
    if (!token) {return null;}
    
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      // .NET Identity uses 'nameid' claim for NameIdentifier
      const userId = payload.nameid || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || payload.sub || payload.userId;
      console.log('Extracted user ID from token:', userId);
      return userId || null;
    } catch (error) {
      console.error('Failed to extract user ID from token:', error);
      return null;
    }
  }

  private setSession(authResult: AuthResponseDto): void {
    console.log('Setting auth session:', authResult);
    localStorage.setItem(this.TOKEN_KEY, authResult.token);
    localStorage.setItem('currentUser', JSON.stringify(authResult));
    // Clean up old storage keys
    localStorage.removeItem('current_user');
    this.isAuthenticatedSubject.next(true);
    this.currentUserSubject.next(authResult);
  }

  private hasToken(): boolean {
    const token = localStorage.getItem(this.TOKEN_KEY);
    console.log('hasToken() check - TOKEN_KEY:', this.TOKEN_KEY);
    console.log('hasToken() check - token exists:', !!token);
    console.log('hasToken() check - token length:', token?.length);
    
    if (!token) {
      console.log('hasToken() - No token found in localStorage');
      return false;
    }
    
    const isExpired = this.isTokenExpired(token);
    console.log('hasToken() check - token expired:', isExpired);
    
    if (isExpired) {
      console.warn('Auth token has expired');
      this.logout();
      return false;
    }
    
    console.log('hasToken() - Token is valid and not expired');
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
