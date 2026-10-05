import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import {
  CareerApiError,
  CareerGoalDto,
  CareerProfileService,
  JobObservation,
  JobObservations,
} from '../../services/career-profile.service';
import { CareerJobsComponent } from './career-jobs.component';

const MALICIOUS = '<img src=x onerror=alert(1)>';
const observation = (over: Partial<JobObservation> = {}): JobObservation => ({
  observationId: 'usajobs:1',
  title: 'IT Specialist',
  organization: 'Department of Veterans Affairs',
  locations: [
    { city: 'Denver', state: 'CO', areaCode: '19740', match: 'user_area' },
    { city: 'Colorado Springs', state: 'CO', areaCode: null, match: 'other' },
  ],
  multiLocation: true,
  pay: { min: 98500, max: 128000, unit: 'usd_per_year', basis: 'annual', status: 'available' },
  postedOn: '2026-09-28',
  closesOn: '2026-10-20',
  remoteEligibility: 'unknown',
  remoteNote: null,
  series: '2210',
  grade: 'GS-12',
  sourceUrl: 'https://www.usajobs.gov/job/1',
  sourceId: 'usajobs',
  ...over,
});
const result = (over: Partial<JobObservations> = {}): JobObservations => ({
  occupation: { code: '15-1252.00', title: 'Software Developers' },
  area: { input: 'Denver, CO', resolution: 'metro', code: '19740', title: 'Denver-Aurora, CO' },
  coverage: {
    available: true,
    reason: null,
    sourceId: 'usajobs',
    sourceName: 'USAJOBS',
    coverage: 'U.S. federal agencies only.',
    attribution: 'Job postings from USAJOBS.',
    sourceUrl: 'https://www.usajobs.gov/',
    retrievedAt: null,
    observedFrom: null,
    observedTo: null,
    counts: {
      matched: 5,
      shown: 1,
      duplicateIds: 0,
      duplicateReposts: 0,
      expired: 0,
      remoteUnknownExcluded: 0,
      otherLocationExcluded: 4,
    },
  },
  preferences: { areaCode: '19740', stalePreference: false, note: null },
  observations: [observation()],
  truncated: false,
  note: 'Postings are observations.',
  ...over,
});

describe('CareerJobsComponent', () => {
  let api: jasmine.SpyObj<CareerProfileService>;
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let navigate: jasmine.Spy;

  const create = (query: Record<string, string> = {}) => {
    params.next(convertToParamMap(query));
    const fixture = TestBed.createComponent(CareerJobsComponent);
    fixture.detectChanges();
    return fixture;
  };
  const text = (f: { nativeElement: HTMLElement }) => f.nativeElement.textContent ?? '';

  beforeEach(() => {
    params = new BehaviorSubject(convertToParamMap({}));
    api = jasmine.createSpyObj('CareerProfileService', ['getJobObservations', 'getGoal']);
    api.getJobObservations.and.returnValue(of(result()));
    api.getGoal.and.returnValue(
      of({
        goal: { targetLocation: 'Austin, TX' },
        preferredArea: { title: 'Austin, TX' },
      } as CareerGoalDto)
    );
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: CareerProfileService, useValue: api },
        { provide: ActivatedRoute, useValue: { queryParamMap: params } },
      ],
    });
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('restores filters from the query params and requests them', () => {
    create({ area: 'Denver', eligibleOnly: 'true', remote: 'eligible', q: 'it' });
    expect(api.getJobObservations).toHaveBeenCalledWith({
      area: 'Denver',
      eligibleOnly: true,
      remote: 'eligible',
      q: 'it',
    });
  });

  it('writes filter changes to the query params and clears them', () => {
    const fixture = create({ q: 'it' });
    fixture.componentInstance.setEligibleOnly(true);
    expect(navigate.calls.mostRecent().args[1].queryParams).toEqual({
      area: null,
      eligibleOnly: 'true',
      remote: null,
      q: 'it',
    });
    fixture.componentInstance.clear();
    expect(navigate.calls.mostRecent().args[1].queryParams).toEqual({
      area: null,
      eligibleOnly: null,
      remote: null,
      q: null,
    });
  });

  it('renders untrusted listing text as text only', () => {
    api.getJobObservations.and.returnValue(
      of(result({ observations: [observation({ title: MALICIOUS, organization: MALICIOUS })] }))
    );
    const fixture = create();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-title]')?.textContent).toBe(MALICIOUS);
    expect(el.querySelector('img')).toBeNull();
  });

  it('shows the original-posting link safely and omits it when the source dropped the URL', () => {
    api.getJobObservations.and.returnValue(
      of(
        result({
          observations: [observation(), observation({ observationId: 'x', sourceUrl: null })],
        })
      )
    );
    const fixture = create();
    const links = fixture.nativeElement.querySelectorAll('a[href="https://www.usajobs.gov/job/1"]');
    expect(links.length).toBe(1);
    expect(links[0].getAttribute('rel')).toBe('noopener noreferrer');
    expect(links[0].getAttribute('target')).toBe('_blank');
  });

  it('states pay basis, remote status and the multi-location match in words', () => {
    const t = text(create());
    expect(t).toContain('$98,500 to $128,000 per year');
    expect(t).toContain('Remote not stated');
    expect(t).toContain('Denver, CO (matches your area)');
    expect(t).toContain('4 hidden: not open where you are');
  });

  it('explains each unavailable reason and keeps the benchmark links', () => {
    const cases: [JobObservations['coverage']['reason'], string][] = [
      ['source_not_configured', 'The posting source is not configured yet.'],
      [
        'source_unavailable',
        'The posting source could not be reached. The benchmark pages are unaffected.',
      ],
    ];
    for (const [reason, copy] of cases) {
      const base = result();
      api.getJobObservations.and.returnValue(
        of(result({ coverage: { ...base.coverage, available: false, reason }, observations: [] }))
      );
      const fixture = create();
      expect(text(fixture)).toContain(copy);
      expect(text(fixture)).toContain('wage benchmark, pay analysis and market comparison');
      expect(text(fixture)).not.toContain('No open postings matched');
    }
  });

  it('names the filters when an available result is empty', () => {
    api.getJobObservations.and.returnValue(of(result({ observations: [] })));
    const t = text(create({ q: 'cobol', eligibleOnly: 'true' }));
    expect(t).toContain('No open postings matched');
    expect(t).toContain('search: "cobol"');
    expect(t).toContain('remote-eligible only');
    expect(t).toContain('widen');
  });

  it('names the saved preference and the current filter when stale', () => {
    api.getJobObservations.and.returnValue(
      of(result({ preferences: { areaCode: '1', stalePreference: true, note: null } }))
    );
    expect(text(create({ area: 'Denver' }))).toContain(
      'Your saved location is Austin, TX; this filter is Denver.'
    );
  });

  it('says when the list is truncated', () => {
    api.getJobObservations.and.returnValue(
      of(
        result({
          truncated: true,
          coverage: { ...result().coverage, counts: { ...result().coverage.counts, shown: 25 } },
        })
      )
    );
    expect(text(create())).toContain('Showing the first 25');
  });

  it('routes a disabled workspace home and a missing occupation to its prompt', () => {
    api.getJobObservations.and.returnValue(
      throwError(() => ({ kind: 'occupationRequired', message: 'm' }) as CareerApiError)
    );
    const fixture = create();
    expect(fixture.componentInstance.occupationMissing()).toBeTrue();
  });
});
