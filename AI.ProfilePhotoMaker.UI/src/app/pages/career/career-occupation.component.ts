import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, distinctUntilChanged, map, switchMap, tap, throwError } from 'rxjs';
import {
  CareerApiError,
  CareerProfileService,
  CareerRunDto,
  CareerRunStatus,
  OccupationCandidate,
  OccupationMatchDto,
  OccupationStrength,
} from '../../services/career-profile.service';
import {
  clearStartKey,
  isActive,
  latestRun,
  pollRun,
  releaseStartKey,
  startKey,
} from './career-run';

export const OCCUPATION_START_KEY_STORAGE = 'career-occupation-start-key';
const OCCUPATION_PATH = '/app/career/occupation';

const STATUS_LABELS: Record<CareerRunStatus, string> = {
  queued: 'Waiting to start',
  working: 'Working',
  needs_input: 'Needs your answer',
  completed: 'Matches ready',
  failed: 'Could not finish',
  cancelled: 'Stopped',
};
export const STRENGTH_LABELS: Record<OccupationStrength, string> = {
  strong: 'Strong evidence',
  moderate: 'Some evidence',
  weak: 'Limited evidence',
};
const STALE_COPY = 'Your profile changed after this match. Start a new match.';

@Component({
  standalone: true,
  selector: 'app-career-occupation',
  imports: [RouterLink],
  templateUrl: './career-occupation.component.html',
  styleUrl: './career.scss',
})
export class CareerOccupationComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);

  run = signal<CareerRunDto | null>(null);
  match = signal<OccupationMatchDto | null>(null);
  starting = signal(false);
  sending = signal(false);
  cancelling = signal(false);
  confirming = signal(false);
  dismissing = signal(false);
  connectionLost = signal(false);
  loadingRun = signal(false);
  error = signal('');
  errorKind = signal<CareerApiError['kind'] | ''>('');
  choice = signal('');
  selectedCode = signal('');

  statusText = computed(() => {
    const run = this.run();
    return run ? STATUS_LABELS[run.status] : '';
  });
  active = computed(() => {
    const run = this.run();
    return !!run && isActive(run.status);
  });
  confirmedTitle = computed(() => {
    const match = this.match();
    return match?.candidates.find(c => c.code === match.confirmedCode)?.title ?? '';
  });
  stale = computed(() => !!this.match()?.profileChanged);
  canConfirm = computed(
    () =>
      this.match()?.status === 'proposed' &&
      !this.stale() &&
      !!this.selectedCode() &&
      !this.confirming()
  );

  ngOnInit() {
    this.route.queryParamMap
      .pipe(
        map(params => params.get('run')),
        distinctUntilChanged(),
        tap(id => {
          this.run.set(null);
          this.match.set(null);
          this.choice.set('');
          this.selectedCode.set('');
          this.connectionLost.set(false);
          this.loadingRun.set(!!id);
          if (id) {
            this.clearError();
          }
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
          if (run.status === 'completed' && run.occupationMatchId && !this.match()) {
            this.loadMatch(run.occupationMatchId);
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
    this.clearError();
    this.starting.set(true);
    this.api.createRun(startKey(OCCUPATION_START_KEY_STORAGE), 'occupation_match').subscribe({
      next: run => {
        this.starting.set(false);
        clearStartKey(OCCUPATION_START_KEY_STORAGE);
        this.run.set(run);
        this.router.navigate([], { relativeTo: this.route, queryParams: { run: run.id } });
      },
      error: (e: CareerApiError) => {
        this.starting.set(false);
        releaseStartKey(OCCUPATION_START_KEY_STORAGE, e);
        this.handle(e);
      },
    });
  }

  startOver() {
    this.clearError();
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  sendAnswer() {
    const run = this.run();
    const question = run?.question;
    const answer = this.choice();
    if (!run || !question || !answer || this.sending()) {
      return;
    }
    this.sending.set(true);
    this.api.answerRun(run.id, question.id, answer).subscribe({
      next: updated => {
        this.sending.set(false);
        this.choice.set('');
        this.run.set(latestRun(this.run(), updated));
      },
      error: (e: CareerApiError) => {
        this.sending.set(false);
        this.handle(e);
      },
    });
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

  confirm() {
    const match = this.match();
    const code = this.selectedCode();
    if (!match || !code || !this.canConfirm()) {
      return;
    }
    this.clearError();
    this.confirming.set(true);
    // The goal ETag is read right before saving so a goal edited elsewhere is caught.
    this.api
      .getGoal()
      .pipe(
        catchError((e: CareerApiError) =>
          throwError(() => (e.kind === 'notFound' ? { ...e, kind: 'goalRequired' } : e))
        ),
        switchMap(goal => this.api.confirmOccupationMatch(match.id, code, goal.etag))
      )
      .subscribe({
        next: () => {
          this.confirming.set(false);
          this.match.set({ ...match, status: 'confirmed', confirmedCode: code });
        },
        error: (e: CareerApiError) => {
          this.confirming.set(false);
          this.handle(e);
        },
      });
  }

  dismiss() {
    const match = this.match();
    if (!match || this.dismissing()) {
      return;
    }
    this.clearError();
    this.dismissing.set(true);
    this.api.dismissOccupationMatch(match.id).subscribe({
      next: updated => {
        this.dismissing.set(false);
        this.match.set(updated);
      },
      error: (e: CareerApiError) => {
        this.dismissing.set(false);
        this.handle(e);
      },
    });
  }

  strengthLabel(candidate: OccupationCandidate) {
    return STRENGTH_LABELS[candidate.strength];
  }

  private loadMatch(id: string) {
    this.api.getOccupationMatch(id).subscribe({
      next: match => this.match.set(match),
      error: (e: CareerApiError) => this.handle(e),
    });
  }

  private handle(e: CareerApiError) {
    switch (e.kind) {
      case 'disabled':
        this.router.navigateByUrl('/app');
        return;
      case 'unauthorized':
        this.router.navigate(['/auth/login'], { queryParams: { returnUrl: OCCUPATION_PATH } });
        return;
      case 'profileRequired':
        this.error.set('Confirm your profile before matching occupations.');
        break;
      case 'unavailable':
        this.error.set('The occupation list is not available right now. Try again later.');
        break;
      case 'goalRequired':
        this.error.set('Save a career goal first.');
        break;
      case 'matchStale':
        this.error.set(STALE_COPY);
        break;
      case 'notConfirmable':
        this.error.set('This match can no longer be used. Start a new match.');
        break;
      case 'conflict':
        this.error.set('Your goal changed in another tab. Reload and try again.');
        break;
      case 'notFound':
        this.error.set('That match no longer exists.');
        break;
      case 'notWaiting':
        this.error.set('The assistant is no longer waiting for an answer.');
        break;
      default:
        this.error.set(e.message || 'Something went wrong. Try again.');
    }
    this.errorKind.set(e.kind);
  }

  private clearError() {
    this.error.set('');
    this.errorKind.set('');
  }
}
