import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';

import { AuthResponseDto, AuthService, LoginDto, RegisterDto } from './auth.service';
import { ConfigService } from './config.service';

/**
 * AuthService Test Suite
 *
 * Simplified tests that match the actual service structure.
 * Tests critical authentication functionality including JWT token management and login/logout flows.
 */
describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let mockRouter: jasmine.SpyObj<Router>;

  beforeEach(() => {
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockRouter.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService, { provide: Router, useValue: mockRouter }, ConfigService],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    const pendingRoleRequests = httpMock.match('/api/auth/user-roles');
    pendingRoleRequests.forEach(req => req.flush({ success: true, data: { roles: [] } }));
    httpMock.verify();
    localStorage.clear();
  });

  describe('Service Initialization', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should start with unauthenticated state when no token exists', () => {
      expect(service.isAuthenticated()).toBeFalse();
      expect(service.getToken()).toBeNull();
    });
  });

  describe('Login Functionality', () => {
    it('should login with valid credentials', done => {
      const loginData: LoginDto = {
        email: 'test@example.com',
        password: 'password123',
        ageConfirmed: true,
      };
      const mockResponse = {
        isSuccess: true,
        message: 'Login successful',
        token: 'jwt-token',
        expiration: '2025-01-01T00:00:00Z',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
      };

      service.login(loginData).subscribe(response => {
        expect(response).toEqual(
          jasmine.objectContaining({
            token: 'jwt-token',
            email: 'test@example.com',
          })
        );
        expect(service.isAuthenticated()).toBeTrue();
        // Token is not stored client-side; verify secure session via currentUser
        const stored = localStorage.getItem('currentUser');
        expect(stored).not.toBeNull();
        if (stored) {
          const user = JSON.parse(stored);
          expect(user.email).toBe('test@example.com');
        }
        done();
      });

      const req = httpMock.expectOne('/api/auth/login');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(loginData);
      req.flush(mockResponse);
    });

    it('should handle login errors', done => {
      const loginData: LoginDto = {
        email: 'test@example.com',
        password: 'wrong-password',
        ageConfirmed: true,
      };
      const errorResponse = {
        isSuccess: false,
        message: 'Invalid credentials',
        token: '',
        expiration: '',
      };

      service.login(loginData).subscribe({
        next: () => fail('Expected error, but got success'),
        error: _err => {
          expect(service.isAuthenticated()).toBeFalse();
          done();
        },
      });

      const req = httpMock.expectOne('/api/auth/login');
      req.flush(errorResponse);
    });

    it('should persist currentUser on successful login', done => {
      const loginData: LoginDto = {
        email: 'test@example.com',
        password: 'password123',
        ageConfirmed: true,
      };
      const mockResponse = {
        isSuccess: true,
        message: 'Login successful',
        token: 'jwt-token',
        expiration: '2025-01-01T00:00:00Z',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
      };

      service.login(loginData).subscribe(() => {
        // Token is stored server-side in HttpOnly cookie; client persists currentUser only
        expect(localStorage.getItem('auth_token')).toBeNull();
        const stored = localStorage.getItem('currentUser');
        expect(stored).not.toBeNull();
        if (stored) {
          const user = JSON.parse(stored);
          expect(user.email).toBe('test@example.com');
        }
        done();
      });

      const req = httpMock.expectOne('/api/auth/login');
      req.flush(mockResponse);
    });

    it('should emit authentication state changes', done => {
      let emissionCount = 0;
      service.isAuthenticated$.subscribe(isAuth => {
        emissionCount++;
        if (emissionCount === 2) {
          // Skip initial false emission
          expect(isAuth).toBeTrue();
          done();
        }
      });

      const loginData: LoginDto = {
        email: 'test@example.com',
        password: 'password123',
        ageConfirmed: true,
      };
      const mockResponse = {
        isSuccess: true,
        message: 'Login successful',
        token: 'jwt-token',
        expiration: '2025-01-01T00:00:00Z',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
      };

      service.login(loginData).subscribe();

      const req = httpMock.expectOne('/api/auth/login');
      req.flush(mockResponse);
    });
  });

  describe('Registration', () => {
    it('should register new user', done => {
      const registrationData: RegisterDto = {
        email: 'newuser@example.com',
        password: 'password123',
        firstName: 'New',
        lastName: 'User',
        gender: 'prefer-not-to-say',
        ethnicity: 'prefer-not-to-say',
        ageConfirmed: true,
      };
      const mockResponse = {
        isSuccess: true,
        message: 'Registration successful',
        token: 'jwt-token',
        expiration: '2025-01-01T00:00:00Z',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
      };

      service.register(registrationData).subscribe(response => {
        expect(response).toEqual(
          jasmine.objectContaining({
            token: 'jwt-token',
            email: 'newuser@example.com',
          })
        );
        expect(service.isAuthenticated()).toBeTrue();
        done();
      });

      const req = httpMock.expectOne('/api/auth/register');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(registrationData);
      req.flush(mockResponse);
    });

    it('should handle registration errors', done => {
      const registrationData: RegisterDto = {
        email: 'invalid-email',
        password: '123',
        firstName: '',
        lastName: '',
        gender: '',
        ethnicity: '',
        ageConfirmed: false,
      };
      const errorResponse = {
        isSuccess: false,
        message: 'Validation failed',
        token: '',
        expiration: '',
      };

      service.register(registrationData).subscribe({
        next: () => fail('Expected error, but got success'),
        error: _err => {
          expect(service.isAuthenticated()).toBeFalse();
          done();
        },
      });

      const req = httpMock.expectOne('/api/auth/register');
      req.flush(errorResponse);
    });
  });

  describe('Logout Functionality', () => {
    beforeEach(() => {
      // Set up authenticated state
      localStorage.setItem('auth_token', 'jwt-token');
      (service as any)['_currentUserSubject'].next({
        token: 'jwt-token',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
      });
      (service as any)['_isAuthenticatedSubject'].next(true);
    });

    it('should logout and clear session data', () => {
      service.logout();
      const req = httpMock.expectOne('/api/auth/logout');
      expect(req.request.method).toBe('POST');
      req.flush({ success: true });

      expect(localStorage.getItem('auth_token')).toBeNull();
      expect(service.isAuthenticated()).toBeFalse();
    });

    it('should emit authentication state change on logout', done => {
      service.isAuthenticated$.subscribe(isAuth => {
        if (!isAuth) {
          expect(isAuth).toBeFalse();
          done();
        }
      });

      service.logout();
      const req = httpMock.expectOne('/api/auth/logout');
      expect(req.request.method).toBe('POST');
      req.flush({ success: true });
    });

    it('should navigate to login page after logout', () => {
      service.logout();
      const req = httpMock.expectOne('/api/auth/logout');
      expect(req.request.method).toBe('POST');
      req.flush({ success: true });
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: {} });
    });
  });

  describe('Token Management', () => {
    it('should return null for getToken in cookie-based auth', () => {
      localStorage.setItem('auth_token', 'stored-token');
      expect(service.getToken()).toBeNull();
    });

    it('should return null when no token exists', () => {
      expect(service.getToken()).toBeNull();
    });

    it('should report authentication state via subject', () => {
      expect(service.isAuthenticated()).toBeFalse();
      (service as any)['_isAuthenticatedSubject'].next(true);
      expect(service.isAuthenticated()).toBeTrue();
    });
  });

  describe('User Profile Management', () => {
    beforeEach(() => {
      const mockUser: AuthResponseDto = {
        token: 'jwt-token',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
      };
      (service as any)['_currentUserSubject'].next(mockUser);
    });

    it('should get current user from observable', done => {
      service.currentUser$.subscribe(user => {
        if (user) {
          expect(user).toEqual(
            jasmine.objectContaining({
              email: 'test@example.com',
              firstName: 'Test',
              lastName: 'User',
            })
          );
          done();
        }
      });
    });

    it('should get current user as observable', done => {
      service.currentUser$.subscribe(user => {
        if (user) {
          expect(user).toEqual(
            jasmine.objectContaining({
              email: 'test@example.com',
              firstName: 'Test',
              lastName: 'User',
            })
          );
          done();
        }
      });
    });
  });

  describe('Authentication Guards', () => {
    it('should check if user is authenticated', () => {
      expect(service.isAuthenticated()).toBeFalse();

      localStorage.setItem('auth_token', 'valid-token');
      (service as any)['_isAuthenticatedSubject'].next(true);

      expect(service.isAuthenticated()).toBeTrue();
    });
  });

  describe('Session Validation', () => {
    it('should return true without validation when already authenticated', done => {
      (service as any)['_isAuthenticatedSubject'].next(true);
      (service as any)['_currentUserSubject'].next({
        token: '',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
      });

      service.ensureSession().subscribe(result => {
        expect(result).toBeTrue();
        done();
      });

      httpMock.expectNone('/api/auth/validate-session');
    });

    it('should validate session and update auth state', done => {
      (service as any)['_currentUserSubject'].next({
        token: '',
        email: '',
        firstName: 'Test',
        lastName: '',
      });

      service.ensureSession().subscribe(result => {
        expect(result).toBeTrue();
        expect(service.isAuthenticated()).toBeTrue();
        done();
      });

      const req = httpMock.expectOne('/api/auth/validate-session');
      expect(req.request.method).toBe('GET');
      req.flush('', { status: 204, statusText: 'No Content' });
    });

    it('should return false when session validation fails', done => {
      service.ensureSession().subscribe(result => {
        expect(result).toBeFalse();
        expect(service.isAuthenticated()).toBeFalse();
        done();
      });

      const req = httpMock.expectOne('/api/auth/validate-session');
      req.flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    });
  });

  describe('Error Handling', () => {
    it('should handle network errors during login', done => {
      const loginData: LoginDto = {
        email: 'test@example.com',
        password: 'password123',
        ageConfirmed: true,
      };

      service.login(loginData).subscribe({
        next: () => fail('Expected network error'),
        error: _err => done(),
      });

      const req = httpMock.expectOne('/api/auth/login');
      req.error(new ErrorEvent('Network error'));
    });

    it('should handle 401 unauthorized responses', done => {
      const loginData: LoginDto = {
        email: 'test@example.com',
        password: 'password123',
        ageConfirmed: true,
      };

      service.login(loginData).subscribe({
        next: () => fail('Expected 401 error'),
        error: _err => {
          expect(service.isAuthenticated()).toBeFalse();
          done();
        },
      });

      const req = httpMock.expectOne('/api/auth/login');
      req.flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    });
  });
});

