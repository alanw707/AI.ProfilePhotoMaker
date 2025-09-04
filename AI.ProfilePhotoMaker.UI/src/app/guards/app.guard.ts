import { Injectable } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  CanActivate,
  CanActivateChild,
  Router,
  RouterStateSnapshot,
} from '@angular/router';
import { Observable, of } from 'rxjs';
import { map, switchMap, tap, catchError } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

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
    return this._authService.isAuthenticated$.pipe(
      switchMap(isAuthenticated => {
        if (!isAuthenticated) {
          // Store the attempted URL for redirecting after login
          sessionStorage.setItem('redirectUrl', redirectUrl);

          // Redirect to login with a message
          this._router.navigate(['/auth/login'], {
            queryParams: {
              message: 'Please log in to access this feature',
              returnUrl: redirectUrl,
            },
          });
          return of(false);
        }

        // Skip profile completion check for the profile completion route itself
        if (redirectUrl.includes('/auth/complete-profile')) {
          return of(true);
        }

        // Server-validate session first to avoid 401s, then check profile completion
        return this._authService.validateSession().pipe(
          switchMap(() =>
            this._authService.checkProfileCompletion().pipe(
              tap(profileStatus => {
                if (!profileStatus.isCompleted) {
                  console.log('🔒 Profile incomplete, redirecting to profile completion');
                  this._router.navigate(['/auth/complete-profile']);
                }
              }),
              map(profileStatus => profileStatus.isCompleted),
              catchError(error => {
                // If profile completion check fails, allow access but log error
                console.error('🔒 Profile completion check failed:', error);
                return of(true);
              })
            )
          ),
          catchError(() => {
            // Session not valid on server; force login
            sessionStorage.setItem('redirectUrl', redirectUrl);
            this._router.navigate(['/auth/login'], {
              queryParams: {
                message: 'Please log in to access this feature',
                returnUrl: redirectUrl,
              },
            });
            return of(false);
          })
        );
      })
    );
  }
}
