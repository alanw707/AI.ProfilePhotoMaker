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
import { NgTemplateOutlet } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, distinctUntilChanged, map, switchMap, tap } from 'rxjs';
import {
  CareerApiError,
  CareerProfileService,
  CareerRunDto,
  RoadmapDto,
  RoadmapOption,
  RoadmapOptionKey,
  RoadmapRationale,
  RoadmapSummary,
  RoadmapTask,
} from '../../services/career-profile.service';
import {
  clearStartKey,
  isActive,
  latestRun,
  pollRun,
  releaseStartKey,
  startKey,
} from './career-run';
import { CareerRoadmapTrackingComponent } from './career-roadmap-tracking.component';
import { dateText } from './market-format';
import { periodText } from './career-market.component';

const PATH = '/app/career/roadmap';
const START_KEY = 'career-roadmap-start-key';
const OPTION_LABELS: Record<string, string> = {
  closest_fit: 'Closest fit',
  higher_ambition: 'Higher ambition',
  steadier_transition: 'Steadier transition',
};
const OMITTED_COPY: Record<string, string> = {
  no_supported_alternative: 'No other path is supported by the evidence yet.',
};
const SOURCE_NAMES: Record<string, string> = {
  oews: 'BLS Occupational Employment and Wage Statistics',
  projections: 'BLS Employment Projections',
  onet: 'O*NET occupation data',
};
const plain = (code: string) => {
  const text = code
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .toLowerCase();
  return text ? text.charAt(0).toUpperCase() + text.slice(1) : 'Available evidence';
};

