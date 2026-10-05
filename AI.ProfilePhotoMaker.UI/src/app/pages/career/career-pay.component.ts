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
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, distinctUntilChanged, map, switchMap, tap } from 'rxjs';
import {
  CareerApiError,
  CareerProfileService,
  CareerRunDto,
  MarketFigure,
  MarketSource,
  PayAnalysisDto,
  PayAnalysisSummary,
  PayBenchmarkSection,
  PayPersonalizedSection,
  PayQualification,
  PayScenarioSection,
} from '../../services/career-profile.service';
import {
  clearStartKey,
  isActive,
  latestRun,
  pollRun,
  releaseStartKey,
  startKey,
} from './career-run';
import { comparisonRows, formatFigure } from './market-format';
import { periodText } from './career-market.component';

const EXCLUSION_COPY: Record<string, string> = {
  'different occupation family': 'a different occupation',
  'different geography': 'a different location',
  'different or unknown level': 'a different or unstated level',
  'different or unknown employment type': 'a different or unstated employment type',
  'duplicate requisition': 'a duplicate posting',
  'missing identity or matching field': 'missing employer, role or location',
  'not employer-disclosed pay': 'pay not disclosed by the employer',
  'unknown or non-USD currency': 'a non-USD currency',
  'unknown pay basis or annual hours': 'an unclear pay basis',
  'outside 90-day lookback': 'posted more than 90 days ago',
  'work-location ineligible': 'not open where you are',
  'work-location eligibility unknown': 'unclear whether it is open where you are',
  'invalid pay range': 'an invalid pay range',
};
const REASON_COPY: Record<string, string> = {
  source_unavailable: 'The published data could not be loaded.',
  location_unresolved: 'Add a city and state to your goal to see local figures.',
  not_published: 'BLS does not publish this measure for this occupation.',
  provider_rights_unverified: 'No qualified source yet.',
  insufficient_observations: 'Not enough independent current observations yet.',
  insufficient_employers: 'Not enough independent current observations yet.',
  CareerPaySourceUnavailable:
    'The advertised-pay source is temporarily unavailable; the benchmark is unaffected.',
};
const GATE_STATUS_COPY: Record<string, string> = {
  Passed: 'Met',
  Unverified: 'Not yet verified',
  Failed: 'Not met',
};
const plain = (code: string) => {
  const text = code
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .toLowerCase();
  return text ? text.charAt(0).toUpperCase() + text.slice(1) + '.' : 'This is not available.';
};

const PATH = '/app/career/pay';
const START_KEY = 'career-pay-start-key';

