import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterTestingModule } from '@angular/router/testing';

import { AuthService } from '../services/auth.service';
import { ConfigService } from '../services/config.service';
import { authGuard } from '../guards/auth.guard';
import { guestGuard } from '../guards/guest.guard';

// Mock components for routing tests
@Component({ template: 'Mock Dashboard' })
class MockDashboardComponent {}

@Component({ template: 'Mock Login' })
class MockLoginComponent {}

@Component({ template: 'Mock Register' })
class MockRegisterComponent {}

describe('Authentication Flow Integration Tests', () => {
  let authService: AuthService;
  let router: Router;
  let location: Location;
  let httpMock: HttpTestingController;
  let configService: ConfigService;

  beforeEach(async () => {
    // Clear localStorage before each test
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [
        HttpClientTestingModule,
        FormsModule,
        CommonModule,
        RouterTestingModule.withRoutes([
          { path: 'login', component: MockLoginComponent, canActivate: [guestGuard] },
          { path: 'register', component: MockRegisterComponent, canActivate: [guestGuard] },
          { path: 'dashboard', component: MockDashboardComponent, canActivate: [authGuard] },
          { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
        ]),
      ],
      providers: [AuthService, ConfigService],
    }).compileComponents();

    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    location = TestBed.inject(Location);
    httpMock = TestBed.inject(HttpTestingController);
    configService = TestBed.inject(ConfigService);

    // Mock ConfigService URLs
    spyOn(configService, 'authLoginUrl').and.returnValue('http://localhost:5035/api/auth/login');
    spyOn(configService, 'authRegisterUrl').and.returnValue(
      'http://localhost:5035/api/auth/register'
    );
    spyOn(configService, 'baseUrl').and.returnValue('http://localhost:5035');
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  describe('User Registration Flow', () => {
    it('should complete full registration workflow', async () => {
      // 1. Navigate to register page
      await router.navigate(['/register']);
      expect(location.path()).toBe('/register');

      // 2. Attempt registration
      const registerData = {
        email: 'test@example.com',
        password: 'Password123!',
        firstName: 'John',
        lastName: 'Doe',
        gender: 'male',
        ethnicity: 'caucasian',
      };

      const registerRequest = authService.register(registerData);

      // 3. Mock successful API response
      const mockResponse = {
        isSuccess: true,
        message: 'Registration successful',
        token: 'mock-jwt-token',
        expiration: new Date(Date.now() + 3600000).toISOString(),
        email: 'test@example.com',
        firstName: 'John',
        lastName: 'Doe',
      };

      registerRequest.subscribe(response => {
        expect(response.token).toBe('mock-jwt-token');
        expect(response.email).toBe('test@example.com');
        expect(response.firstName).toBe('John');
        expect(response.lastName).toBe('Doe');
      });

      const req = httpMock.expectOne('http://localhost:5035/api/auth/register');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(registerData);
      req.flush(mockResponse);

      // 4. Verify authentication state
      expect(authService.isAuthenticated()).toBe(true);
      expect(localStorage.getItem('auth_token')).toBe('mock-jwt-token');
      expect(localStorage.getItem('currentUser')).toBeTruthy();

      // 5. Verify user data persistence
      const currentUser = JSON.parse(localStorage.getItem('currentUser') || '{}');
      expect(currentUser.email).toBe('test@example.com');
      expect(currentUser.firstName).toBe('John');
      expect(currentUser.lastName).toBe('Doe');
    });

    it('should handle registration errors gracefully', async () => {
      const registerData = {
        email: 'invalid@example.com',
        password: 'weak',
        firstName: 'John',
        lastName: 'Doe',
        gender: 'male',
        ethnicity: 'caucasian',
      };

      const registerRequest = authService.register(registerData);

      registerRequest.subscribe({
        next: () => fail('Should have thrown error'),
        error: error => {
          expect(error.message).toBe('Registration failed');
        },
      });

      const req = httpMock.expectOne('http://localhost:5035/api/auth/register');
      req.flush({ isSuccess: false, message: 'Registration failed' });

      expect(authService.isAuthenticated()).toBe(false);
      expect(localStorage.getItem('auth_token')).toBe(null);
    });
  });

  describe('User Login Flow', () => {
    it('should complete full login workflow', async () => {
      // 1. Navigate to login page
      await router.navigate(['/login']);
      expect(location.path()).toBe('/login');

      // 2. Attempt login
      const loginData = {
        email: 'test@example.com',
        password: 'Password123!',
      };

      const loginRequest = authService.login(loginData);

      // 3. Mock successful API response
      const mockResponse = {
        isSuccess: true,
        message: 'Login successful',
        token: 'mock-jwt-token',
        expiration: new Date(Date.now() + 3600000).toISOString(),
        email: 'test@example.com',
        firstName: 'John',
        lastName: 'Doe',
      };

      loginRequest.subscribe(response => {
        expect(response.token).toBe('mock-jwt-token');
        expect(response.email).toBe('test@example.com');
      });

      const req = httpMock.expectOne('http://localhost:5035/api/auth/login');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(loginData);
      req.flush(mockResponse);

      // 4. Verify authentication state
      expect(authService.isAuthenticated()).toBe(true);
      expect(localStorage.getItem('auth_token')).toBe('mock-jwt-token');
    });

    it('should handle login errors gracefully', async () => {
      const loginData = {
        email: 'invalid@example.com',
        password: 'wrongpassword',
      };

      const loginRequest = authService.login(loginData);

      loginRequest.subscribe({
        next: () => fail('Should have thrown error'),
        error: error => {
          expect(error.message).toBe('Invalid credentials');
        },
      });

      const req = httpMock.expectOne('http://localhost:5035/api/auth/login');
      req.flush({ isSuccess: false, message: 'Invalid credentials' });

      expect(authService.isAuthenticated()).toBe(false);
    });
  });

  describe('OAuth Authentication Flow', () => {
    it('should handle OAuth callback with complete user data', () => {
      // Simulate OAuth callback with JWT token containing user data
      const mockJwt =
        btoa(
          JSON.stringify({
            header: { alg: 'HS256', typ: 'JWT' },
          })
        ) +
        '.' +
        btoa(
          JSON.stringify({
            'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress':
              'oauth@example.com',
            'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname': 'OAuth',
            'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname': 'User',
            exp: Math.floor(Date.now() / 1000) + 3600,
          })
        ) +
        '.signature';

      authService.handleOAuthCallback(mockJwt);

      expect(authService.isAuthenticated()).toBe(true);
      expect(localStorage.getItem('auth_token')).toBe(mockJwt);

      const currentUser = JSON.parse(localStorage.getItem('currentUser') || '{}');
      expect(currentUser.email).toBe('oauth@example.com');
      expect(currentUser.firstName).toBe('OAuth');
      expect(currentUser.lastName).toBe('User');
    });

    it('should fetch user profile when JWT lacks complete user data', () => {
      // Simulate OAuth callback with JWT token lacking user data
      const mockJwt =
        btoa(
          JSON.stringify({
            header: { alg: 'HS256', typ: 'JWT' },
          })
        ) +
        '.' +
        btoa(
          JSON.stringify({
            'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress':
              'oauth@example.com',
            exp: Math.floor(Date.now() / 1000) + 3600,
          })
        ) +
        '.signature';

      authService.handleOAuthCallback(mockJwt);

      // Should make profile API call
      const req = httpMock.expectOne('http://localhost:5035/profile');
      expect(req.request.method).toBe('GET');

      req.flush({
        firstName: 'OAuth',
        lastName: 'User',
      });

      expect(authService.isAuthenticated()).toBe(true);

      const currentUser = JSON.parse(localStorage.getItem('currentUser') || '{}');
      expect(currentUser.email).toBe('oauth@example.com');
      expect(currentUser.firstName).toBe('OAuth');
      expect(currentUser.lastName).toBe('User');
    });
  });

  describe('Protected Route Access', () => {
    it('should redirect unauthenticated users to login', async () => {
      // Ensure user is not authenticated
      expect(authService.isAuthenticated()).toBe(false);

      // Try to navigate to protected route
      await router.navigate(['/dashboard']);

      // Should redirect to login
      expect(location.path()).toBe('/login');
    });

    it('should allow authenticated users to access protected routes', async () => {
      // Set up authenticated state
      const mockToken =
        btoa(
          JSON.stringify({
            header: { alg: 'HS256', typ: 'JWT' },
          })
        ) +
        '.' +
        btoa(
          JSON.stringify({
            exp: Math.floor(Date.now() / 1000) + 3600,
          })
        ) +
        '.signature';

      localStorage.setItem('auth_token', mockToken);
      localStorage.setItem(
        'currentUser',
        JSON.stringify({
          token: mockToken,
          email: 'test@example.com',
          firstName: 'John',
          lastName: 'Doe',
        })
      );

      // Navigate to protected route
      await router.navigate(['/dashboard']);

      // Should access the route
      expect(location.path()).toBe('/dashboard');
    });

    it('should redirect authenticated users away from guest routes', async () => {
      // Set up authenticated state
      const mockToken =
        btoa(
          JSON.stringify({
            header: { alg: 'HS256', typ: 'JWT' },
          })
        ) +
        '.' +
        btoa(
          JSON.stringify({
            exp: Math.floor(Date.now() / 1000) + 3600,
          })
        ) +
        '.signature';

      localStorage.setItem('auth_token', mockToken);
      localStorage.setItem(
        'currentUser',
        JSON.stringify({
          token: mockToken,
          email: 'test@example.com',
          firstName: 'John',
          lastName: 'Doe',
        })
      );

      // Try to navigate to guest route
      await router.navigate(['/login']);

      // Should redirect to dashboard
      expect(location.path()).toBe('/dashboard');
    });
  });

  describe('Session Management', () => {
    it('should maintain authentication state across page reloads', () => {
      // Simulate authentication
      const mockToken =
        btoa(
          JSON.stringify({
            header: { alg: 'HS256', typ: 'JWT' },
          })
        ) +
        '.' +
        btoa(
          JSON.stringify({
            exp: Math.floor(Date.now() / 1000) + 3600,
          })
        ) +
        '.signature';

      localStorage.setItem('auth_token', mockToken);
      localStorage.setItem(
        'currentUser',
        JSON.stringify({
          token: mockToken,
          email: 'test@example.com',
          firstName: 'John',
          lastName: 'Doe',
        })
      );

      // Create new AuthService instance (simulating page reload)
      const newAuthService = new AuthService(
        TestBed.inject(HttpClient),
        TestBed.inject(ConfigService),
        TestBed.inject(Router)
      );

      expect(newAuthService.isAuthenticated()).toBe(true);
    });

    it('should handle token expiration gracefully', () => {
      // Set up expired token
      const expiredToken =
        btoa(
          JSON.stringify({
            header: { alg: 'HS256', typ: 'JWT' },
          })
        ) +
        '.' +
        btoa(
          JSON.stringify({
            exp: Math.floor(Date.now() / 1000) - 3600, // Expired 1 hour ago
          })
        ) +
        '.signature';

      localStorage.setItem('auth_token', expiredToken);
      localStorage.setItem(
        'currentUser',
        JSON.stringify({
          token: expiredToken,
          email: 'test@example.com',
          firstName: 'John',
          lastName: 'Doe',
        })
      );

      // Check authentication - should be false due to expired token
      expect(authService.isAuthenticated()).toBe(false);
      expect(localStorage.getItem('auth_token')).toBe(null);
    });

    it('should complete logout workflow', async () => {
      // Set up authenticated state
      const mockToken =
        btoa(
          JSON.stringify({
            header: { alg: 'HS256', typ: 'JWT' },
          })
        ) +
        '.' +
        btoa(
          JSON.stringify({
            exp: Math.floor(Date.now() / 1000) + 3600,
          })
        ) +
        '.signature';

      localStorage.setItem('auth_token', mockToken);
      localStorage.setItem(
        'currentUser',
        JSON.stringify({
          token: mockToken,
          email: 'test@example.com',
          firstName: 'John',
          lastName: 'Doe',
        })
      );

      expect(authService.isAuthenticated()).toBe(true);

      // Logout
      authService.logout();

      // Verify cleanup
      expect(authService.isAuthenticated()).toBe(false);
      expect(localStorage.getItem('auth_token')).toBe(null);
      expect(localStorage.getItem('currentUser')).toBe(null);
      expect(location.path()).toBe('/login');
    });
  });

  describe('Error Recovery', () => {
    it('should handle network errors during authentication', async () => {
      const loginData = {
        email: 'test@example.com',
        password: 'Password123!',
      };

      const loginRequest = authService.login(loginData);

      loginRequest.subscribe({
        next: () => fail('Should have thrown error'),
        error: error => {
          expect(error).toBeDefined();
        },
      });

      const req = httpMock.expectOne('http://localhost:5035/api/auth/login');
      req.error(new ErrorEvent('Network error'));

      expect(authService.isAuthenticated()).toBe(false);
    });

    it('should handle corrupted localStorage gracefully', () => {
      // Simulate corrupted localStorage
      localStorage.setItem('auth_token', 'invalid-token');
      localStorage.setItem('currentUser', 'invalid-json');

      // Should not throw and should return false
      expect(authService.isAuthenticated()).toBe(false);
    });
  });

  describe('Observable State Management', () => {
    it('should emit authentication state changes', done => {
      const states: boolean[] = [];

      authService.isAuthenticated$.subscribe(isAuth => {
        states.push(isAuth);

        if (states.length === 2) {
          expect(states[0]).toBe(false); // Initial state
          expect(states[1]).toBe(true); // After login
          done();
        }
      });

      // Simulate login
      const mockToken =
        btoa(
          JSON.stringify({
            header: { alg: 'HS256', typ: 'JWT' },
          })
        ) +
        '.' +
        btoa(
          JSON.stringify({
            exp: Math.floor(Date.now() / 1000) + 3600,
          })
        ) +
        '.signature';

      localStorage.setItem('auth_token', mockToken);
      localStorage.setItem(
        'currentUser',
        JSON.stringify({
          token: mockToken,
          email: 'test@example.com',
          firstName: 'John',
          lastName: 'Doe',
        })
      );

      // Manually trigger state change
      authService['isAuthenticatedSubject'].next(true);
    });

    it('should emit current user changes', done => {
      const users: any[] = [];

      authService.currentUser$.subscribe(user => {
        users.push(user);

        if (users.length === 2) {
          expect(users[0]).toBe(null); // Initial state
          expect(users[1].email).toBe('test@example.com');
          done();
        }
      });

      // Simulate user data update
      const userData = {
        token: 'mock-token',
        email: 'test@example.com',
        firstName: 'John',
        lastName: 'Doe',
      };

      authService['currentUserSubject'].next(userData);
    });
  });
});
