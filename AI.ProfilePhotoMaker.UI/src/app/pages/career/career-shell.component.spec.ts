import { signal } from '@angular/core';
import { CareerAccessService } from '../../services/career-access.service';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { CareerJourneyDto, CareerProfileService } from '../../services/career-profile.service';
import { ConfigService } from '../../services/config.service';
import { SubscriptionStateService } from '../../services/subscription-state.service';
import { CareerShellComponent, careerSteps } from './career-shell.component';

const journey = (over: Partial<CareerJourneyDto> = {}): CareerJourneyDto => ({
  profile: { version: 1, confirmed: true },
  goal: { version: 1, occupationCode: '15-1252.00', occupationTitle: 'Developers' },
  nextAction: { key: 'analyze_pay' },
  latestResult: null,
  activeRuns: [],
  stale: [],
  ...over,
});

describe('careerSteps', () => {
  it('marks steps before the next action done and the next action current', () => {
    const s = careerSteps(journey());
    expect(s.steps.map(x => x.state)).toEqual([
      'done',
      'done',
      'done',
      'done',
      'current',
      'upcoming',
      'upcoming',
    ]);
    expect(s.summary).toBe('Step 5 of 7: Pay analysis');
  });
  it('treats "none" as every step done', () => {
    const s = careerSteps(journey({ nextAction: { key: 'none' } }));
    expect(s.steps.every(x => x.state === 'done')).toBeTrue();
    expect(s.summary).toBe('All 7 steps done');
  });
  it('flags a step whose result is stale', () => {
    const s = careerSteps(
      journey({ nextAction: { key: 'none' }, stale: [{ kind: 'market_brief', id: 'b' }] })
    );
    expect(s.steps[3].state).toBe('review');
    expect(s.steps[3].status).toBe('Needs review');
  });
  it('sends a new user to setup and claims nothing for an unknown key', () => {
    expect(
      careerSteps(journey({ profile: null, nextAction: { key: 'create_profile' } })).steps[0].path
    ).toBe('/app/career/setup');
    const s = careerSteps(journey({ nextAction: { key: 'mystery' } }));
    expect(s.steps.every(x => x.state === 'upcoming')).toBeTrue();
    expect(s.summary).toBe('Career steps');
  });
  it('works before the journey has loaded', () => {
    expect(careerSteps(null).steps.length).toBe(7);
  });
});

describe('CareerShellComponent', () => {
  function render() {
    const api = jasmine.createSpyObj<CareerProfileService>('api', ['getJourney']);
    api.getJourney.and.returnValue(of(journey()));
    TestBed.configureTestingModule({
      imports: [CareerShellComponent],
      providers: [
        provideRouter([]),
        { provide: CareerProfileService, useValue: api },
        {
          provide: AuthService,
          useValue: {
            isAuthenticated$: new BehaviorSubject(true),
            currentUser$: new BehaviorSubject(null),
          },
        },
        { provide: ConfigService, useValue: { isCareerWorkspaceEnabled: true } },
        {
          provide: CareerAccessService,
          useValue: { granted: signal(true), resolve: () => Promise.resolve(true) },
        },
        {
          provide: SubscriptionStateService,
          useValue: { state$: new BehaviorSubject({ userCreditStatus: null }) },
        },
      ],
    });
    const f = TestBed.createComponent(CareerShellComponent);
    f.detectChanges();
    return { el: f.nativeElement as HTMLElement, f };
  }
  it('shows the shared header and a labelled step rail with text states', () => {
    const { el } = render();
    expect(el.querySelector('app-header-navigation')).not.toBeNull();
    const rail = el.querySelector('nav[aria-label="Career steps"]');
    expect(rail).not.toBeNull();
    const text = rail?.textContent?.replace(/\s+/g, ' ') ?? '';
    expect(text).toContain('Pay analysis');
    expect(text).toContain('Next');
    expect(text).toContain('Done');
    expect(text).toContain('Privacy and your data');
  });
  it('collapses the step list behind a toggle on narrow screens', () => {
    const { el, f } = render();
    const toggle = el.querySelector<HTMLButtonElement>('button[aria-controls="career-step-list"]');
    expect(toggle?.textContent).toContain('Step 5 of 7: Pay analysis');
    expect(toggle?.getAttribute('aria-expanded')).toBe('false');
    toggle?.click();
    f.detectChanges();
    expect(toggle?.getAttribute('aria-expanded')).toBe('true');
  });
});
