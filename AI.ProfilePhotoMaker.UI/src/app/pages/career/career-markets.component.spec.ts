import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import {
  CareerGoalDto,
  CareerProfileService,
  MarketComparison,
  MarketComparisonArea,
} from '../../services/career-profile.service';
import { CareerMarketsComponent } from './career-markets.component';

const area = (
  code: string,
  title: string,
  value: number | null,
  rank: number | null,
  status: MarketComparisonArea['status'] = 'available'
): MarketComparisonArea => ({
  areaCode: code,
  areaTitle: title,
  type: 'state',
  value,
  status,
  rank,
  rankedOf: 3,
  selected: false,
});
const comparison = {
  occupation: { code: '15-1252.00', title: 'Software Developers' },
  metric: { key: 'median_wage', label: 'Median annual wage', unit: 'usd_per_year' },
  level: 'state',
  national: { areaCode: '99', areaTitle: 'U.S.', value: 135980, status: 'available' },
  reference: { release: '2025-05', publishedOn: '2026-05-15' },
  areas: [
    area('08', 'Colorado', 138390, 2),
    area('72', 'Suppressed', null, null, 'not_available'),
    area('06', 'California', 191000, 1),
    area('48', 'Texas', 120000, 3),
    area('36', 'New York', 110000, null, 'top_coded'),
  ],
  selectionLimit: 3,
  truncated: false,
} as unknown as MarketComparison;
const goal = { id: 'g', etag: '"goal-v2"', goal: { targetLocation: 'Austin' } } as CareerGoalDto;

