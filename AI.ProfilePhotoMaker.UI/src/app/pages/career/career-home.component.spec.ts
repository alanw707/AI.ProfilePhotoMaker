import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CareerJourneyDto, CareerProfileService } from '../../services/career-profile.service';
import { CareerHomeComponent } from './career-home.component';

describe('CareerHomeComponent', () => {
  function render(j: Partial<CareerJourneyDto>) {
    const api = jasmine.createSpyObj<CareerProfileService>('api', ['getJourney']);
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
    expect(el.textContent?.replace(/\s+/g, ' ')).toContain('Dev · Austin');
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
