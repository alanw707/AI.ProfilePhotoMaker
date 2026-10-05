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
  CareerRunStatus,
  MarketBriefDto,
  MarketBriefSummary,
  MarketAlternative,
  MarketFigure,
  MarketSection,
  MarketSource,
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

export const MARKET_START_KEY_STORAGE = 'career-market-start-key';
const MARKET_PATH = '/app/career/market';

const STATUS_LABELS: Record<CareerRunStatus, string> = {
  queued: 'Waiting to start',
  working: 'Working',
  needs_input: 'Needs your answer',
  completed: 'Market brief ready',
  failed: 'Could not finish',
  cancelled: 'Stopped',
};
const REASON_COPY: Record<string, string> = {
  location_unresolved: 'Add a city and state to your goal to see local figures.',
  not_published: 'BLS does not publish this measure for this occupation.',
  CareerReferenceUnavailable: 'This data source could not be loaded.',
};

const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

/** "2025-05" reads "May 2025"; other periods ("2025-2035") stay as published. */
export function periodText(period: string) {
  const match = /^(\d{4})-(\d{2})$/.exec(period);
  return match ? `${MONTHS[Number(match[2]) - 1]} ${match[1]}` : period;
}

@Component({
  standalone: true,
  selector: 'app-career-market',
  imports: [RouterLink],
  templateUrl: './career-market.component.html',
  styleUrl: './career.scss',
})
export class CareerMarketComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private drawer = viewChild<ElementRef<HTMLDialogElement>>('drawer');

  run = signal<CareerRunDto | null>(null);
  brief = signal<MarketBriefDto | null>(null);
  recent = signal<MarketBriefSummary[]>([]);
  drawerSources = signal<MarketSource[]>([]);
  starting = signal(false);
  cancelling = signal(false);
  connectionLost = signal(false);
  loadingRun = signal(false);
  loadingBrief = signal(false);
  occupationMissing = signal(false);
  error = signal('');

  statusText = computed(() => {
    const run = this.run();
    return run ? STATUS_LABELS[run.status] : '';
  });
  active = computed(() => {
    const run = this.run();
    return !!run && isActive(run.status);
  });
  locationText = computed(() => {
    const location = this.brief()?.location;
    if (!location) {
      return '';
    }
    switch (location.resolution) {
      case 'metro':
      case 'state':
        return `Near your goal location: ${location.local?.title ?? location.input}`;
      case 'national_only':
        return 'National figures only';
      default:
        return `We could not place "${location.input ?? ''}"`;
    }
  });

  ngOnInit() {
    this.loadRecent();
    this.api.getGoal().subscribe({
      next: goal => this.occupationMissing.set(!goal.occupation),
      error: (e: CareerApiError) => this.handle(e),
    });
    // switchMap: opening another brief drops a slower response for the previous one.
    this.route.queryParamMap
      .pipe(
        map(params => params.get('brief')),
        distinctUntilChanged(),
        tap(id => {
          this.brief.set(null);
          this.loadingBrief.set(!!id);
        }),
        switchMap(id => (id ? this.api.getMarketBrief(id) : EMPTY)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: brief => {
          this.loadingBrief.set(false);
          this.brief.set(brief);
        },
        error: (e: CareerApiError) => {
          this.loadingBrief.set(false);
          this.handle(e);
        },
      });
    this.route.queryParamMap
      .pipe(
        map(params => params.get('run')),
        distinctUntilChanged(),
        tap(id => {
          this.run.set(null);
          this.connectionLost.set(false);
          this.loadingRun.set(!!id);
        }),
        switchMap(id =>
          id ? pollRun(this.api, id, lost => this.connectionLost.set(lost)) : EMPTY
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: polled => {
          const run = latestRun(this.run(), polled);
          this.run.set(run);
          this.loadingRun.set(false);
          if (run.status === 'completed' && run.marketBriefId) {
            this.loadRecent();
            this.router.navigate([], {
              relativeTo: this.route,
              queryParams: { brief: run.marketBriefId },
              replaceUrl: true,
            });
          }
        },
        error: (e: CareerApiError) => {
          this.loadingRun.set(false);
          this.connectionLost.set(false);
          this.handle(e);
        },
      });
  }

  start() {
    if (this.starting()) {
      return;
    }
    this.error.set('');
    this.starting.set(true);
    this.api.createRun(startKey(MARKET_START_KEY_STORAGE), 'market_brief').subscribe({
      next: run => {
        this.starting.set(false);
        clearStartKey(MARKET_START_KEY_STORAGE);
        this.run.set(run);
        this.router.navigate([], { relativeTo: this.route, queryParams: { run: run.id } });
      },
      error: (e: CareerApiError) => {
        this.starting.set(false);
        releaseStartKey(MARKET_START_KEY_STORAGE, e);
        this.handle(e);
      },
    });
  }

  /** Back to the start state, ready to build a new brief. */
  startOver() {
    this.error.set('');
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  cancel() {
    const run = this.run();
    if (!run || this.cancelling()) {
      return;
    }
    this.cancelling.set(true);
    this.api.cancelRun(run.id).subscribe({
      next: updated => {
        this.cancelling.set(false);
        this.run.set(latestRun(this.run(), updated));
      },
      error: (e: CareerApiError) => {
        this.cancelling.set(false);
        this.handle(e);
      },
    });
  }

  open(id: string) {
    this.router.navigate([], { relativeTo: this.route, queryParams: { brief: id } });
  }

  showSource(sourceId: string) {
    const source = this.brief()?.sources.find(s => s.id === sourceId);
    this.openDrawer(source ? [source] : []);
  }

  showAllSources() {
    this.openDrawer(this.brief()?.sources ?? []);
  }

  closeDrawer() {
    this.drawer()?.nativeElement.close();
  }

  figureText(figure: MarketFigure) {
    return formatFigure(figure);
  }

  table(section: MarketSection) {
    return comparisonRows(section.figures);
  }

  /** Local column heading; state figures are named as a fallback from the city. */
  localHeading(title: string) {
    return this.brief()?.location.resolution === 'state' ? `${title}, state figures` : title;
  }

  // The saved brief's item DTO includes note; the shared client interface predates this field.
  itemNote(item: MarketAlternative): string | null {
    return item.note ?? null;
  }

  /** "As of …" for the sources a section's figures come from, with their coverage limits. */
  asOfText(section: MarketSection) {
    const ids = [
      ...new Set(
        [...section.figures, ...section.items.flatMap(item => item.figures)].map(f => f.sourceId)
      ),
    ];
    const sources = this.brief()?.sources.filter(s => ids.includes(s.id)) ?? [];
    return sources
      .map(
        s =>
          `As of ${periodText(s.referencePeriod)} (published ${s.publishedOn}). Coverage: ${s.coverage}`
      )
      .join(' ');
  }

  reasonText(section: MarketSection) {
    return REASON_COPY[section.reason ?? ''] ?? 'This section is not available.';
  }

  private openDrawer(sources: MarketSource[]) {
    this.drawerSources.set(sources);
    const dialog = this.drawer()?.nativeElement;
    if (dialog && !dialog.open) {
      dialog.showModal();
    }
  }

  private loadRecent() {
    this.api.listMarketBriefs().subscribe({
      next: list => this.recent.set(list.briefs),
      error: (e: CareerApiError) => this.handle(e),
    });
  }

  private handle(e: CareerApiError) {
    switch (e.kind) {
      case 'disabled':
        this.router.navigateByUrl('/app');
        return;
      case 'unauthorized':
        this.router.navigate(['/auth/login'], { queryParams: { returnUrl: MARKET_PATH } });
        return;
      case 'occupationRequired':
        this.occupationMissing.set(true);
        return;
      case 'notFound':
        // No goal yet is the same dead end as no occupation.
        if (e.code === 'CareerGoalNotFound') {
          this.occupationMissing.set(true);
        } else {
          this.error.set('That market brief no longer exists.');
        }
        return;
      case 'profileRequired':
        this.error.set('Confirm your profile before building a market brief.');
        return;
      case 'unavailable':
        this.error.set('The market data is not available right now. Try again later.');
        return;
      default:
        this.error.set(e.message || 'Something went wrong. Try again.');
    }
  }
}