@Component({
  standalone: true,
  selector: 'app-career-roadmap',
  imports: [RouterLink, NgTemplateOutlet, CareerRoadmapTrackingComponent],
  templateUrl: './career-roadmap.component.html',
  styleUrl: './career.scss',
})
export class CareerRoadmapComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private confirmDialog = viewChild<ElementRef<HTMLDialogElement>>('confirm');
  run = signal<CareerRunDto | null>(null);
  roadmap = signal<RoadmapDto | null>(null);
  recent = signal<RoadmapSummary[]>([]);
  selected = signal<RoadmapOptionKey | null>(null);
  starting = signal(false);
  loading = signal(false);
  accepting = signal(false);
  occupationMissing = signal(false);
  connectionLost = signal(false);
  error = signal('');
  notice = signal('');
  effortErrors = signal<Record<string, string>>({});
  effortSaved = signal('');
  active = computed(() => !!this.run() && isActive(this.run()!.status));
  proposed = computed(() => this.roadmap()?.status === 'proposed');
  selectedTitle = computed(() => {
    const key = this.selected();
    return this.roadmap()?.options.find(o => o.key === key)?.title ?? '';
  });
  chosen = computed(() => {
    const r = this.roadmap();
    return r?.options.find(o => o.key === r.selectedOption) ?? null;
  });

  ngOnInit() {
    this.loadRecent();
    this.api.getGoal().subscribe({
      next: goal => this.occupationMissing.set(!goal.occupation),
      error: e => this.handle(e),
    });
    this.route.queryParamMap
      .pipe(
        map(p => p.get('roadmap')),
        distinctUntilChanged(),
        tap(id => {
          this.roadmap.set(null);
          this.selected.set(null);
          this.notice.set('');
          this.effortSaved.set('');
          this.effortErrors.set({});
          this.loading.set(!!id);
        }),
        switchMap(id => (id ? this.api.getRoadmap(id) : EMPTY)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: r => {
          this.roadmap.set(r);
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
          if (run.status === 'completed' && run.roadmapId) {
            this.loadRecent();
            this.router.navigate([], {
              relativeTo: this.route,
              queryParams: { roadmap: run.roadmapId },
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
    this.api.createRun(startKey(START_KEY), 'roadmap').subscribe({
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
  open(id: string) {
    this.router.navigate([], { relativeTo: this.route, queryParams: { roadmap: id } });
  }
  choose(key: RoadmapOptionKey) {
    this.selected.set(key);
    this.error.set('');
  }
  askToAccept() {
    if (!this.selected()) {
      this.error.set('Choose a path first.');
      return;
    }
    this.error.set('');
    this.confirmDialog()?.nativeElement.showModal();
  }
  cancelAccept() {
    this.confirmDialog()?.nativeElement.close();
  }
  accept() {
    const roadmap = this.roadmap();
    const key = this.selected();
    if (!roadmap || !key || this.accepting()) {
      return;
    }
    this.confirmDialog()?.nativeElement.close();
    this.accepting.set(true);
    this.error.set('');
    // The goal ETag is read right before saving so a goal edited elsewhere is caught.
    this.api
      .getGoal()
      .pipe(switchMap(goal => this.api.acceptRoadmap(roadmap.id, key, goal.etag)))
      .subscribe({
        next: r => {
          this.accepting.set(false);
          this.roadmap.set(r);
          this.loadRecent();
        },
        error: (e: CareerApiError) => {
          this.accepting.set(false);
          if (e.kind === 'conflict' || e.kind === 'precondition') {
            this.api.getGoal().subscribe({ error: err => this.handle(err) });
            this.error.set(
              'Your goal changed in another tab. We reloaded it. Choose Accept this path to try again.'
            );
            return;
          }
          this.handle(e);
        },
      });
  }
  dismiss() {
    const roadmap = this.roadmap();
    if (!roadmap) {
      return;
    }
    this.api.dismissRoadmap(roadmap.id).subscribe({
      next: r => {
        this.roadmap.set(r);
        this.loadRecent();
      },
      error: e => this.handle(e),
    });
  }
  saveEffort(option: RoadmapOption, task: RoadmapTask, raw: string) {
    const roadmap = this.roadmap();
    const key = this.taskKey(option, task);
    const hours = Number(raw);
    this.effortSaved.set('');
    if (!roadmap) {
      return;
    }
    if (!raw.trim() || !Number.isFinite(hours) || hours < 0.5 || hours > 40) {
      this.effortErrors.update(e => ({ ...e, [key]: 'Enter hours between 0.5 and 40.' }));
      return;
    }
    this.effortErrors.update(e => ({ ...e, [key]: '' }));
    this.api.updateRoadmapTask(roadmap.id, task.id, hours).subscribe({
      next: r => {
        this.roadmap.set(r);
        this.effortSaved.set(`Saved ${this.hoursText(hours)} for "${task.title}".`);
        if (r.id !== roadmap.id) {
          this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { roadmap: r.id },
            replaceUrl: true,
          });
        }
        this.loadRecent();
      },
      error: (e: CareerApiError) => {
        if (e.kind === 'roadmapCycle') {
          this.effortErrors.update(x => ({
            ...x,
            [key]:
              'These tasks depend on each other in a loop, so the hours could not be saved. Nothing was changed.',
          }));
        } else if (e.kind === 'validation') {
          this.effortErrors.update(x => ({ ...x, [key]: 'Enter hours between 0.5 and 40.' }));
        } else {
          this.handle(e);
        }
      },
    });
  }
  onReplanApplied(r: RoadmapDto) {
    this.roadmap.set(r);
    if (this.route.snapshot.queryParamMap.get('roadmap') !== r.id) {
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { roadmap: r.id },
        replaceUrl: true,
      });
    }
    this.loadRecent();
  }
  taskKey(option: RoadmapOption, task: RoadmapTask) {
    return `${option.key}-${task.id}`;
  }
  optionLabel(key: string) {
    return OPTION_LABELS[key] ?? plain(key);
  }
  omittedText(reason: string) {
    return OMITTED_COPY[reason] ?? 'The evidence does not support this path yet.';
  }
  hoursText(hours: number) {
    return `${hours} ${hours === 1 ? 'hour' : 'hours'}`;
  }
  sourceText(r: RoadmapRationale) {
    const name = SOURCE_NAMES[r.sourceId] ?? plain(r.sourceId);
    return `${name}, ${periodText(r.release)}`;
  }
  dependsText(option: RoadmapOption, task: RoadmapTask) {
    const all = [...option.thisWeek, ...option.milestones.flatMap(m => m.tasks)];
    return task.dependsOn.map(d => all.find(t => t.id === d)?.title ?? d).join(', ');
  }
  allTasks(option: RoadmapOption) {
    return option.thisWeek.length + option.milestones.reduce((n, m) => n + m.tasks.length, 0);
  }
  dateText(iso: string) {
    return dateText(iso.slice(0, 10));
  }
  statusText(status: string) {
    return status === 'accepted' ? 'Accepted' : status === 'dismissed' ? 'Dismissed' : 'Proposed';
  }
  private loadRecent() {
    this.api
      .listRoadmaps()
      .subscribe({ next: list => this.recent.set(list.roadmaps), error: e => this.handle(e) });
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
    if (e.kind === 'roadmapNotProposed') {
      this.error.set('This roadmap was already accepted or dismissed.');
      return;
    }
    this.error.set(
      e.kind === 'notFound'
        ? 'That roadmap no longer exists.'
        : e.message || 'Something went wrong. Try again.'
    );
  }
}
