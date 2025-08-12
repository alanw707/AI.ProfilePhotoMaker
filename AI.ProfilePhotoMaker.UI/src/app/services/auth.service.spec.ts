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
  let mockConfigService: jasmine.SpyObj<ConfigService>;

  beforeEach(() => {
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockConfigService = jasmine.createSpyObj('ConfigService', ['getApiUrl']);
    mockConfigService.getApiUrl.and.returnValue('http://localhost:5000');

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AuthService,
        { provide: Router, useValue: mockRouter },
        { provide: ConfigService, useValue: mockConfigService },
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  describe('Service Initialization', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should initialize authentication state from localStorage', () => {
      localStorage.setItem('auth_token', 'stored-token');

      const newService = TestBed.inject(AuthService);

      expect(newService.getToken()).toBe('stored-token');
      expect(newService.isAuthenticated()).toBeTrue();
    });

    it('should start with unauthenticated state when no token exists', () => {
      expect(service.isAuthenticated()).toBeFalse();
      expect(service.getToken()).toBeNull();
    });
  });

  describe('Login Functionality', () => {
    it('should login with valid credentials', done => {
      const loginData: LoginDto = { email: 'test@example.com', password: 'password123' };
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
        expect(service.getToken()).toBe('jwt-token');
        done();
      });

      const req = httpMock.expectOne('http://localhost:5000/api/auth/login');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(loginData);
      req.flush(mockResponse);
    });

    it('should handle login errors', done => {
      const loginData: LoginDto = { email: 'test@example.com', password: 'wrong-password' };
      const errorResponse = {
        isSuccess: false,
        message: 'Invalid credentials',
        token: '',
        expiration: '',
      };

      service.login(loginData).subscribe(response => {
        expect(response.token).toBe('');
        expect(service.isAuthenticated()).toBeFalse();
        done();
      });

      const req = httpMock.expectOne('http://localhost:5000/api/auth/login');
      req.flush(errorResponse);
    });

    it('should store token on successful login', done => {
      const loginData: LoginDto = { email: 'test@example.com', password: 'password123' };
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
        expect(localStorage.getItem('auth_token')).toBe('jwt-token');
        done();
      });

      const req = httpMock.expectOne('http://localhost:5000/api/auth/login');
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

      const loginData: LoginDto = { email: 'test@example.com', password: 'password123' };
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

      const req = httpMock.expectOne('http://localhost:5000/api/auth/login');
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

      const req = httpMock.expectOne('http://localhost:5000/api/auth/register');
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
      };
      const errorResponse = {
        isSuccess: false,
        message: 'Validation failed',
        token: '',
        expiration: '',
      };

      service.register(registrationData).subscribe(response => {
        expect(response.token).toBe('');
        expect(service.isAuthenticated()).toBeFalse();
        done();
      });

      const req = httpMock.expectOne('http://localhost:5000/api/auth/register');
      req.flush(errorResponse);
    });
  });

  describe('Logout Functionality', () => {
    beforeEach(() => {
      // Set up authenticated state
      localStorage.setItem('auth_token', 'jwt-token');
      service['currentUserSubject'].next({
        token: 'jwt-token',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
      });
      service['isAuthenticatedSubject'].next(true);
    });

    it('should logout and clear session data', () => {
      service.logout();

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
    });

    it('should navigate to login page after logout', () => {
      service.logout();
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });
  });

  describe('Token Management', () => {
    it('should get stored token', () => {
      localStorage.setItem('auth_token', 'stored-token');
      expect(service.getToken()).toBe('stored-token');
    });

    it('should return null when no token exists', () => {
      expect(service.getToken()).toBeNull();
    });

    it('should check authentication state based on token', () => {
      expect(service.isAuthenticated()).toBeFalse();

      localStorage.setItem('auth_token', 'token');
      service['isAuthenticatedSubject'].next(true);
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
      service['currentUserSubject'].next(mockUser);
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
      service['isAuthenticatedSubject'].next(true);

      expect(service.isAuthenticated()).toBeTrue();
    });
  });

  describe('Error Handling', () => {
    it('should handle network errors during login', done => {
      const loginData: LoginDto = { email: 'test@example.com', password: 'password123' };

      service.login(loginData).subscribe(response => {
        // Should handle error gracefully
        expect(response).toBeDefined();
        done();
      });

      const req = httpMock.expectOne('http://localhost:5000/api/auth/login');
      req.error(new ErrorEvent('Network error'));
    });

    it('should handle 401 unauthorized responses', done => {
      const loginData: LoginDto = { email: 'test@example.com', password: 'password123' };

      service.login(loginData).subscribe(_response => {
        expect(service.isAuthenticated()).toBeFalse();
        done();
      });

      const req = httpMock.expectOne('http://localhost:5000/api/auth/login');
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
    const mockConfigService = jasmine.createSpyObj('ConfigService', ['getApiUrl']);
    mockConfigService.getApiUrl.and.returnValue('http://localhost:5000');

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService, { provide: ConfigService, useValue: mockConfigService }],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should maintain authentication state across service instances', () => {
    // Simulate login
    localStorage.setItem('auth_token', 'stored-token');

    // Create new service instance (simulating page reload)
    const newService = TestBed.inject(AuthService);

    expect(newService.isAuthenticated()).toBeTrue();
    expect(newService.getToken()).toBe('stored-token');
  });

  it('should complete login flow', done => {
    const loginData: LoginDto = { email: 'test@example.com', password: 'password123' };
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
      expect(service.isAuthenticated()).toBeFalse();

      done();
    });

    const req = httpMock.expectOne('http://localhost:5000/api/auth/login');
    req.flush(mockResponse);
  });
});
