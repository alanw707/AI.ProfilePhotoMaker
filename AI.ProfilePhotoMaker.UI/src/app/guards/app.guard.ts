import { Injectable } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  CanActivate,
  CanActivateChild,
  Router,
  RouterStateSnapshot,
} from '@angular/router';
import { Observable } from 'rxjs';
import { map, tap } from 'rxjs/operators';
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
      tap(isAuthenticated => {
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
        }
      }),
      map(isAuthenticated => isAuthenticated)
    );
  }
}
