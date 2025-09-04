import { Injectable } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  CanActivate,
  CanActivateChild,
  Router,
  RouterStateSnapshot,
} from '@angular/router';
import { Observable, of } from 'rxjs';
import { switchMap, tap, catchError } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

@Injectable({
  providedIn: 'root',
})
export class AppGuard implements CanActivate, CanActivateChild {
  private _currentCheck?: Observable<boolean>;

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
    // Deduplicate concurrent guard executions for same URL
    if (this._currentCheck) {
      return this._currentCheck;
    }

    const check$ = this._authService.isAuthenticated$.pipe(
      switchMap(isAuthenticated => {
        if (!isAuthenticated) {
          sessionStorage.setItem('redirectUrl', redirectUrl);
          this._router.navigate(['/auth/login'], {
            queryParams: {
              message: 'Please log in to access this feature',
              returnUrl: redirectUrl,
            },
          });
          return of(false);
        }

        if (redirectUrl.includes('/auth/complete-profile')) {
          return of(true);
        }

        // Run validation and profile-check in the background to avoid race
        this._authService
          .validateSession()
          .pipe(catchError(() => of('retry' as const)))
          .subscribe(result => {
            const doProfileCheck = () =>
              this._authService
                .checkProfileCompletion()
                .pipe(catchError(() => of({ isCompleted: true } as any)))
                .subscribe(status => {
                  if (status && !status.isCompleted) {
                    this._router.navigate(['/auth/complete-profile']);
                  }
                });

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
      // Clear guard deduplication when complete
      tap(() => (this._currentCheck = undefined))
    );

    this._currentCheck = check$;
    return check$;
  }
}
