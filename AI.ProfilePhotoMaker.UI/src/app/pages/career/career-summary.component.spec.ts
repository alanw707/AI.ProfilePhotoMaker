import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CareerProfileService, CareerRunDto } from '../../services/career-profile.service';
import {
  CareerSummaryComponent,
  START_KEY_STORAGE,
  backoffDelay,
} from './career-summary.component';

const allowance = { used: 1, reserved: 0, limit: 20, periodStart: '2026-10-01T00:00:00Z' };
const queuedRun = { id: 'r1', status: 'queued', steps: [], allowance } as unknown as CareerRunDto;

describe('CareerSummaryComponent', () => {
  let api: jasmine.SpyObj<CareerProfileService>;
  beforeEach(() => {
    sessionStorage.clear();
    api = jasmine.createSpyObj('CareerProfileService', ['createRun', 'getRun', 'listRuns']);
    api.listRuns.and.returnValue(of({ runs: [], allowance }));
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: CareerProfileService, useValue: api }],
    });
  });

  it('backs off exponentially up to 15 seconds', () => {
    expect([0, 1, 2, 3, 4, 10].map(backoffDelay)).toEqual([2000, 4000, 8000, 15000, 15000, 15000]);
  });

  it('shows the drafts left this month', () => {
    const fixture = TestBed.createComponent(CareerSummaryComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-allowance]').textContent).toContain(
      '19 of 20 drafts left this month'
    );
  });

  it('reuses the idempotency key after a network failure and clears it on success', () => {
    const fixture = TestBed.createComponent(CareerSummaryComponent);
    fixture.detectChanges();
    api.createRun.and.returnValues(
      throwError(() => ({ kind: 'unknown', message: 'offline' })),
      of(queuedRun)
    );
    fixture.componentInstance.start();
    const first = api.createRun.calls.mostRecent().args[0];
    expect(sessionStorage.getItem(START_KEY_STORAGE)).toBe(first);
    fixture.componentInstance.start();
    expect(api.createRun.calls.mostRecent().args[0]).toBe(first);
    expect(sessionStorage.getItem(START_KEY_STORAGE)).toBeNull();
  });

  it('drops the key after a definitive refusal and shows allowance copy', () => {
    const fixture = TestBed.createComponent(CareerSummaryComponent);
    fixture.detectChanges();
    api.createRun.and.returnValue(throwError(() => ({ kind: 'allowance', message: 'x' })));
    fixture.componentInstance.start();
    expect(sessionStorage.getItem(START_KEY_STORAGE)).toBeNull();
    expect(fixture.componentInstance.error()).toBe('You have used all drafts for this month.');
  });
});
