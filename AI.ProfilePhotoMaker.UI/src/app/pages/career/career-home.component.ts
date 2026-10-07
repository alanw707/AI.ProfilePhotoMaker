import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import {
  CareerAllowanceDto,
  CareerApiError,
  CareerJourneyDto,
  CareerProfileService,
} from '../../services/career-profile.service';

interface Target {
  label: string;
  path: string;
  param?: string;
}
const BASE = '/app/career/';

/** Fixed allowlist: unknown keys from the server render nothing. */
export const NEXT_ACTIONS: Record<string, Target> = {
  create_profile: { label: 'Create your profile', path: 'setup' },
  confirm_profile: { label: 'Confirm your profile', path: 'profile' },
  set_goal: { label: 'Set your goal', path: 'setup' },
  confirm_occupation: { label: 'Confirm your occupation', path: 'occupation' },
  build_brief: { label: 'Build your market brief', path: 'market' },
  analyze_pay: { label: 'Analyze pay', path: 'pay' },
  build_roadmap: { label: 'Build your roadmap', path: 'roadmap' },
  accept_roadmap: { label: 'Review and accept your roadmap', path: 'roadmap' },
  draft_resume: { label: 'Draft your resume', path: 'resume' },
  export_material: { label: 'Export your materials', path: 'materials' },
};
/** Latest-result kinds -> label, page and the query param that page already reads. */
export const RESULTS: Record<string, Target> = {
  occupation_match: { label: 'Occupation match', path: 'occupation' },
  market_brief: { label: 'Market brief', path: 'market', param: 'brief' },
  pay_analysis: { label: 'Pay analysis', path: 'pay', param: 'analysis' },
  roadmap: { label: 'Career roadmap', path: 'roadmap', param: 'roadmap' },
  targeted_resume: { label: 'Targeted resume', path: 'resume', param: 'material' },
  resume: { label: 'Targeted resume', path: 'resume', param: 'material' },
  professional_summary: { label: 'Professional summary', path: 'summary-draft', param: 'material' },
  summary: { label: 'Professional summary', path: 'summary-draft', param: 'material' },
};
const TASKS: Record<string, Target> = {
  profile_summary: { label: 'Profile summary', path: 'summary' },
  occupation_match: RESULTS['occupation_match'],
  market_brief: RESULTS['market_brief'],
  pay_analysis: RESULTS['pay_analysis'],
  roadmap: RESULTS['roadmap'],
  targeted_resume: RESULTS['targeted_resume'],
  professional_summary: RESULTS['professional_summary'],
};
const RUNNING = ['queued', 'running', 'working'];

