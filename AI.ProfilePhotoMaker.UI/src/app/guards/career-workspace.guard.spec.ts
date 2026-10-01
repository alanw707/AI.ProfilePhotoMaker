import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ConfigService } from '../services/config.service';
import { careerWorkspaceGuard } from './career-workspace.guard';

describe('careerWorkspaceGuard', () => {
  it('redirects to /app when disabled and allows enabled navigation', () => {
    const config = { isCareerWorkspaceEnabled: false };
    TestBed.configureTestingModule({ providers: [{ provide: ConfigService, useValue: config }] });
    const router = TestBed.inject(Router);
    expect(TestBed.runInInjectionContext(() => careerWorkspaceGuard({} as never, {} as never))).toEqual(router.createUrlTree(['/app']));
    config.isCareerWorkspaceEnabled = true;
    expect(TestBed.runInInjectionContext(() => careerWorkspaceGuard({} as never, {} as never))).toBeTrue();
  });
});
