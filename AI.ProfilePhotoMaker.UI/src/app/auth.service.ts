import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';
import { Router } from '@angular/router';

export interface User {
  email: string;
  firstName: string;
  lastName: string;
}

export interface AuthResponse {
  isSuccess: boolean;
  message: string;
  token: string;
  expiration: string;
  email?: string;
  firstName?: string;
  lastName?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = 'https://057a-71-38-148-86.ngrok-free.app/api';
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private http: HttpClient,
    private router: Router
  ) {
    // Check if user is already logged in
    this.loadUserFromStorage();
  }

  private loadUserFromStorage(): void {
    const token = localStorage.getItem('authToken');
    const userStr = localStorage.getItem('currentUser');
    
    if (token && userStr && this.isTokenValid(token)) {
      const user: User = JSON.parse(userStr);
      this.currentUserSubject.next(user);
    } else {
      this.logout();
    }
  }

  private isTokenValid(token: string): boolean {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const exp = payload.exp * 1000;
      return Date.now() < exp;
    } catch {
      return false;
    }
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/register`, request)
      .pipe(
        tap(response => {
          if (response.isSuccess) {
            this.setUserSession(response);
          }
        })
      );
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/login`, request)
      .pipe(
        tap(response => {
          if (response.isSuccess) {
            this.setUserSession(response);
          }
        })
      );
  }

  externalLogin(provider: string, returnUrl: string = ''): Observable<{redirectUrl: string}> {
    return this.http.get<{redirectUrl: string}>(`${this.apiUrl}/auth/external-login/${provider}?returnUrl=${returnUrl}`);
  }

  processExternalLoginCallback(provider: string, code: string, state?: string): Observable<AuthResponse> {
    const request = { provider, code, state };
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/external-login/callback`, request)
      .pipe(
        tap(response => {
          if (response.isSuccess) {
            this.setUserSession(response);
          }
        })
      );
  }

  private setUserSession(response: AuthResponse): void {
    localStorage.setItem('authToken', response.token);
    localStorage.setItem('tokenExpiration', response.expiration);
    
    if (response.email && response.firstName && response.lastName) {
      const user: User = {
        email: response.email,
        firstName: response.firstName,
        lastName: response.lastName
      };
      localStorage.setItem('currentUser', JSON.stringify(user));
      this.currentUserSubject.next(user);
    }
  }

  logout(): void {
    localStorage.removeItem('authToken');
    localStorage.removeItem('tokenExpiration');
    localStorage.removeItem('currentUser');
    this.currentUserSubject.next(null);
    this.router.navigate(['/login']);
  }

  isAuthenticated(): boolean {
    const token = localStorage.getItem('authToken');
    return token ? this.isTokenValid(token) : false;
  }

  getToken(): string | null {
    return localStorage.getItem('authToken');
  }

  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  getAuthHeaders(): HttpHeaders {
    const token = this.getToken();
    return new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });
  }
}
