import { Injectable } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  CanActivate,
  CanActivateChild,
  Router,
  RouterStateSnapshot,
} from '@angular/router';
import { Observable, of } from 'rxjs';
import { switchMap, catchError, take } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class AppGuard implements CanActivate, CanActivateChild {
  constructor(
    private _authService: AuthService,
    private _router: Router
  ) {}

  canActivate(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): Observable<boolean> | Promise<boolean> | boolean {
    return this._checkAuth(state.url);
  }

  canActivateChild(
    childRoute: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): Observable<boolean> | Promise<boolean> | boolean {
    return this._checkAuth(state.url);
  }

  private _checkAuth(redirectUrl: string): Observable<boolean> {
    if (
      !environment.production &&
      (localStorage.getItem('e2eAuthBypass') === 'true' || redirectUrl.includes('e2eAuthBypass=1'))
    ) {
      return of(true);
    }

    return this._authService.isAuthenticated$.pipe(
      take(1),
      switchMap(isAuthenticated => {
        if (!isAuthenticated && !this._authService.hasVerifiedSession()) {
          return this._authService.ensureSession().pipe(
            switchMap(ensured => {
              if (!ensured) {
                return this._redirectToLogin(redirectUrl);
              }
              return this._handleAuthenticated(redirectUrl, true);
            })
          );
        }

        if (!isAuthenticated) {
          return this._redirectToLogin(redirectUrl);
        }

        return this._handleAuthenticated(redirectUrl);
      })
    );
  }

  private _redirectToLogin(redirectUrl: string): Observable<boolean> {
    sessionStorage.setItem('redirectUrl', redirectUrl);
    this._router.navigate(['/auth/login'], {
      queryParams: {
        message: 'Please log in to access this feature',
        returnUrl: redirectUrl,
      },
    });
    return of(false);
  }

  private _handleAuthenticated(redirectUrl: string, skipValidation = false): Observable<boolean> {
    // Enforce email verification for all /app routes.
    return this._authService.getAccountStatus().pipe(
      take(1),
      switchMap(status => {
        const emailConfirmed = !!status?.data?.emailConfirmed;
        if (!emailConfirmed) {
          sessionStorage.setItem('redirectUrl', redirectUrl);
          this._router.navigate(['/auth/verify-email']);
          return of(false);
        }

        const doProfileCheck = () =>
          this._authService
            .checkProfileCompletion()
            .pipe(catchError(() => of({ isCompleted: true } as any)))
            .subscribe(profileStatus => {
              if (profileStatus && !profileStatus.isCompleted) {
                this._router.navigate(['/auth/complete-profile']);
              }
            });

        if (skipValidation) {
          doProfileCheck();
          return of(true);
        }

        // Run validation and profile-check in the background to avoid race
        this._authService
          .validateSession()
          .pipe(catchError(() => of('retry' as const)))
          .subscribe(result => {
            if (result === 'retry') {
              setTimeout(() => {
                this._authService
                  .validateSession()
                  .pipe(catchError(() => of('fail' as const)))
                  .subscribe(second => {
                    if (second === 'fail') {
                      this._authService.logout();
                    } else {
                      doProfileCheck();
                    }
                  });
              }, 300);
            } else {
              doProfileCheck();
            }
          });

        return of(true);
      }),
      // Fail closed: if we can't verify email status, block /app and send user to login/verify.
      catchError(err => {
        sessionStorage.setItem('redirectUrl', redirectUrl);

        if (err?.status === 401 || err?.status === 403) {
          this._authService.clearAuthCache();
          this._router.navigate(['/auth/login'], {
            queryParams: {
              message: 'Please sign in again to continue.',
              returnUrl: redirectUrl,
            },
          });
        } else {
          this._router.navigate(['/auth/verify-email'], {
            queryParams: {
              message: 'Unable to verify your email status right now. Please try again.',
            },
          });
        }

        return of(false);
      })
    );
  }
}