@Component({
  standalone: true,
  selector: 'app-career-home',
  imports: [RouterLink, DatePipe],
  template: ` <main class="career-page career-home">
    <div class="career-sheet">
      <h1>Career workspace</h1>
      <p class="lede">
        Your goal, next step and latest work in one place. You enter and confirm every detail.
      </p>
      @if (journey(); as j) {
        <section class="brief" aria-labelledby="goal-heading">
          <h2 id="goal-heading" class="brief__label">Your goal</h2>
          @if (j.goal) {
            <p class="brief__goal" data-goal>
              {{ j.goal.occupationTitle || 'Goal saved' }}
              @if (j.goal.location) {
                <span class="brief__place">{{ j.goal.location }}</span>
              }
            </p>
          } @else {
            <p class="brief__goal brief__goal--empty">
              You have not set a goal yet. Set one to get a tailored plan.
            </p>
            <a routerLink="/app/career/setup">Set your goal</a>
          }
          @if (next(); as n) {
            <div class="brief__next">
              <h2 id="next-heading" class="brief__label">Next step</h2>
              <a class="primary" data-next [routerLink]="n.link" [queryParams]="n.params">{{
                n.label
              }}</a>
            </div>
          } @else if (j.nextAction.key === 'none') {
            <p class="brief__done" data-all-done>
              Every step is done. Open any page from the step list to review or update it.
            </p>
          }
        </section>
        @for (s of stale(); track s.id) {
          <p class="caution" data-stale>
            Needs review · Your {{ s.label }} may be out of date because your profile or goal
            changed.
            <a data-stale-link [routerLink]="s.link" [queryParams]="s.params">Open</a>
          </p>
        }
      } @else if (!loading()) {
        <section class="brief">
          <h2 class="brief__label">Start with your facts</h2>
          <a class="primary" routerLink="/app/career/setup">Set up your profile and goal</a>
        </section>
      }

      @if (allowance() || latest() || runs().length) {
        <dl class="ledger" aria-label="Workspace status">
          @if (allowance(); as a) {
            <div class="ledger__row" data-allowance>
              <dt id="allowance-heading">Drafting allowance</dt>
              <dd>
                <p data-allowance-count>
                  {{ a.remaining }} of {{ a.limit }} drafts left this month
                  @if (a.reserved > 0) {
                    · <span data-allowance-reserved>{{ a.reserved }} in progress</span>
                  }
                </p>
                <p class="muted" data-allowance-reset>
                  Resets on {{ a.resetsAt | date: 'longDate' : 'UTC' }}.
                </p>
                @if (a.remaining <= 0) {
                  <p data-allowance-used>
                    You have used this month's drafts. Your saved work stays readable, editable and
                    exportable:
                    <a routerLink="/app/career/profile">edit your profile</a>,
                    <a routerLink="/app/career/materials">export your materials</a>.
                  </p>
                }
              </dd>
            </div>
          }
          @if (latest(); as l) {
            <div class="ledger__row">
              <dt id="latest-heading">Latest result</dt>
              <dd>
                <a data-latest [routerLink]="l.link" [queryParams]="l.params">{{ l.label }}</a>
                <span class="muted"> · {{ l.at | date: 'mediumDate' }}</span>
              </dd>
            </div>
          }
          @if (runs().length) {
            <div class="ledger__row">
              <dt id="runs-heading">Work in progress</dt>
              <dd>
                <ul>
                  @for (r of runs(); track r.id) {
                    <li>
                      {{ r.label }}:
                      <a data-run [routerLink]="r.link" [queryParams]="r.params">{{
                        r.failed ? 'Try again' : 'Still working'
                      }}</a>
                    </li>
                  }
                </ul>
              </dd>
            </div>
          }
        </dl>
      }
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
    </div>
  </main>`,
  styleUrl: './career.scss',
})
export class CareerHomeComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  journey = signal<CareerJourneyDto | null>(null);
  allowance = signal<CareerAllowanceDto | null>(null);
  loading = signal(true);
  error = signal('');
  ngOnInit() {
    // The indicator is optional: if it fails, the rest of the page still works.
    this.api.getAllowance().subscribe({ next: a => this.allowance.set(a), error: () => undefined });
    this.api.getJourney().subscribe({
      next: j => {
        this.journey.set(j);
        this.loading.set(false);
      },
      error: (e: CareerApiError) => {
        this.loading.set(false);
        this.handle(e);
      },
    });
  }
  next() {
    const j = this.journey();
    const t = j ? NEXT_ACTIONS[j.nextAction?.key] : undefined;
    if (!t || !j) {
      return null;
    }
    const l = j.latestResult;
    const r = l ? RESULTS[l.kind] : undefined;
    const same = r && r.path === t.path && r.param && l;
    return {
      label: t.label,
      link: BASE + t.path,
      params: same ? { [r.param as string]: l.id } : {},
    };
  }
  latest() {
    const l = this.journey()?.latestResult;
    const t = l ? RESULTS[l.kind] : undefined;
    if (!l || !t) {
      return null;
    }
    return {
      label: t.label,
      link: BASE + t.path,
      params: t.param ? { [t.param]: l.id } : {},
      at: l.createdAt,
    };
  }
  runs() {
    const out: { id: string; label: string; link: string; params: object; failed: boolean }[] = [];
    for (const r of this.journey()?.activeRuns ?? []) {
      const t = TASKS[r.task];
      const failed = r.status === 'failed';
      if (t && (failed || RUNNING.includes(r.status))) {
        out.push({
          id: r.id,
          label: t.label,
          link: BASE + t.path,
          params: { run: r.id },
          failed,
        });
      }
    }
    return out;
  }
  stale() {
    const out: { id: string; label: string; link: string; params: object }[] = [];
    for (const s of this.journey()?.stale ?? []) {
      const t = RESULTS[s.kind];
      if (t) {
        out.push({
          id: s.id,
          label: t.label.toLowerCase(),
          link: BASE + t.path,
          params: t.param ? { [t.param]: s.id } : {},
        });
      }
    }
    return out;
  }
  private handle(e: CareerApiError) {
    if (e.kind === 'disabled') {
      this.router.navigateByUrl('/app');
    } else if (e.kind !== 'notFound') {
      this.error.set(e.message);
    }
  }
}
