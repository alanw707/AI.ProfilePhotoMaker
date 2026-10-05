import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import {
  CareerApiError,
  CareerGoalDto,
  CareerProfileService,
  MarketComparison,
  MarketComparisonArea,
  MarketFigure,
  MarketMetric,
} from '../../services/career-profile.service';
import { formatFigure, unitLabel } from './market-format';
import { periodText } from './career-market.component';

export const MARKETS_PATH = '/app/career/markets';
const DEFAULT_METRIC = 'median_wage';
const DEFAULT_LIMIT = 3;
const BUCKETS = 5;
const SEARCH_DELAY_MS = 250;

const REASON_COPY: Record<string, string> = {
  national_only_source:
    'Only national projections are published, so this metric is not available per market.',
};
const STATE_ABBREVIATIONS: Record<string, string> = {
  Alabama: 'AL',
  Alaska: 'AK',
  Arizona: 'AZ',
  Arkansas: 'AR',
  California: 'CA',
  Colorado: 'CO',
  Connecticut: 'CT',
  Delaware: 'DE',
  'District of Columbia': 'DC',
  Florida: 'FL',
  Georgia: 'GA',
  Hawaii: 'HI',
  Idaho: 'ID',
  Illinois: 'IL',
  Indiana: 'IN',
  Iowa: 'IA',
  Kansas: 'KS',
  Kentucky: 'KY',
  Louisiana: 'LA',
  Maine: 'ME',
  Maryland: 'MD',
  Massachusetts: 'MA',
  Michigan: 'MI',
  Minnesota: 'MN',
  Mississippi: 'MS',
  Missouri: 'MO',
  Montana: 'MT',
  Nebraska: 'NE',
  Nevada: 'NV',
  'New Hampshire': 'NH',
  'New Jersey': 'NJ',
  'New Mexico': 'NM',
  'New York': 'NY',
  'North Carolina': 'NC',
  'North Dakota': 'ND',
  Ohio: 'OH',
  Oklahoma: 'OK',
  Oregon: 'OR',
  Pennsylvania: 'PA',
  'Rhode Island': 'RI',
  'South Carolina': 'SC',
  'South Dakota': 'SD',
  Tennessee: 'TN',
  Texas: 'TX',
  Utah: 'UT',
  Vermont: 'VT',
  Virginia: 'VA',
  Washington: 'WA',
  'West Virginia': 'WV',
  Wisconsin: 'WI',
  Wyoming: 'WY',
};

type Level = 'state' | 'metro';
type SortKey = 'value' | 'area';
interface Filters {
  metric: string;
  level: Level;
  q: string;
  areas: string[];
}

