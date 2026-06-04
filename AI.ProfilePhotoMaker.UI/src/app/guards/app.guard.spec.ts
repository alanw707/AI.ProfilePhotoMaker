import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { BehaviorSubject, from, isObservable, of } from 'rxjs';
import { AppGuard } from './app.guard';
import { AuthService } from '../services/auth.service';

describe('AppGuard', () => {
  let guard: AppGuard;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  let isAuthenticatedSubject: BehaviorSubject<boolean>;
  const toObservable = (value: boolean | Promise<boolean> | import('rxjs').Observable<boolean>) =>
    isObservable(value) ? value : from(Promise.resolve(value));

  beforeEach(() => {
    isAuthenticatedSubject = new BehaviorSubject<boolean>(false);
    authService = jasmine.createSpyObj<AuthService>(
      'AuthService',
      [
        'getAccountStatus',
        'validateSession',
        'checkProfileCompletion',
        'hasVerifiedSession',
        'ensureSession',
        'logout',
      ],
      { isAuthenticated$: isAuthenticatedSubject.asObservable() }
    );

    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        AppGuard,
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
      ],
    });

    guard = TestBed.inject(AppGuard);
  });

  it('allows navigation when ensureSession succeeds', done => {
    authService.hasVerifiedSession.and.returnValue(false);
    authService.ensureSession.and.returnValue(of(true));
    authService.getAccountStatus.and.returnValue(of({ data: { emailConfirmed: true } } as any));
    authService.validateSession.and.returnValue(of('ok'));
    authService.checkProfileCompletion.and.returnValue(of({ isCompleted: true } as any));

    toObservable(guard.canActivate({} as any, { url: '/app/enhance' } as any)).subscribe(
      (result: boolean) => {
        expect(result).toBeTrue();
        expect(authService.ensureSession).toHaveBeenCalled();
        expect(authService.validateSession).not.toHaveBeenCalled();
        expect(authService.checkProfileCompletion).toHaveBeenCalled();
        expect(router.navigate).not.toHaveBeenCalled();
        done();
      }
    );
  });

  it('redirects to login when ensureSession fails', done => {
    authService.hasVerifiedSession.and.returnValue(false);
    authService.ensureSession.and.returnValue(of(false));

    toObservable(guard.canActivate({} as any, { url: '/app/enhance' } as any)).subscribe(
      (result: boolean) => {
        expect(result).toBeFalse();
        expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], {
          queryParams: {
            message: 'Please log in to access this feature',
            returnUrl: '/app/enhance',
          },
        });
        done();
      }
    );
  });

  it('redirects to verify-email when email is not confirmed', done => {
    authService.hasVerifiedSession.and.returnValue(true);
    isAuthenticatedSubject.next(true);
    authService.getAccountStatus.and.returnValue(of({ data: { emailConfirmed: false } } as any));

    toObservable(guard.canActivate({} as any, { url: '/app/enhance' } as any)).subscribe(
      (result: boolean) => {
        expect(result).toBeFalse();
        expect(router.navigate).toHaveBeenCalledWith(['/auth/verify-email']);
        done();
      }
    );
  });

  it('sends user to complete-profile when profile is incomplete', done => {
    authService.hasVerifiedSession.and.returnValue(false);
    authService.ensureSession.and.returnValue(of(true));
    authService.getAccountStatus.and.returnValue(of({ data: { emailConfirmed: true } } as any));
    authService.checkProfileCompletion.and.returnValue(of({ isCompleted: false } as any));

    toObservable(guard.canActivate({} as any, { url: '/app/enhance' } as any)).subscribe(
      (result: boolean) => {
        expect(result).toBeTrue();
        expect(router.navigate).toHaveBeenCalledWith(['/auth/complete-profile']);
        done();
      }
    );
  });
});
