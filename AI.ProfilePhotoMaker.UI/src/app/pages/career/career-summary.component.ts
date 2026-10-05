import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  EMPTY,
  defer,
  map,
  distinctUntilChanged,
  repeat,
  retry,
  switchMap,
  takeWhile,
  tap,
  throwError,
  timer,
} from 'rxjs';
import {
  CareerApiError,
  CareerProfileService,
  CareerRunAllowance,
  CareerRunDto,
  CareerRunStatus,
} from '../../services/career-profile.service';

export const START_KEY_STORAGE = 'career-summary-start-key';
export const POLL_INTERVAL_MS = 2000;
export const MAX_BACKOFF_MS = 15000;
const SUMMARY_PATH = '/app/career/summary';

const STATUS_LABELS: Record<CareerRunStatus, string> = {
  queued: 'Waiting to start',
  working: 'Working',
  needs_input: 'Needs your answer',
  completed: 'Draft ready',
  failed: 'Could not finish',
  cancelled: 'Stopped',
};
const ERROR_COPY: Record<string, string> = {
  CareerStepLimit: 'The assistant needed more steps than allowed, so it stopped.',
  CareerTimeLimit: 'The draft took too long, so it stopped.',
  CareerRetryLimit: 'The assistant kept running into problems, so it stopped.',
  CareerCostLimit: 'The draft would have cost more than allowed, so it stopped.',
  CareerToolNotAllowed: 'The assistant tried something it is not allowed to do, so it stopped.',
  CareerModelFailed: 'The drafting service could not produce a draft.',
  CareerQuestionExpired: 'The question went unanswered for too long, so the draft stopped.',
};
const FALLBACK_ERROR = 'The draft could not be finished.';
const ACTIVE: CareerRunStatus[] = ['queued', 'working', 'needs_input'];
const FATAL_KINDS: CareerApiError['kind'][] = ['notFound', 'unauthorized', 'disabled'];

export function isActive(status: CareerRunStatus) {
  return ACTIVE.includes(status);
}
/**
 * A poll sent before an answer or cancel can arrive after it. Keep whichever state of
 * the same run the server wrote last, so an old "needs your answer" never comes back.
 */
export function latestRun(current: CareerRunDto | null, incoming: CareerRunDto): CareerRunDto {
  if (!current || current.id !== incoming.id) {
    return incoming;
  }
  return Date.parse(incoming.updatedAt) < Date.parse(current.updatedAt) ? current : incoming;
}

export function backoffDelay(attempt: number) {
  return Math.min(POLL_INTERVAL_MS * 2 ** attempt, MAX_BACKOFF_MS);
}

@Component({
  standalone: true,
  selector: 'app-career-summary',
  imports: [RouterLink, DatePipe],
  templateUrl: './career-summary.component.html',
  styleUrl: './career.scss',
})
export class CareerSummaryComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);

  run = signal<CareerRunDto | null>(null);
  runs = signal<CareerRunDto[]>([]);
  allowance = signal<CareerRunAllowance | null>(null);
  starting = signal(false);
  sending = signal(false);
  cancelling = signal(false);
  connectionLost = signal(false);
  error = signal('');
  errorKind = signal<CareerApiError['kind'] | ''>('');
  answer = signal('');
  loadingRun = signal(false);

  draftsLeft = computed(() => {
    const a = this.allowance();
    return a ? Math.max(0, a.limit - a.used - a.reserved) : null;
  });
  statusText = computed(() => {
    const run = this.run();
    return run ? STATUS_LABELS[run.status] : '';
  });
  active = computed(() => {
    const run = this.run();
    return !!run && isActive(run.status);
  });
  failureText = computed(() => ERROR_COPY[this.run()?.errorCode ?? ''] ?? FALLBACK_ERROR);

  ngOnInit() {
    this.loadRuns();
    this.route.queryParamMap
      .pipe(
        map(params => params.get('run')),
        distinctUntilChanged(),
        tap(id => {
          this.run.set(null);
          this.answer.set('');
          this.connectionLost.set(false);
          this.loadingRun.set(!!id);
          if (id) {
            this.clearError();
          }
        }),
        switchMap(id => (id ? this.poll(id) : EMPTY)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: polled => {
          const run = latestRun(this.run(), polled);
          this.run.set(run);
          this.loadingRun.set(false);
          if (!isActive(run.status)) {
            this.loadRuns();
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
    this.api.createRun(this.startKey()).subscribe({
      next: run => {
        this.starting.set(false);
        sessionStorage.removeItem(START_KEY_STORAGE);
        this.run.set(run);
        this.router.navigate([], { relativeTo: this.route, queryParams: { run: run.id } });
      },
      error: (e: CareerApiError) => {
        this.starting.set(false);
        // Only a lost connection may be retried with the same key; every other
        // answer means the next click is a new request.
        if (e.kind !== 'unknown') {
          sessionStorage.removeItem(START_KEY_STORAGE);
        }
        this.handle(e);
      },
    });
  }

  newDraft() {
    this.clearError();
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  sendAnswer() {
    const run = this.run();
    const question = run?.question;
    const text = this.answer().trim();
    if (!run || !question || !text || this.sending()) {
      return;
    }
    this.sending.set(true);
    this.api.answerRun(run.id, question.id, text).subscribe({
      next: updated => {
        this.sending.set(false);
        this.answer.set('');
        this.run.set(updated);
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
        this.run.set(updated);
        this.loadRuns();
      },
      error: (e: CareerApiError) => {
        this.cancelling.set(false);
        this.handle(e);
      },
    });
  }

  statusLabel(status: CareerRunStatus) {
    return STATUS_LABELS[status];
  }

  /** One key per user intent; survives a retry after a network error. */
  private startKey(): string {
    let key = sessionStorage.getItem(START_KEY_STORAGE);
    if (!key) {
      key = crypto.randomUUID();
      sessionStorage.setItem(START_KEY_STORAGE, key);
    }
    return key;
  }

  private poll(id: string) {
    return defer(() => this.api.getRun(id)).pipe(
      retry({
        resetOnSuccess: true,
        delay: (e: CareerApiError, attempt) => {
          if (FATAL_KINDS.includes(e.kind)) {
            return throwError(() => e);
          }
          this.connectionLost.set(true);
          return timer(backoffDelay(attempt - 1));
        },
      }),
      tap(() => this.connectionLost.set(false)),
      repeat({ delay: POLL_INTERVAL_MS }),
      takeWhile(run => isActive(run.status), true)
    );
  }

  private loadRuns() {
    this.api.listRuns().subscribe({
      next: list => {
        this.runs.set(list.runs);
        this.allowance.set(list.allowance);
      },
      error: e => this.handle(e),
    });
  }

  private handle(e: CareerApiError) {
    switch (e.kind) {
      case 'disabled':
        this.router.navigateByUrl('/app');
        return;
      case 'unauthorized':
        this.router.navigate(['/auth/login'], { queryParams: { returnUrl: SUMMARY_PATH } });
        return;
      case 'allowance':
        this.error.set('You have used all drafts for this month.');
        break;
      case 'unavailable':
        this.error.set('Drafting is not available yet.');
        break;
      case 'profileRequired':
        this.error.set('Confirm your profile before drafting a summary.');
        break;
      case 'notFound':
        this.error.set('That draft no longer exists.');
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
