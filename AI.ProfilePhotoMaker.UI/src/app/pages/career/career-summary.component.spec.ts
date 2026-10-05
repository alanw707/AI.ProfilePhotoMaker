import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CareerProfileService, CareerRunDto } from '../../services/career-profile.service';
import { CareerSummaryComponent, START_KEY_STORAGE } from './career-summary.component';
import { backoffDelay, latestRun } from './career-run';

const allowance = { used: 1, reserved: 0, limit: 20, periodStart: '2026-10-01T00:00:00Z' };
const allowanceView = {
  policyVersion: 'v1',
  limit: 20,
  used: 1,
  reserved: 0,
  remaining: 19,
  resetsAt: '2026-11-01T00:00:00Z',
};
const queuedRun = { id: 'r1', status: 'queued', steps: [], allowance } as unknown as CareerRunDto;

describe('CareerSummaryComponent', () => {
  let api: jasmine.SpyObj<CareerProfileService>;
  beforeEach(() => {
    sessionStorage.clear();
    api = jasmine.createSpyObj('CareerProfileService', [
      'createRun',
      'getRun',
      'listRuns',
      'getAllowance',
    ]);
    api.getAllowance.and.returnValue(of(allowanceView));
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

  it('reads the allowance from GET /allowance, not from the legacy run DTO', () => {
    api.listRuns.and.returnValue(
      of({ runs: [], allowance: { ...allowance, used: 20, limit: 20 } })
    );
    const fixture = TestBed.createComponent(CareerSummaryComponent);
    fixture.detectChanges();
    expect(api.getAllowance).toHaveBeenCalled();
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

  it('keeps the newer run when a slow poll returns an older state', () => {
    const answered = {
      ...queuedRun,
      status: 'queued',
      updatedAt: '2026-10-04T12:00:05Z',
    } as CareerRunDto;
    const stalePoll = {
      ...queuedRun,
      status: 'needs_input',
      updatedAt: '2026-10-04T12:00:01Z',
    } as CareerRunDto;
    const laterPoll = {
      ...queuedRun,
      status: 'working',
      updatedAt: '2026-10-04T12:00:07Z',
    } as CareerRunDto;
    expect(latestRun(answered, stalePoll)).toBe(answered);
    expect(latestRun(answered, laterPoll)).toBe(laterPoll);
    expect(latestRun(null, stalePoll)).toBe(stalePoll);
    expect(latestRun({ ...answered, id: 'other' } as CareerRunDto, stalePoll)).toBe(stalePoll);
  });

  it('explains an expired question', () => {
    const fixture = TestBed.createComponent(CareerSummaryComponent);
    fixture.detectChanges();
    fixture.componentInstance.run.set({
      ...queuedRun,
      status: 'failed',
      errorCode: 'CareerQuestionExpired',
    } as CareerRunDto);
    expect(fixture.componentInstance.failureText()).toBe(
      'The question went unanswered for too long, so the draft stopped.'
    );
  });
});
