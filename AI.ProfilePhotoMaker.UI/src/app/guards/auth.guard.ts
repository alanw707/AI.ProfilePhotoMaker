import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  console.log('=== Auth Guard Check ===');
  console.log('Route being accessed:', state.url);
  console.log('Authentication check result:', authService.isAuthenticated());

  if (authService.isAuthenticated()) {
    console.log('✅ Auth guard: User is authenticated, allowing access');
    return true;
  } else {
    console.log('❌ Auth guard: User not authenticated, redirecting to login');
    console.log('Redirect URL will be:', state.url);
    router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }
};