/**
 * Integration Tests for AuthService
 */
describe('AuthService Integration Tests', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    const mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService, ConfigService, { provide: Router, useValue: mockRouter }],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    const pendingRoleRequests = httpMock.match('/api/auth/user-roles');
    pendingRoleRequests.forEach(req => req.flush({ success: true, data: { roles: [] } }));
    httpMock.verify();
    localStorage.clear();
  });

  // Note: Cookie-based auth stores session server-side; service
  // initializes its state on construction and via probeSession().

  it('should complete login flow', done => {
    const loginData: LoginDto = {
      email: 'test@example.com',
      password: 'password123',
      ageConfirmed: true,
    };
    const mockResponse = {
      isSuccess: true,
      message: 'Login successful',
      token: 'jwt-token',
      expiration: '2025-01-01T00:00:00Z',
      email: 'test@example.com',
      firstName: 'Test',
      lastName: 'User',
    };

    service.login(loginData).subscribe(response => {
      expect(response.token).toBe('jwt-token');
      expect(service.isAuthenticated()).toBeTrue();

      // Logout
      service.logout();
      const logoutReq = httpMock.expectOne('/api/auth/logout');
      expect(logoutReq.request.method).toBe('POST');
      logoutReq.flush({ success: true });
      expect(service.isAuthenticated()).toBeFalse();

      done();
    });

    const req = httpMock.expectOne('/api/auth/login');
    req.flush(mockResponse);
  });
});
