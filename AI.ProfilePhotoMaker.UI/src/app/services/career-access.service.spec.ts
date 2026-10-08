import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from './auth.service';
import { CareerAccessService } from './career-access.service';
import { ConfigService } from './config.service';

describe('CareerAccessService', () => {
  let auth: BehaviorSubject<boolean>;
  let http: HttpTestingController;

  function setup(publicFlag: boolean, signedIn: boolean) {
    auth = new BehaviorSubject(signedIn);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ConfigService,
          useValue: { isCareerWorkspaceEnabled: publicFlag, baseUrl: '/api' },
        },
        {
          provide: AuthService,
          useValue: { isAuthenticated$: auth, isAuthenticated: () => auth.value },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(CareerAccessService);
  }
  afterEach(() => http.verify());

  it('grants access without a request when career is open to everyone', async () => {
    const s = setup(true, true);
    expect(await s.resolve()).toBeTrue();
    expect(s.granted()).toBeTrue();
  });

  it('denies signed-out visitors without a request during a staged rollout', async () => {
    const s = setup(false, false);
    expect(await s.resolve()).toBeFalse();
  });

  it('asks the server for a signed-in account and caches the answer', async () => {
    const s = setup(false, true);
    const first = s.resolve();
    http.expectOne('/api/config/career-access').flush({ success: true, data: { enabled: true } });
    expect(await first).toBeTrue();
    expect(s.granted()).toBeTrue();
    expect(await s.resolve()).toBeTrue();
  });

  it('fails closed when the access check errors', async () => {
    const s = setup(false, true);
    const result = s.resolve();
    http.expectOne('/api/config/career-access').flush('x', { status: 500, statusText: 'err' });
    expect(await result).toBeFalse();
  });

  it('forgets the answer when the user signs out', async () => {
    const s = setup(false, true);
    const first = s.resolve();
    http.expectOne('/api/config/career-access').flush({ success: true, data: { enabled: true } });
    await first;
    auth.next(false);
    expect(s.granted()).toBeFalse();
    expect(await s.resolve()).toBeFalse();
  });
});
