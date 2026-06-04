import { Injectable } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  CanActivate,
  CanActivateChild,
  Router,
  RouterStateSnapshot,
} from '@angular/router';
import { Observable, of } from 'rxjs';
import { map, switchMap, take } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

@Injectable({
  providedIn: 'root',
})
export class AdminGuard implements CanActivate, CanActivateChild {
  constructor(
    private _authService: AuthService,
    private _router: Router
  ) {}

  canActivate(_route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean> {
    return this._checkAccess(state.url);
  }

  canActivateChild(
    _route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): Observable<boolean> {
    return this._checkAccess(state.url);
  }

  private _checkAccess(redirectUrl: string): Observable<boolean> {
    return this._authService.isAuthenticated$.pipe(
      take(1),
      switchMap(isAuthenticated => {
        if (!isAuthenticated && !this._authService.hasVerifiedSession()) {
          return this._authService.ensureSession().pipe(
            switchMap(ensured => {
              if (!ensured) {
                this._router.navigate(['/auth/login'], { queryParams: { returnUrl: redirectUrl } });
                return of(false);
              }

              return this._authService.ensureRolesLoaded().pipe(
                map(roles => {
                  if (!roles.includes('Admin')) {
                    this._router.navigate(['/app/enhance'], {
                      queryParams: { message: 'not-authorized' },
                    });
                    return false;
                  }

                  return true;
                })
              );
            })
          );
        }

        if (!isAuthenticated) {
          this._router.navigate(['/auth/login'], { queryParams: { returnUrl: redirectUrl } });
          return of(false);
        }

        return this._authService.ensureRolesLoaded().pipe(
          map(roles => {
            if (!roles.includes('Admin')) {
              this._router.navigate(['/app/enhance'], {
                queryParams: { message: 'not-authorized' },
              });
              return false;
            }

            return true;
          })
        );
      })
    );
  }
}
