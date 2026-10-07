import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  CareerAllowanceDto,
  CareerJourneyDto,
  CareerProfileService,
} from '../../services/career-profile.service';
import { CareerHomeComponent } from './career-home.component';

describe('CareerHomeComponent', () => {
  function render(j: Partial<CareerJourneyDto>, allowance?: CareerAllowanceDto) {
    const api = jasmine.createSpyObj<CareerProfileService>('api', ['getJourney', 'getAllowance']);
    api.getAllowance.and.returnValue(
      allowance ? of(allowance) : throwError(() => ({ kind: 'unknown', message: 'x' }))
    );
    api.getJourney.and.returnValue(
      of({
        profile: null,
        goal: null,
        nextAction: { key: 'none' },
        latestResult: null,
        activeRuns: [],
        stale: [],
        ...j,
      })
    );
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: CareerProfileService, useValue: api }],
    });
    const f = TestBed.createComponent(CareerHomeComponent);
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }
  it('shows remaining, in progress and a readable reset date', () => {
    const el = render(
      {},
      {
        policyVersion: 'v1',
        limit: 20,
        used: 5,
        reserved: 2,
        remaining: 13,
        resetsAt: '2026-11-01T00:00:00Z',
      }
    );
    const text = el.textContent?.replace(/\s+/g, ' ') ?? '';
    expect(text).toContain('13 of 20 drafts left');
    expect(text).toContain('2 in progress');
    expect(text).toContain('Resets on November 1, 2026.');
    expect(text).not.toContain('2026-11-01T');
    expect(el.querySelector('[data-allowance-used]')).toBeNull();
  });
  it('keeps edit and export links when the allowance is used up', () => {
    const el = render(
      {},
      { policyVersion: 'v1', limit: 5, used: 5, reserved: 0, remaining: 0, resetsAt: '2026-11-01Z' }
    );
    expect(el.querySelector('[data-allowance-used]')?.textContent).toContain('exportable');
    expect(el.querySelectorAll('[data-allowance-used] a').length).toBe(2);
  });
  it('prompts for a goal and shows the allowlisted next step', () => {
    const el = render({ nextAction: { key: 'set_goal', route: '/evil' } });
    expect(el.textContent).toContain('not set a goal');
    expect(el.querySelector('[data-next]')?.getAttribute('href')).toBe('/app/career/setup');
  });
  it('renders nothing for an unknown action key', () => {
    expect(
      render({ nextAction: { key: 'javascript:alert(1)' } }).querySelector('[data-next]')
    ).toBeNull();
  });
  it('links the latest result with its id and hides raw codes and dates', () => {
    const el = render({
      goal: { version: 1, occupationCode: '15-1252', occupationTitle: 'Dev', location: 'Austin' },
      latestResult: { kind: 'roadmap', id: 'r-1', createdAt: '2026-10-05T10:00:00Z' },
    });
    expect(el.querySelector('[data-latest]')?.getAttribute('href')).toBe(
      '/app/career/roadmap?roadmap=r-1'
    );
    const goal = el.querySelector('[data-goal]');
    expect(goal?.firstChild?.textContent?.trim()).toBe('Dev');
    expect(goal?.querySelector('.brief__place')?.textContent?.trim()).toBe('Austin');
    expect(el.textContent).not.toContain('15-1252');
    expect(el.textContent).not.toContain('2026-10-05T');
  });
  it('shows Still working and Try again for recoverable runs', () => {
    const el = render({
      activeRuns: [
        { id: 'a', task: 'pay_analysis', status: 'working', startedAt: 'x' },
        { id: 'b', task: 'roadmap', status: 'failed', startedAt: 'x' },
      ],
    });
    const links = Array.from(el.querySelectorAll('[data-run]')).map(a => a.textContent?.trim());
    expect(links).toEqual(['Still working', 'Try again']);
    expect(el.querySelectorAll('[data-run]')[1].getAttribute('href')).toBe(
      '/app/career/roadmap?run=b'
    );
  });
});
