import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  CareerGoalDto,
  CareerProfileService,
  CareerRunDto,
  OccupationMatchDto,
} from '../../services/career-profile.service';
import {
  CareerOccupationComponent,
  OCCUPATION_START_KEY_STORAGE,
} from './career-occupation.component';

const run = { id: 'r1', status: 'queued', steps: [] } as unknown as CareerRunDto;
const match = {
  id: 'm1',
  status: 'proposed',
  profileChanged: false,
  candidates: [{ code: '15-1252.00', title: 'Software Developers', strength: 'strong' }],
  reference: { attribution: 'x', url: 'u', licenseUrl: 'l', name: 'n', license: 'CC' },
} as unknown as OccupationMatchDto;

describe('CareerOccupationComponent', () => {
  let api: jasmine.SpyObj<CareerProfileService>;
  let component: CareerOccupationComponent;
  beforeEach(() => {
    sessionStorage.clear();
    api = jasmine.createSpyObj('CareerProfileService', [
      'createRun',
      'getRun',
      'getGoal',
      'confirmOccupationMatch',
      'dismissOccupationMatch',
    ]);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: CareerProfileService, useValue: api }],
    });
    component = TestBed.createComponent(CareerOccupationComponent).componentInstance;
    component.match.set(match);
    component.selectedCode.set('15-1252.00');
  });

  it('starts an occupation_match run and reuses the key after a network failure', () => {
    api.createRun.and.returnValues(
      throwError(() => ({ kind: 'unknown', message: 'offline' })),
      of(run)
    );
    component.start();
    const first = api.createRun.calls.mostRecent().args[0];
    expect(api.createRun.calls.mostRecent().args[1]).toBe('occupation_match');
    expect(sessionStorage.getItem(OCCUPATION_START_KEY_STORAGE)).toBe(first);
    component.start();
    expect(api.createRun.calls.mostRecent().args[0]).toBe(first);
    expect(sessionStorage.getItem(OCCUPATION_START_KEY_STORAGE)).toBeNull();
  });

  it('confirms with the current goal ETag', () => {
    api.getGoal.and.returnValue(of({ etag: '"goal-v2"' } as CareerGoalDto));
    api.confirmOccupationMatch.and.returnValue(of({} as CareerGoalDto));
    component.confirm();
    expect(api.confirmOccupationMatch).toHaveBeenCalledWith('m1', '15-1252.00', '"goal-v2"');
    expect(component.match()?.status).toBe('confirmed');
    expect(component.confirmedTitle()).toBe('Software Developers');
  });

  it('asks for a goal when none exists', () => {
    api.getGoal.and.returnValue(throwError(() => ({ kind: 'notFound', message: 'x' })));
    component.confirm();
    expect(component.error()).toBe('Save a career goal first.');
    expect(api.confirmOccupationMatch).not.toHaveBeenCalled();
  });

  it('explains stale and conflicting confirms', () => {
    api.getGoal.and.returnValue(of({ etag: '"g"' } as CareerGoalDto));
    api.confirmOccupationMatch.and.returnValue(
      throwError(() => ({ kind: 'conflict', message: '' }))
    );
    component.confirm();
    expect(component.error()).toBe('Your goal changed in another tab. Reload and try again.');
    api.confirmOccupationMatch.and.returnValue(
      throwError(() => ({ kind: 'matchStale', message: '' }))
    );
    component.confirm();
    expect(component.error()).toBe('Your profile changed after this match. Start a new match.');
  });

  it('cannot confirm when the profile changed', () => {
    expect(component.canConfirm()).toBeTrue();
    component.match.set({ ...match, profileChanged: true });
    expect(component.canConfirm()).toBeFalse();
  });
});
