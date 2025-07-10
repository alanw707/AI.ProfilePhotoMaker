import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const guestGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  console.log('=== Guest Guard Check ===');
  console.log('Route being accessed:', state.url);
  console.log('Authentication check result:', authService.isAuthenticated());

  if (!authService.isAuthenticated()) {
    console.log('✅ Guest guard: User not authenticated, allowing access to guest route');
    return true;
  } else {
    console.log('❌ Guest guard: User is authenticated, redirecting to dashboard');
    router.navigate(['/dashboard']);
    return false;
  }
};