@Component({
  standalone: true,
  selector: 'app-career-pay',
  imports: [RouterLink],
  templateUrl: './career-pay.component.html',
  styleUrl: './career.scss',
})
export class CareerPayComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private drawer = viewChild<ElementRef<HTMLDialogElement>>('drawer');
  run = signal<CareerRunDto | null>(null);
  analysis = signal<PayAnalysisDto | null>(null);
  recent = signal<PayAnalysisSummary[]>([]);
  qualification = signal<PayQualification | null>(null);
  drawerSources = signal<MarketSource[]>([]);
  starting = signal(false);
  loading = signal(false);
  occupationMissing = signal(false);
  connectionLost = signal(false);
  error = signal('');
  recomputing = signal(false);
  recomputeMessage = signal('');
  active = computed(() => !!this.run() && isActive(this.run()!.status));
  benchmark = computed(
    () =>
      this.analysis()?.sections.find(s => s.key === 'benchmark') as PayBenchmarkSection | undefined
  );
  personalized = computed(
    () =>
      this.analysis()?.sections.find(s => s.key === 'personalized') as
        | PayPersonalizedSection
        | undefined
  );
  scenario = computed(
    () =>
      this.analysis()?.sections.find(s => s.key === 'scenario') as PayScenarioSection | undefined
  );

  ngOnInit() {
    this.loadRecent();
    this.api
      .getPayQualification()
      .subscribe({ next: q => this.qualification.set(q), error: e => this.handle(e) });
    this.api.getGoal().subscribe({
      next: goal => this.occupationMissing.set(!goal.occupation),
      error: e => this.handle(e),
    });
    this.route.queryParamMap
      .pipe(
        map(p => p.get('analysis')),
        distinctUntilChanged(),
        tap(id => {
          this.analysis.set(null);
          this.recomputeMessage.set('');
          this.loading.set(!!id);
        }),
        switchMap(id => (id ? this.api.getPayAnalysis(id) : EMPTY)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: a => {
          this.analysis.set(a);
          this.loading.set(false);
        },
        error: e => {
          this.loading.set(false);
          this.handle(e);
        },
      });
    this.route.queryParamMap
      .pipe(
        map(p => p.get('run')),
        distinctUntilChanged(),
        tap(() => {
          this.run.set(null);
          this.connectionLost.set(false);
        }),
        switchMap(id =>
          id ? pollRun(this.api, id, lost => this.connectionLost.set(lost)) : EMPTY
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: incoming => {
          const run = latestRun(this.run(), incoming);
          this.run.set(run);
          if (run.status === 'completed' && run.payAnalysisId) {
            this.loadRecent();
            this.router.navigate([], {
              relativeTo: this.route,
              queryParams: { analysis: run.payAnalysisId },
              replaceUrl: true,
            });
          }
        },
        error: e => this.handle(e),
      });
  }
  start() {
    if (this.starting()) {
      return;
    }
    this.error.set('');
    this.starting.set(true);
    this.api.createRun(startKey(START_KEY), 'pay_analysis').subscribe({
      next: run => {
        this.starting.set(false);
        clearStartKey(START_KEY);
        this.run.set(run);
        this.router.navigate([], { relativeTo: this.route, queryParams: { run: run.id } });
      },
      error: (e: CareerApiError) => {
        this.starting.set(false);
        releaseStartKey(START_KEY, e);
        this.handle(e);
      },
    });
  }
  recompute() {
    const analysis = this.analysis();
    if (!analysis || this.recomputing()) {
      return;
    }
    this.recomputing.set(true);
    this.recomputeMessage.set('');
    this.api.recomputePayAnalysis(analysis.id).subscribe({
      next: result => {
        this.recomputing.set(false);
        this.recomputeMessage.set(
          result.matches
            ? `Reproduced exactly (input hash ${result.inputHash.slice(0, 12)})`
            : `The stored analysis and a fresh calculation disagree: ${result.differences.join(', ')}`
        );
      },
      error: e => {
        this.recomputing.set(false);
        this.handle(e);
      },
    });
  }
  open(id: string) {
    this.router.navigate([], { relativeTo: this.route, queryParams: { analysis: id } });
  }
  showSource(id: string) {
    this.openDrawer(this.analysis()?.sources.filter(s => s.id === id) ?? []);
  }
  showAllSources() {
    this.openDrawer(this.analysis()?.sources ?? []);
  }
  closeDrawer() {
    this.drawer()?.nativeElement.close();
  }
  figureText(f: MarketFigure) {
    return formatFigure(f);
  }
  table(section: PayBenchmarkSection) {
    return comparisonRows(section.figures);
  }
  asOfText(section: PayBenchmarkSection) {
    const ids = new Set(section.figures.map(f => f.sourceId));
    return (
      this.analysis()
        ?.sources.filter(s => ids.has(s.id))
        .map(
          s =>
            `As of ${periodText(s.referencePeriod)} (published ${s.publishedOn}). Coverage: ${s.coverage}`
        )
        .join(' ') ?? ''
    );
  }
  dollars(value: number) {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      maximumFractionDigits: 0,
    }).format(value);
  }
  exclusions(section: PayPersonalizedSection) {
    return Object.entries(section.cohort.exclusionReasons).map(([code, count]) => ({
      code,
      text: `${count} excluded for ${EXCLUSION_COPY[code] ?? plain(code).replace(/\.$/, '').toLowerCase()}`,
    }));
  }
  reasonText(reason: string | null | undefined) {
    return reason ? (REASON_COPY[reason] ?? plain(reason)) : '';
  }
  gateStatus(status: string) {
    return GATE_STATUS_COPY[status] ?? status;
  }
  sharePercent(share: number) {
    return `${(share * 100).toFixed(1)}%`;
  }
  intervalUnit(unit: string) {
    return /year/i.test(unit) ? 'per year' : /hour/i.test(unit) ? 'per hour' : '';
  }
  areaLabel(s: PayScenarioSection) {
    return s.benchmarkAreaTitle || s.benchmarkAreaCode || 'U.S.';
  }
  paySource(s: PayScenarioSection) {
    const code = (s.requestedPaySource ?? '').toLowerCase();
    if (code.includes('min')) {
      return "your goal's minimum desired pay";
    }
    return code.includes('max') ? "your goal's maximum desired pay" : '';
  }
  private openDrawer(sources: MarketSource[]) {
    this.drawerSources.set(sources);
    const dialog = this.drawer()?.nativeElement;
    if (dialog && !dialog.open) {
      dialog.showModal();
    }
  }
  private loadRecent() {
    this.api
      .listPayAnalyses()
      .subscribe({ next: list => this.recent.set(list.analyses), error: e => this.handle(e) });
  }
  private handle(e: CareerApiError) {
    if (e.kind === 'disabled') {
      this.router.navigateByUrl('/app');
      return;
    }
    if (e.kind === 'unauthorized') {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: PATH } });
      return;
    }
    if (e.kind === 'occupationRequired' || e.code === 'CareerGoalNotFound') {
      this.occupationMissing.set(true);
      return;
    }
    this.error.set(
      e.kind === 'notFound'
        ? 'That pay analysis no longer exists.'
        : e.message || 'Something went wrong. Try again.'
    );
  }
}
