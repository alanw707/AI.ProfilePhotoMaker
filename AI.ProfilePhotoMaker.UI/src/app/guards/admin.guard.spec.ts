import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AdminGuard } from './admin.guard';
import { AuthService } from '../services/auth.service';

describe('AdminGuard', () => {
  let guard: AdminGuard;
  let router: jasmine.SpyObj<Router>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    authService = jasmine.createSpyObj<AuthService>(
      'AuthService',
      ['ensureSession', 'hasVerifiedSession', 'ensureRolesLoaded'],
      {
        isAuthenticated$: of(true),
      }
    );

    authService.ensureRolesLoaded.and.returnValue(of(['Admin']));

    TestBed.configureTestingModule({
      providers: [
        AdminGuard,
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authService },
      ],
    });

    guard = TestBed.inject(AdminGuard);
  });

  it('allows navigation for admin user', done => {
    authService.ensureRolesLoaded.and.returnValue(of(['Admin']));

    guard.canActivate({} as any, { url: '/admin' } as any).subscribe(result => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('redirects non-admin user to dashboard', done => {
    authService.ensureRolesLoaded.and.returnValue(of([]));

    guard.canActivate({} as any, { url: '/admin' } as any).subscribe(result => {
      expect(result).toBeFalse();
      expect(router.navigate).toHaveBeenCalledWith(['/app/enhance'], {
        queryParams: { message: 'not-authorized' },
      });
      done();
    });
  });

  it('redirects unauthenticated user to login', done => {
    Object.defineProperty(authService, 'isAuthenticated$', { value: of(false) });
    authService.hasVerifiedSession.and.returnValue(true);

    guard.canActivate({} as any, { url: '/admin' } as any).subscribe(result => {
      expect(result).toBeFalse();
      expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/admin' },
      });
      done();
    });
  });
});
