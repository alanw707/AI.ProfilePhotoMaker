import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { signal } from '@angular/core';
import { CareerAccessService } from '../../services/career-access.service';
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
        {
          provide: CareerAccessService,
          useValue: { granted: signal(careerOn), resolve: () => Promise.resolve(careerOn) },
        },
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

  it('links to the career workspace when the account has career access', () => {
    expect(career(render(true, true))?.getAttribute('href')).toBe('/app/career');
  });
  it('hides the link without career access', () => {
    expect(career(render(true, false))).toBeUndefined();
  });
  it('hides the link for signed-out visitors', () => {
    expect(career(render(false, true))).toBeUndefined();
  });
});
