import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
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
  JobObservation,
  JobObservations,
  RemoteFilter,
} from '../../services/career-profile.service';
import { dateText } from './market-format';
import {
  REASON_COPY,
  REASON_FALLBACK,
  exclusionLines,
  locationLines,
  payText,
  remoteText,
} from './job-format';

export const JOBS_PATH = '/app/career/jobs';
const SEARCH_DELAY_MS = 300;
const REMOTE_VALUES: RemoteFilter[] = ['all', 'eligible', 'ineligible', 'unknown'];

interface Filters {
  area: string;
  eligibleOnly: boolean;
  remote: RemoteFilter;
  q: string;
}
const NO_FILTERS: Filters = { area: '', eligibleOnly: false, remote: 'all', q: '' };

/** Open postings from one source, with the coverage and what was left out stated plainly. */
@Component({
  standalone: true,
  selector: 'app-career-jobs',
  imports: [RouterLink],
  templateUrl: './career-jobs.component.html',
  styleUrl: './career.scss',
})
export class CareerJobsComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private typing = new Subject<Partial<Filters>>();

  result = signal<JobObservations | null>(null);
  goal = signal<CareerGoalDto | null>(null);
  occupationMissing = signal(false);
  loading = signal(true);
  error = signal('');

  area = signal('');
  eligibleOnly = signal(false);
  remote = signal<RemoteFilter>('all');
  q = signal('');

  coverage = computed(() => this.result()?.coverage ?? null);
  /** Plain-language note when the first search found nothing and the API widened it. */
  broadenedNote = computed(() => {
    const search = this.result()?.search;
    const steps = search?.broadened ?? [];
    if (!search || steps.length === 0) {return null;}
    const parts: string[] = [];
    if (steps.includes('keyword')) {parts.push(`searched for “${search.keyword}” instead`);}
    if (steps.includes('area') && search.areaTitle) {parts.push(`across ${search.areaTitle}`);}
    return `Nothing matched the first search, so we ${parts.join(' ')}.`;
  });
  /** Set when no occupation is confirmed and the source was searched by the goal's target role. */
  roleSearch = computed(() => {
    const search = this.result()?.search;
    return search?.basis === 'target_role' ? search.keyword : null;
  });
  unavailableText = computed(() => REASON_COPY[this.coverage()?.reason ?? ''] ?? REASON_FALLBACK);
  exclusions = computed(() => {
    const counts = this.coverage()?.counts;
    return counts ? exclusionLines(counts) : [];
  });
  /** What the area filter currently means: the typed text, else the resolved default area. */
  areaName = computed(() => {
    const r = this.result();
    return this.area() || r?.area.title || r?.area.input || 'your preferred area';
  });
  /** The saved preference and the current filter, only when the two differ. */
  staleText = computed(() => {
    const r = this.result();
    if (!r?.preferences.stalePreference) {
      return '';
    }
    const goal = this.goal();
    const saved = goal?.preferredArea?.title ?? goal?.goal.targetLocation;
    return saved
      ? `Your saved location is ${saved}; this filter is ${this.areaName()}.`
      : (r.preferences.note ?? '');
  });
  filterText = computed(() => {
    const parts = [`area: ${this.areaName()}`];
    if (this.q()) {
      parts.push(`search: "${this.q()}"`);
    }
    if (this.eligibleOnly()) {
      parts.push('only postings that state remote work');
    }
    if (this.remote() !== 'all') {
      parts.push(`remote: ${remoteText(this.remote() as 'eligible')}`);
    }
    return parts.join(', ');
  });
  hasFilters = computed(
    () => !!this.area() || !!this.q() || this.eligibleOnly() || this.remote() !== 'all'
  );

  ngOnInit() {
    this.api.getGoal().subscribe({
      next: goal => this.goal.set(goal),
      error: () => undefined, // the page works without a goal; only the default area hint is lost
    });
    this.typing
      .pipe(debounceTime(SEARCH_DELAY_MS), takeUntilDestroyed(this.destroyRef))
      .subscribe(patch => this.go(patch));
    // The URL is the only filter state, so a reload restores every filter.
    this.route.queryParamMap
      .pipe(
        map(params => this.readFilters(params)),
        tap(filters => this.apply(filters)),
        distinctUntilChanged(
          (a, b) =>
            a.area === b.area &&
            a.eligibleOnly === b.eligibleOnly &&
            a.remote === b.remote &&
            a.q === b.q
        ),
        tap(() => {
          this.loading.set(true);
          this.error.set('');
        }),
        switchMap(filters =>
          this.api
            .getJobObservations(filters)
            .pipe(catchError((e: CareerApiError) => this.failed(e)))
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(result => {
        this.loading.set(false);
        if (result) {
          this.result.set(result);
          // A 200 can still say no search was possible; show the way forward, not a dead end.
          this.occupationMissing.set(result.coverage?.reason === 'occupation_required');
        }
      });
  }

  onArea(value: string) {
    this.area.set(value);
    this.typing.next({ area: value.trim() });
  }
  onSearch(value: string) {
    this.q.set(value);
    this.typing.next({ q: value.trim() });
  }
  setEligibleOnly(value: boolean) {
    this.go({ eligibleOnly: value });
  }
  setRemote(value: string) {
    this.go({
      remote: REMOTE_VALUES.includes(value as RemoteFilter) ? (value as RemoteFilter) : 'all',
    });
  }
  clear() {
    this.go(NO_FILTERS);
  }

  payText(o: JobObservation) {
    return payText(o.pay);
  }
  remoteText(o: JobObservation) {
    return remoteText(o.remoteEligibility);
  }
  locations(o: JobObservation) {
    return locationLines(o);
  }
  dateText(iso: string) {
    return dateText(iso);
  }
  seriesGrade(o: JobObservation) {
    return [o.series, o.grade].filter(Boolean).join(', ');
  }
  trackObservation(o: JobObservation) {
    return o.observationId;
  }

  private readFilters(params: ParamMap): Filters {
    const remote = params.get('remote') as RemoteFilter;
    return {
      area: params.get('area') ?? '',
      eligibleOnly: params.get('eligibleOnly') === 'true',
      remote: REMOTE_VALUES.includes(remote) ? remote : 'all',
      q: params.get('q') ?? '',
    };
  }
  private apply(filters: Filters) {
    this.area.set(filters.area);
    this.eligibleOnly.set(filters.eligibleOnly);
    this.remote.set(filters.remote);
    this.q.set(filters.q);
  }
  private go(patch: Partial<Filters>) {
    const next = {
      area: this.area().trim(),
      eligibleOnly: this.eligibleOnly(),
      remote: this.remote(),
      q: this.q().trim(),
      ...patch,
    };
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        area: next.area || null,
        eligibleOnly: next.eligibleOnly ? 'true' : null,
        remote: next.remote === 'all' ? null : next.remote,
        q: next.q || null,
      },
    });
  }
  private failed(e: CareerApiError) {
    this.result.set(null);
    switch (e.kind) {
      case 'disabled':
        this.router.navigateByUrl('/app');
        break;
      case 'unauthorized':
        this.router.navigate(['/auth/login'], { queryParams: { returnUrl: JOBS_PATH } });
        break;
      case 'occupationRequired':
        this.occupationMissing.set(true);
        break;
      default:
        this.error.set(e.message || 'Something went wrong. Try again.');
    }
    return of(null);
  }
}
