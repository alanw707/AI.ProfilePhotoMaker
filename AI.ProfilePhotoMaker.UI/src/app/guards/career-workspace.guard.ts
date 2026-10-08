import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CareerAccessService } from '../services/career-access.service';

export const careerWorkspaceGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(CareerAccessService)
    .resolve()
    .then(allowed => allowed || router.createUrlTree(['/app']));
};
