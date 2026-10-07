import { Component, DestroyRef, ElementRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { EMPTY, Subject, catchError, filter, switchMap } from 'rxjs';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { CareerJourneyDto, CareerProfileService } from '../../services/career-profile.service';

export type CareerStepState = 'done' | 'current' | 'review' | 'upcoming';

export interface CareerStep {
  id: string;
  label: string;
  path: string;
  fragment?: string;
  state: CareerStepState;
  /** Text equivalent of the state; never conveyed by colour alone. */
  status: string;
}

interface StepDef {
  id: string;
  label: string;
  path: string;
  /** Journey next-action keys that mean this step is the one to do now. */
  keys: string[];
  /** Result kinds whose staleness puts this step back up for review. */
  results: string[];
}

const ROOT = '/app/career/';

/** The journey order, matching the API's next-action sequence (ADR 0021). */
const STEPS: StepDef[] = [
  {
    id: 'profile',
    label: 'Profile',
    path: 'profile',
    keys: ['create_profile', 'confirm_profile'],
    results: [],
  },
  { id: 'goal', label: 'Goal', path: 'setup', keys: ['set_goal'], results: [] },
  {
    id: 'occupation',
    label: 'Occupation',
    path: 'occupation',
    keys: ['confirm_occupation'],
    results: ['occupation_match'],
  },
  {
    id: 'market',
    label: 'Market brief',
    path: 'market',
    keys: ['build_brief'],
    results: ['market_brief'],
  },
  {
    id: 'pay',
    label: 'Pay analysis',
    path: 'pay',
    keys: ['analyze_pay'],
    results: ['pay_analysis'],
  },
  {
    id: 'roadmap',
    label: 'Roadmap',
    path: 'roadmap',
    keys: ['build_roadmap', 'accept_roadmap'],
    results: ['roadmap'],
  },
  {
    id: 'materials',
    label: 'Resume and materials',
    path: 'materials',
    keys: ['draft_resume', 'export_material'],
    results: ['targeted_resume', 'resume', 'professional_summary', 'summary'],
  },
];

export const CAREER_MORE_PAGES: { label: string; path: string }[] = [
  { label: 'Import from a resume', path: 'import' },
  { label: 'Profile summary', path: 'summary' },
  { label: 'Compare markets', path: 'markets' },
  { label: 'Open postings', path: 'jobs' },
  { label: 'Targeted resume', path: 'resume' },
  { label: 'Professional summary', path: 'summary-draft' },
  { label: 'Privacy and your data', path: 'privacy' },
];

const STATUS: Record<CareerStepState, string> = {
  done: 'Done',
  current: 'Next',
  review: 'Needs review',
  upcoming: '',
};

/**
 * Derives the rail from the journey. Only known next-action keys move the marker: an unknown
 * key (or no journey yet) claims nothing, and "none" means every step is done.
 */
export function careerSteps(j: CareerJourneyDto | null): { steps: CareerStep[]; summary: string } {
  const key = j?.nextAction?.key;
  const allDone = key === 'none';
  const current = STEPS.findIndex(s => key !== undefined && s.keys.includes(key));
  const staleKinds = new Set((j?.stale ?? []).map(s => s.kind));

  const steps = STEPS.map((def, i): CareerStep => {
    let state: CareerStepState = 'upcoming';
    if (allDone || (current >= 0 && i < current)) {
      state = 'done';
    } else if (i === current) {
      state = 'current';
    }
    if (state === 'done' && def.results.some(r => staleKinds.has(r))) {
      state = 'review';
    }
    const path = def.id === 'profile' && j && !j.profile ? 'setup' : def.path;
    // Without a profile, Profile and Goal both open setup; the Goal link carries a fragment so
    // only one link reports itself as the current page.
    const fragment = def.id === 'goal' && j && !j.profile ? 'goal' : undefined;
    return {
      id: def.id,
      label: def.label,
      path: ROOT + path,
      fragment,
      state,
      status: STATUS[state],
    };
  });

  const summary = allDone
    ? `All ${STEPS.length} steps done`
    : current >= 0
      ? `Step ${current + 1} of ${STEPS.length}: ${STEPS[current].label}`
      : 'Career steps';
  return { steps, summary };
}

@Component({
  standalone: true,
  selector: 'app-career-shell',
  imports: [HeaderNavigationComponent, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './career-shell.component.html',
  styleUrl: './career-shell.component.scss',
})
export class CareerShellComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  readonly more = CAREER_MORE_PAGES;
  readonly journey = signal<CareerJourneyDto | null>(null);
  readonly rail = computed(() => careerSteps(this.journey()));
  /** Narrow screens only: the step list is always shown on wide screens. */
  readonly open = signal(false);

  private host: ElementRef<HTMLElement> = inject(ElementRef);
  private reload = new Subject<void>();

  ngOnInit() {
    // switchMap drops a slower earlier response so the rail never shows an older journey.
    this.reload
      .pipe(
        switchMap(() => this.api.getJourney().pipe(catchError(() => EMPTY))),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(j => this.journey.set(j));
    this.load();
    this.router.events
      .pipe(
        filter(e => e instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => {
        const list = this.host.nativeElement.querySelector('#career-step-list');
        const focusInList = !!list && list.contains(document.activeElement);
        this.open.set(false);
        // Collapsing hides the focused link; hand focus back to the control that opened it.
        if (focusInList) {
          this.host.nativeElement.querySelector<HTMLElement>('.rail__toggle')?.focus();
        }
        this.load();
      });
  }

  toggle() {
    this.open.update(v => !v);
  }

  private load() {
    // The rail is guidance only: if the journey fails, the page itself still works and reports it.
    this.reload.next();
  }
}
