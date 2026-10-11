import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { ConfigService } from '../services/config.service';
import { careerJobsGuard } from './career-jobs.guard';

describe('careerJobsGuard', () => {
  it('redirects to the career home while Jobs is off and allows navigation when on', () => {
    const config = { isCareerJobsEnabled: false };
    TestBed.configureTestingModule({ providers: [{ provide: ConfigService, useValue: config }] });
    const router = TestBed.inject(Router);
    const run = () =>
      TestBed.runInInjectionContext(() => careerJobsGuard({} as never, {} as never)) as
        | boolean
        | UrlTree;
    expect(run()).toEqual(router.createUrlTree(['/app/career']));
    config.isCareerJobsEnabled = true;
    expect(run()).toBeTrue();
  });
});