describe('CareerMarketsComponent', () => {
  let api: jasmine.SpyObj<CareerProfileService>;
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let component: CareerMarketsComponent;
  let navigate: jasmine.Spy;

  const create = (query: Record<string, string> = {}) => {
    params.next(convertToParamMap(query));
    component = TestBed.createComponent(CareerMarketsComponent).componentInstance;
    component.ngOnInit();
  };
  beforeEach(() => {
    params = new BehaviorSubject(convertToParamMap({}));
    api = jasmine.createSpyObj('CareerProfileService', [
      'getMarketMetrics',
      'compareMarkets',
      'getGoal',
      'saveMarketPreference',
    ]);
    api.getMarketMetrics.and.returnValue(of({ metrics: [] }));
    api.compareMarkets.and.returnValue(of(comparison));
    api.getGoal.and.returnValue(of(goal));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: CareerProfileService, useValue: api },
        { provide: ActivatedRoute, useValue: { queryParamMap: params } },
      ],
    });
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('restores filters from the query params and requests that comparison', () => {
    create({ metric: 'employment', level: 'metro', q: 'den', areas: '08,06,48,72' });
    expect(api.compareMarkets).toHaveBeenCalledWith({
      metric: 'employment',
      level: 'metro',
      q: 'den',
      areas: ['08', '06', '48'],
    });
    expect(component.selected()).toEqual(['08', '06', '48']);
  });

  it('orders tiles by rank with unranked areas last and never ranks suppressed values', () => {
    create();
    expect(component.ranked().map(a => a.areaCode)).toEqual(['06', '08', '48', '36', '72']);
    expect(component.bucket(comparison.areas[1])).toBe('unranked');
    expect(component.bucket(comparison.areas[4])).toBe('unranked');
    expect(component.bucket(comparison.areas[2])).toBe('q1');
    expect(component.bucket(comparison.areas[3])).toBe('q4');
  });

  it('describes a cell with title, value and rank, and the table row with the same text', () => {
    create();
    const colorado = comparison.areas[0];
    expect(component.cellLabel(colorado)).toBe(
      'Colorado, median annual wage $138,390, rank 2 of 3'
    );
    expect(component.valueText(comparison.areas[1])).toBe(
      'Not available (too few survey responses)'
    );
    expect(component.rankText(comparison.areas[1])).toBe('Not ranked');
    expect(component.dataValue(comparison.areas[1])).toBe('');
  });

  it('sorts the table by value and area and keeps unranked areas last', () => {
    create();
    expect(component.tableRows().map(a => a.areaCode)).toEqual(['06', '08', '48', '36', '72']);
    component.sortBy('value');
    expect(component.ariaSort('value')).toBe('ascending');
    expect(component.tableRows().map(a => a.areaCode)).toEqual(['48', '08', '06', '36', '72']);
    component.sortBy('area');
    expect(component.ariaSort('area')).toBe('ascending');
    expect(component.ariaSort('value')).toBe('none');
    expect(component.tableRows()[0].areaTitle).toBe('California');
  });

  it('refuses a fourth selection with a message', () => {
    create({ areas: '08,06,48' });
    expect(component.toggle(comparison.areas[3])).toBeTrue();
    navigate.calls.reset();
    expect(component.toggle(comparison.areas[4])).toBeFalse();
    expect(component.message()).toContain('up to 3 areas');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('writes selection and filters to the query params with replaceUrl', () => {
    create({ level: 'state' });
    component.toggle(comparison.areas[0]);
    expect(navigate.calls.mostRecent().args[1]).toEqual(
      jasmine.objectContaining({
        replaceUrl: true,
        queryParams: { metric: 'median_wage', level: 'state', q: null, areas: '08' },
      })
    );
  });

  it('does not save until the confirmation step calls confirmSave', () => {
    create({ areas: '08' });
    component.askToSave();
    expect(api.saveMarketPreference).not.toHaveBeenCalled();
    api.saveMarketPreference.and.returnValue(of({ ...goal, etag: '"goal-v3"' }));
    component.confirmSave();
    expect(api.saveMarketPreference).toHaveBeenCalledWith(
      { areaCode: '08', level: 'state' },
      '"goal-v2"'
    );
    expect(component.message()).toBe('Saved to your career goal.');
    expect(component.goal()?.etag).toBe('"goal-v3"');
  });

  it('only offers a save for exactly one area', () => {
    create({ areas: '08,06' });
    expect(component.saveTarget()).toBeNull();
    component.confirmSave();
    expect(api.saveMarketPreference).not.toHaveBeenCalled();
  });

  it('explains a stale goal on 412', () => {
    create({ areas: '08' });
    api.saveMarketPreference.and.returnValue(
      throwError(() => ({ kind: 'conflict', message: 'x' }))
    );
    component.confirmSave();
    expect(component.error()).toBe('Your goal changed in another tab. Try again.');
  });

  it('refetches the goal after a conflict so the next save carries the fresh ETag', () => {
    create({ areas: '08' });
    api.saveMarketPreference.and.returnValue(
      throwError(() => ({ kind: 'precondition', message: 'x' }))
    );
    api.getGoal.and.returnValue(of({ ...goal, etag: '"goal-v9"' } as CareerGoalDto));
    component.confirmSave();
    expect(component.goal()?.etag).toBe('"goal-v9"');
  });

  it('never exposes a code for a selected area missing from the response', () => {
    create({ areas: '99' });
    expect(component.selectedTitles()).toEqual([]);
    expect(component.selectionText()).toBe('1 selected area is not shown by the current filter');
    expect(component.targetText()).toBe('1 area not shown by the current filter');
  });

  it('asks for an occupation and keeps the page usable on a 409', () => {
    api.compareMarkets.and.returnValue(
      throwError(() => ({ kind: 'occupationRequired', message: 'x' }))
    );
    create();
    expect(component.occupationMissing()).toBeTrue();
    expect(component.comparison()).toBeNull();
  });

  it('flags a missing goal for the save panel', () => {
    api.getGoal.and.returnValue(throwError(() => ({ kind: 'notFound', message: 'x' })));
    create();
    expect(component.goalMissing()).toBeTrue();
  });

  it('writes plain-language reasons for unsupported metrics', () => {
    create();
    expect(component.reasonText({ reason: 'national_only_source' })).toBe(
      'Only national projections are published, so this metric is not available per market.'
    );
  });
});
