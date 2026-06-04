import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const guestGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    return true;
  }

  const isLoginRoute = state.url.startsWith('/auth/login');
  if (isLoginRoute && !authService.hasVerifiedSession()) {
    authService.clearAuthCache();
    return true;
  }

  if (authService.hasVerifiedSession()) {
    router.navigate(['/app/enhance']);
    return false;
  }

  return authService.validateSession().pipe(
    map(() => {
      router.navigate(['/app/enhance']);
      return false;
    }),
    catchError(() => {
      // Stale local auth data - clear and allow guest routes.
      authService.clearAuthCache();
      return of(true);
    })
  );
};
