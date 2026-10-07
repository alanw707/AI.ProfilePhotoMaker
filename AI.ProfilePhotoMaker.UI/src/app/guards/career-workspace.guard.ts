import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigService } from '../services/config.service';

export const careerWorkspaceGuard: CanActivateFn = () =>
  inject(ConfigService).isCareerWorkspaceEnabled ? true : inject(Router).createUrlTree(['/app']);
