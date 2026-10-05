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
const PAGES: { label: string; path: string }[] = [
  { label: 'Profile and goal', path: 'profile' },
  { label: 'Import from a resume', path: 'import' },
  { label: 'Profile summary', path: 'summary' },
  { label: 'Occupation', path: 'occupation' },
  { label: 'Market brief', path: 'market' },
  { label: 'Compare markets', path: 'markets' },
  { label: 'Pay analysis', path: 'pay' },
  { label: 'Open postings', path: 'jobs' },
  { label: 'Career roadmap', path: 'roadmap' },
  { label: 'Targeted resume', path: 'resume' },
  { label: 'Professional summary', path: 'summary-draft' },
  { label: 'Materials and photo', path: 'materials' },
  { label: 'Privacy and your data', path: 'privacy' },
];

@Component({
  standalone: true,
  selector: 'app-career-home',
  imports: [RouterLink, DatePipe],
  template: ` <main class="career-page">
    <div class="career-sheet">
      <h1>Career workspace</h1>
      <p>Your goal, next step and latest work in one place. You enter and confirm every detail.</p>
      @if (allowance(); as a) {
        <section aria-labelledby="allowance-heading" data-allowance>
          <h2 id="allowance-heading">Drafting allowance</h2>
          <p data-allowance-count>
            {{ a.remaining }} of {{ a.limit }} drafts left this month
            @if (a.reserved > 0) {
              · <span data-allowance-reserved>{{ a.reserved }} in progress</span>
            }
          </p>
          <p data-allowance-reset>Resets on {{ a.resetsAt | date: 'longDate' : 'UTC' }}.</p>
          @if (a.remaining <= 0) {
            <p data-allowance-used>
              You have used this month's drafts. Your saved work stays readable, editable and
              exportable:
              <a routerLink="/app/career/profile">edit your profile</a>,
              <a routerLink="/app/career/materials">export your materials</a>.
            </p>
          }
        </section>
      }
      @if (journey(); as j) {
        <section aria-labelledby="goal-heading">
          <h2 id="goal-heading">Your goal</h2>
          @if (j.goal) {
            <p data-goal>
              {{ j.goal.occupationTitle || 'Goal saved' }}
              @if (j.goal.location) {
                · {{ j.goal.location }}
              }
            </p>
          } @else {
            <p>You have not set a goal yet. Set one to get a tailored plan.</p>
            <a routerLink="/app/career/setup">Set your goal</a>
          }
        </section>
        @if (next(); as n) {
          <section aria-labelledby="next-heading">
            <h2 id="next-heading">Next step</h2>
            <a class="primary" data-next [routerLink]="n.link" [queryParams]="n.params">{{
              n.label
            }}</a>
          </section>
        }
        @if (latest(); as l) {
          <section aria-labelledby="latest-heading">
            <h2 id="latest-heading">Latest result</h2>
            <p>
              <a data-latest [routerLink]="l.link" [queryParams]="l.params">{{ l.label }}</a>
              · {{ l.at | date: 'mediumDate' }}
            </p>
          </section>
        }
        @if (runs().length) {
          <section aria-labelledby="runs-heading">
            <h2 id="runs-heading">Work in progress</h2>
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
          </section>
        }
        @for (s of stale(); track s.id) {
          <p class="caution" data-stale>
            Needs review · Your {{ s.label }} may be out of date because your profile or goal
            changed.
            <a data-stale-link [routerLink]="s.link" [queryParams]="s.params">Open</a>
          </p>
        }
      } @else if (!loading()) {
        <section>
          <h2>Start with your facts</h2>
          <a class="primary" routerLink="/app/career/setup">Set up your profile and goal</a>
        </section>
      }
      <nav aria-labelledby="pages-heading">
        <h2 id="pages-heading">Your career pages</h2>
        <ul>
          @for (p of pages; track p.path) {
            <li>
              <a [routerLink]="'/app/career/' + p.path">{{ p.label }}</a>
            </li>
          }
        </ul>
      </nav>
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
  pages = PAGES;
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
