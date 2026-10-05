import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, map, distinctUntilChanged, switchMap, tap } from 'rxjs';
import {
  CareerApiError,
  CareerProfileService,
  CareerAllowanceDto,
  CareerRunDto,
  CareerRunStatus,
} from '../../services/career-profile.service';
import {
  clearStartKey,
  isActive,
  latestRun,
  pollRun,
  releaseStartKey,
  startKey,
  runErrorMessage,
} from './career-run';

export const START_KEY_STORAGE = 'career-summary-start-key';
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
  allowance = signal<CareerAllowanceDto | null>(null);
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
    return a ? a.remaining : null;
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
    this.api.createRun(startKey(START_KEY_STORAGE)).subscribe({
      next: run => {
        this.starting.set(false);
        clearStartKey(START_KEY_STORAGE);
        this.run.set(run);
        this.router.navigate([], { relativeTo: this.route, queryParams: { run: run.id } });
      },
      error: (e: CareerApiError) => {
        this.starting.set(false);
        releaseStartKey(START_KEY_STORAGE, e);
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

  private loadRuns() {
    this.api.listRuns().subscribe({
      next: list => {
        this.runs.set(list.runs.filter(r => r.task !== 'occupation_match'));
      },
      error: e => this.handle(e),
    });
    // The only allowance source is GET /api/career/allowance; the run DTO's allowance is legacy.
    this.api.getAllowance().subscribe({ next: a => this.allowance.set(a), error: () => undefined });
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
        this.error.set(runErrorMessage(e));
    }
    this.errorKind.set(e.kind);
  }

  private clearError() {
    this.error.set('');
    this.errorKind.set('');
  }
}
