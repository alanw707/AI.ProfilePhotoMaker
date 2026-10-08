import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { CareerAccessService } from '../services/career-access.service';
import { careerWorkspaceGuard } from './career-workspace.guard';

describe('careerWorkspaceGuard', () => {
  it('redirects to /app without access and allows navigation with it', async () => {
    let allowed = false;
    TestBed.configureTestingModule({
      providers: [
        { provide: CareerAccessService, useValue: { resolve: () => Promise.resolve(allowed) } },
      ],
    });
    const router = TestBed.inject(Router);
    const run = () =>
      TestBed.runInInjectionContext(() =>
        careerWorkspaceGuard({} as never, {} as never)
      ) as Promise<boolean | UrlTree>;
    expect(await run()).toEqual(router.createUrlTree(['/app']));
    allowed = true;
    expect(await run()).toBeTrue();
  });
});