@Component({
  standalone: true,
  selector: 'app-career-markets',
  imports: [RouterLink, NgTemplateOutlet],
  templateUrl: './career-markets.component.html',
  styleUrl: './career.scss',
})
export class CareerMarketsComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private confirmDialog = viewChild<ElementRef<HTMLDialogElement>>('confirm');
  private sourceDialog = viewChild<ElementRef<HTMLDialogElement>>('source');
  private searchInput = new Subject<string>();
  private titles = new Map<string, string>();

  metrics = signal<MarketMetric[]>([]);
  comparison = signal<MarketComparison | null>(null);
  goal = signal<CareerGoalDto | null>(null);
  goalMissing = signal(false);
  occupationMissing = signal(false);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  /** Polite live-region text: refused selections and the save result. */
  message = signal('');

  metric = signal(DEFAULT_METRIC);
  level = signal<Level>('state');
  q = signal('');
  selected = signal<string[]>([]);
  sort = signal<{ by: SortKey; dir: 'asc' | 'desc' }>({ by: 'value', dir: 'desc' });
  mobile = signal(false);

  limit = computed(() => this.comparison()?.selectionLimit ?? DEFAULT_LIMIT);
  /** Heatmap and list order: best rank first, areas without a rank last. */
  ranked = computed(() =>
    [...(this.comparison()?.areas ?? [])].sort(
      (a, b) =>
        (a.rank ?? Infinity) - (b.rank ?? Infinity) || a.areaTitle.localeCompare(b.areaTitle)
    )
  );
  tableRows = computed(() => {
    const { by, dir } = this.sort();
    const sign = dir === 'asc' ? 1 : -1;
    return [...this.ranked()].sort((a, b) => {
      if (by === 'area') {
        return sign * a.areaTitle.localeCompare(b.areaTitle);
      }
      // Areas without a rank stay last in either direction.
      if (a.rank === null || b.rank === null) {
        return (a.rank === null ? 1 : 0) - (b.rank === null ? 1 : 0);
      }
      return sign * ((a.value ?? 0) - (b.value ?? 0));
    });
  });
  selectedTitles = computed(() => this.selected().map(code => this.titles.get(code) ?? code));
  saveTarget = computed(() => {
    const [code] = this.selected();
    return this.selected().length === 1 && code
      ? { code, title: this.titles.get(code) ?? code }
      : null;
  });
  nationalText = computed(() => {
    const c = this.comparison();
    return c ? formatFigure(this.figure(c.national)) : '';
  });
  caption = computed(() => {
    const c = this.comparison();
    return c
      ? `${c.metric.label}, ${unitLabel(c.metric.unit)}, ${periodText(c.reference.release)}, by ${this.levelNoun(c.level)}`
      : '';
  });

  ngOnInit() {
    const query = window.matchMedia?.('(max-width: 719.98px)');
    if (query) {
      this.mobile.set(query.matches);
      const onChange = (e: MediaQueryListEvent) => this.mobile.set(e.matches);
      query.addEventListener('change', onChange);
      this.destroyRef.onDestroy(() => query.removeEventListener('change', onChange));
    }
    this.api.getMarketMetrics().subscribe({
      next: result => this.metrics.set(result.metrics),
      error: (e: CareerApiError) => this.handle(e),
    });
    this.api.getGoal().subscribe({
      next: goal => this.goal.set(goal),
      error: (e: CareerApiError) => {
        if (e.kind === 'notFound') {
          this.goalMissing.set(true);
        } else {
          this.handle(e);
        }
      },
    });
    this.searchInput
      .pipe(debounceTime(SEARCH_DELAY_MS), takeUntilDestroyed(this.destroyRef))
      .subscribe(q => this.go({ q, areas: this.selected() }));
    // The URL is the single source of filter state, so a reload restores it. Selecting
    // an area only changes `areas`, which does not refetch.
    this.route.queryParamMap
      .pipe(
        map(params => this.readFilters(params)),
        tap(filters => this.apply(filters)),
        distinctUntilChanged((a, b) => a.metric === b.metric && a.level === b.level && a.q === b.q),
        tap(() => {
          this.loading.set(true);
          this.error.set('');
        }),
        switchMap(filters =>
          this.api.compareMarkets(filters).pipe(catchError((e: CareerApiError) => this.failed(e)))
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(comparison => {
        this.loading.set(false);
        if (comparison) {
          this.comparison.set(comparison);
          comparison.areas.forEach(a => this.titles.set(a.areaCode, a.areaTitle));
        }
      });
  }

  // --- filters -------------------------------------------------------------

  setMetric(key: string) {
    this.go({ metric: key });
  }
  setLevel(level: Level) {
    // Area codes differ by level, so the selection does not carry over.
    this.go({ level, areas: [] });
  }
  onSearch(value: string) {
    this.q.set(value);
    this.searchInput.next(value.trim());
  }
  clear() {
    this.message.set('');
    this.q.set('');
    this.go({ q: '', areas: [] });
  }

  // --- selection -------------------------------------------------------------

  isSelected(area: MarketComparisonArea) {
    return this.selected().includes(area.areaCode);
  }
  /** Returns false when the limit refused the selection, so a checkbox can revert. */
  toggle(area: MarketComparisonArea) {
    const current = this.selected();
    if (current.includes(area.areaCode)) {
      this.message.set('');
      this.go({ areas: current.filter(code => code !== area.areaCode) });
      return true;
    }
    if (current.length >= this.limit()) {
      this.message.set(
        `You can compare up to ${this.limit()} areas. Deselect one to add ${area.areaTitle}.`
      );
      return false;
    }
    this.message.set('');
    this.go({ areas: [...current, area.areaCode] });
    return true;
  }
  onCheckbox(area: MarketComparisonArea, event: Event) {
    if (!this.toggle(area)) {
      (event.target as HTMLInputElement).checked = false;
    }
  }

  // --- sorting ---------------------------------------------------------------

  sortBy(by: SortKey) {
    const { by: active, dir } = this.sort();
    this.sort.set({
      by,
      dir: active === by ? (dir === 'asc' ? 'desc' : 'asc') : by === 'area' ? 'asc' : 'desc',
    });
  }
  ariaSort(by: SortKey) {
    const { by: active, dir } = this.sort();
    return active === by ? (dir === 'asc' ? 'ascending' : 'descending') : 'none';
  }

  // --- saving ----------------------------------------------------------------

  askToSave() {
    if (this.saveTarget() && this.goal()) {
      this.message.set('');
      this.confirmDialog()?.nativeElement.showModal();
    }
  }
  cancelSave() {
    this.confirmDialog()?.nativeElement.close();
  }
  confirmSave() {
    const target = this.saveTarget();
    const goal = this.goal();
    const level = this.comparison()?.level;
    if (!target || !goal || !level || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.api.saveMarketPreference({ areaCode: target.code, level }, goal.etag).subscribe({
      next: updated => {
        this.saving.set(false);
        this.goal.set(updated);
        this.cancelSave();
        this.message.set('Saved to your career goal.');
      },
      error: (e: CareerApiError) => {
        this.saving.set(false);
        this.cancelSave();
        this.handleSave(e);
      },
    });
  }

  // --- source drawer ---------------------------------------------------------

  showSource() {
    const dialog = this.sourceDialog()?.nativeElement;
    if (dialog && !dialog.open) {
      dialog.showModal();
    }
  }
  closeSource() {
    this.sourceDialog()?.nativeElement.close();
  }

  // --- display ---------------------------------------------------------------

  figure(area: Pick<MarketComparisonArea, 'areaCode' | 'areaTitle' | 'value' | 'status'>) {
    const metric = this.comparison()?.metric;
    return {
      key: metric?.key ?? DEFAULT_METRIC,
      label: metric?.label ?? '',
      unit: metric?.unit ?? 'usd_per_year',
      sourceId: 'oews',
      ...area,
    } as MarketFigure;
  }
  valueText(area: MarketComparisonArea) {
    return formatFigure(this.figure(area));
  }
  rankText(area: MarketComparisonArea) {
    return area.rank === null ? 'Not ranked' : `${area.rank} of ${area.rankedOf}`;
  }
  /** "Colorado, median annual wage $138,390, rank 12 of 49". */
  cellLabel(area: MarketComparisonArea) {
    const label = (this.comparison()?.metric.label ?? '').toLowerCase();
    const rank = area.rank === null ? 'not ranked' : `rank ${area.rank} of ${area.rankedOf}`;
    return `${area.areaTitle}, ${label} ${this.valueText(area)}, ${rank}`;
  }
  /** One of five colour steps from the value's rank; unranked areas get the hatch instead. */
  bucket(area: MarketComparisonArea) {
    if (area.rank === null || !area.rankedOf) {
      return 'unranked';
    }
    return `q${Math.floor(((area.rank - 1) / area.rankedOf) * BUCKETS) + 1}`;
  }
  shortLabel(area: MarketComparisonArea) {
    return STATE_ABBREVIATIONS[area.areaTitle] ?? area.areaTitle.split(/[-,]/)[0].trim();
  }
  dataValue(area: MarketComparisonArea) {
    return area.value === null ? '' : String(area.value);
  }
  unitLabel(unit: string) {
    return unitLabel(unit);
  }
  levelNoun(level: string) {
    return level === 'metro' ? 'metropolitan area' : 'state';
  }
  releaseText(release: string) {
    return periodText(release);
  }
  reasonText(metric: Pick<MarketMetric, 'reason'>) {
    const reason = metric.reason ?? '';
    return REASON_COPY[reason] ?? 'This metric is not available per market.';
  }
  trackArea(area: MarketComparisonArea) {
    return area.areaCode;
  }

  // --- internals ---------------------------------------------------------------

  private readFilters(params: ParamMap): Filters {
    return {
      metric: params.get('metric') || DEFAULT_METRIC,
      level: params.get('level') === 'metro' ? 'metro' : 'state',
      q: params.get('q') ?? '',
      areas: (params.get('areas') ?? '').split(',').filter(Boolean).slice(0, DEFAULT_LIMIT),
    };
  }
  private apply(filters: Filters) {
    this.metric.set(filters.metric);
    this.level.set(filters.level);
    this.q.set(filters.q);
    this.selected.set(filters.areas);
  }
  private go(patch: Partial<Filters>) {
    const next = {
      metric: this.metric(),
      level: this.level(),
      q: this.q().trim(),
      areas: this.selected(),
      ...patch,
    };
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        metric: next.metric,
        level: next.level,
        q: next.q || null,
        areas: next.areas.length ? next.areas.join(',') : null,
      },
    });
  }
  /** Turns a failed comparison into page state and keeps the stream alive. */
  private failed(e: CareerApiError) {
    this.comparison.set(null);
    this.handle(e);
    return of(null);
  }
  private handleSave(e: CareerApiError) {
    switch (e.kind) {
      case 'conflict':
      case 'precondition':
        this.error.set('Your goal changed in another tab. Reload and try again.');
        return;
      case 'goalRequired':
        this.goalMissing.set(true);
        return;
      case 'areaNotFound':
        this.error.set('That area is not in the published data.');
        return;
      default:
        this.handle(e);
    }
  }
  private handle(e: CareerApiError) {
    switch (e.kind) {
      case 'disabled':
        this.router.navigateByUrl('/app');
        return;
      case 'unauthorized':
        this.router.navigate(['/auth/login'], { queryParams: { returnUrl: MARKETS_PATH } });
        return;
      case 'occupationRequired':
        this.occupationMissing.set(true);
        return;
      case 'metricUnsupported':
        this.error.set(
          'Only national projections are published, so this metric is not available per market. Pick another metric.'
        );
        return;
      case 'areaNotFound':
        this.error.set('That area is not in the published data.');
        return;
      case 'unavailable':
        this.error.set('The market data is not available right now. Try again later.');
        return;
      default:
        this.error.set(e.message || 'Something went wrong. Try again.');
    }
  }
}
