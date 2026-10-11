import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigService } from '../services/config.service';

/**
 * The Jobs page is hidden unless features.careerJobs is on. The API enforces the same flag (404),
 * so this only keeps an old bookmark from landing on a page that cannot load.
 */
export const careerJobsGuard: CanActivateFn = () =>
  inject(ConfigService).isCareerJobsEnabled || inject(Router).createUrlTree(['/app/career']);
