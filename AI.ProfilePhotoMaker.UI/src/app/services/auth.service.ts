import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, map } from 'rxjs';

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
  private readonly API_BASE_URL = 'https://localhost:7173/api'; // Updated to match API launch settings
  private readonly TOKEN_KEY = 'auth_token';
  
  private isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();
  
  private currentUserSubject = new BehaviorSubject<AuthResponseDto | null>(this.getCurrentUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient) {}

  login(credentials: LoginDto): Observable<AuthResponseDto> {
    return this.http.post<ApiAuthResponseDto>(`${this.API_BASE_URL}/auth/login`, credentials)
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

  register(userData: RegisterDto): Observable<AuthResponseDto> {
    return this.http.post<ApiAuthResponseDto>(`${this.API_BASE_URL}/auth/register`, userData)
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
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem('current_user');
    this.isAuthenticatedSubject.next(false);
    this.currentUserSubject.next(null);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return this.hasToken();
  }

  private setSession(authResult: AuthResponseDto): void {
    localStorage.setItem(this.TOKEN_KEY, authResult.token);
    localStorage.setItem('current_user', JSON.stringify(authResult));
    this.isAuthenticatedSubject.next(true);
    this.currentUserSubject.next(authResult);
  }

  private hasToken(): boolean {
    const token = localStorage.getItem(this.TOKEN_KEY);
    return !!token && !this.isTokenExpired(token);
  }

  private getCurrentUser(): AuthResponseDto | null {
    const userStr = localStorage.getItem('current_user');
    return userStr ? JSON.parse(userStr) : null;
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
