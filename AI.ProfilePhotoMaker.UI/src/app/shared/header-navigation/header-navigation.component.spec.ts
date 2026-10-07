import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { ConfigService } from '../../services/config.service';
import { SubscriptionStateService } from '../../services/subscription-state.service';
import { HeaderNavigationComponent } from './header-navigation.component';

describe('HeaderNavigationComponent career link', () => {
  function render(authenticated: boolean, careerOn: boolean) {
    TestBed.configureTestingModule({
      imports: [HeaderNavigationComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated$: new BehaviorSubject(authenticated),
            currentUser$: new BehaviorSubject(null),
            logout: () => undefined,
          },
        },
        { provide: ConfigService, useValue: { isCareerWorkspaceEnabled: careerOn } },
        {
          provide: SubscriptionStateService,
          useValue: { state$: new BehaviorSubject({ userCreditStatus: null }) },
        },
      ],
    });
    const f = TestBed.createComponent(HeaderNavigationComponent);
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }
  const career = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('nav a')).find(a => a.textContent?.trim() === 'Career');

  it('links to the career workspace when signed in and the feature is on', () => {
    expect(career(render(true, true))?.getAttribute('href')).toBe('/app/career');
  });
  it('hides the link while the career feature is off', () => {
    expect(career(render(true, false))).toBeUndefined();
  });
  it('hides the link for signed-out visitors', () => {
    expect(career(render(false, true))).toBeUndefined();
  });
});
