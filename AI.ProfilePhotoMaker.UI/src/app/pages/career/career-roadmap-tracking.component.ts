import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import {
  CareerApiError,
  CareerProfileService,
  ReplanChange,
  ReplanDto,
  RoadmapDto,
  TaskProgress,
  TaskStatus,
} from '../../services/career-profile.service';

interface Draft {
  status?: TaskStatus;
  effort?: string;
  note?: string;
}
interface Saved {
  selected: string | null;
  drafts: Record<string, Draft>;
}
const STATUS_LABELS: Record<TaskStatus, string> = {
  not_started: 'Not started',
  in_progress: 'In progress',
  done: 'Done',
  blocked: 'Blocked',
};
const GROUPS = [
  { day: 0, label: 'This week' },
  { day: 30, label: 'By day 30' },
  { day: 60, label: 'By day 60' },
  { day: 90, label: 'By day 90' },
];
const FIELD_LABELS: Record<string, string> = {
  title: 'Title',
  effort: 'Time',
  effortHours: 'Time',
  milestoneDay: 'Due',
  milestone: 'Due',
  day: 'Due',
  dependencies: 'Do this after',
  dependsOn: 'Do this after',
};
const KIND_LABELS = { added: 'New task', removed: 'Task removed', changed: 'Task changed' };
const KEPT_REASONS: Record<string, string> = {
  done: 'You already finished it.',
  has_output: 'You added notes or a linked material to it.',
  human: 'You added it yourself.',
};
const HOURS_ERROR = 'Enter hours between 0.5 and 40.';
const validHours = (raw: string) => {
  const n = Number(raw);
  return raw.trim() !== '' && Number.isFinite(n) && n >= 0.5 && n <= 40;
};

@Component({
  standalone: true,
  selector: 'app-career-roadmap-tracking',
  templateUrl: './career-roadmap-tracking.component.html',
  styleUrl: './career.scss',
})
export class CareerRoadmapTrackingComponent implements OnInit {
  private api = inject(CareerProfileService);
  roadmapId = input.required<string>();
  applied = output<RoadmapDto>();
  tasks = signal<TaskProgress[]>([]);
  loaded = signal(false);
  selected = signal<string | null>(null);
  drafts = signal<Record<string, Draft>>({});
  errors = signal<Record<string, string>>({});
  saved = signal('');
  error = signal('');
  addError = signal('');
  addSaved = signal('');
  newDeps = signal<string[]>([]);
  replan = signal<ReplanDto | null>(null);
  checked = signal<string[]>([]);
  replanNotice = signal('');
  replanError = signal('');
  busy = signal(false);
  statuses = (Object.keys(STATUS_LABELS) as TaskStatus[]).map(value => ({
    value,
    label: STATUS_LABELS[value],
  }));
  groups = computed(() =>
    GROUPS.map(g => ({ ...g, tasks: this.tasks().filter(t => t.milestoneDay === g.day) }))
  );

  reset = () => {
    for (const id of ['add-title', 'add-hours']) {
      const el = document.getElementById(id) as HTMLInputElement | null;
      if (el) {
        el.value = '';
      }
    }
  };

  ngOnInit() {
    this.load(true);
  }
  private load(restore = false) {
    this.api.getProgress(this.roadmapId()).subscribe({
      next: p => {
        this.tasks.set(p.tasks ?? []);
        this.loaded.set(true);
        if (restore) {
          this.restore();
        }
      },
      error: e => {
        this.loaded.set(true);
        this.error.set(
          e.kind === 'roadmapNotAccepted'
            ? 'Accept a path before tracking progress.'
            : 'We could not load your progress. Try again later.'
        );
      },
    });
  }
  private get key() {
    return `career-roadmap-tracking:${this.roadmapId()}`;
  }
  private persist() {
    try {
      const value: Saved = { selected: this.selected(), drafts: this.drafts() };
      sessionStorage.setItem(this.key, JSON.stringify(value));
    } catch {
      // Storage may be unavailable; drafts then simply live for this page view.
    }
  }
  private restore() {
    try {
      const raw = sessionStorage.getItem(this.key);
      if (!raw) {
        return;
      }
      const value = JSON.parse(raw) as Saved;
      const ids = new Set(this.tasks().map(t => t.taskId));
      this.selected.set(value.selected && ids.has(value.selected) ? value.selected : null);
      this.drafts.set(
        Object.fromEntries(Object.entries(value.drafts ?? {}).filter(([id]) => ids.has(id)))
      );
    } catch {
      // Ignore unreadable saved drafts.
    }
  }
  select(id: string) {
    this.selected.set(this.selected() === id ? null : id);
    this.persist();
  }
  status(t: TaskProgress) {
    return this.drafts()[t.taskId]?.status ?? t.status;
  }
  effort(t: TaskProgress) {
    return this.drafts()[t.taskId]?.effort ?? (t.effortHours ?? '').toString();
  }
  note(t: TaskProgress) {
    return this.drafts()[t.taskId]?.note ?? t.outputNote ?? '';
  }
  edit(t: TaskProgress, change: Draft) {
    this.drafts.update(d => ({ ...d, [t.taskId]: { ...d[t.taskId], ...change } }));
    this.persist();
  }
  statusText(s: TaskStatus) {
    return STATUS_LABELS[s];
  }
  hoursText(h: number | null) {
    return h === null ? 'No time set' : `${h} ${h === 1 ? 'hour' : 'hours'}`;
  }
  dayText(day: number) {
    return day === 0 ? 'this week' : `Day ${day}`;
  }
  titles(ids: string[]) {
    return ids.map(id => this.tasks().find(t => t.taskId === id)?.title ?? 'another task');
  }
  save(t: TaskProgress) {
    const draft = this.drafts()[t.taskId] ?? {};
    const update: { status?: TaskStatus; effortHours?: number; outputNote?: string } = {};
    this.saved.set('');
    this.setError(t.taskId, '');
    if (draft.status !== undefined) {
      update.status = draft.status;
    }
    if (draft.effort !== undefined && draft.effort.trim() !== '') {
      if (!validHours(draft.effort)) {
        this.setError(t.taskId, HOURS_ERROR);
        return;
      }
      update.effortHours = Number(draft.effort);
    }
    if (draft.note !== undefined) {
      update.outputNote = draft.note;
    }
    if (!Object.keys(update).length) {
      this.setError(t.taskId, 'Nothing to save yet.');
      return;
    }
    this.api.updateTaskProgress(this.roadmapId(), t.taskId, update, t.etag).subscribe({
      next: fresh => {
        this.tasks.update(list => list.map(x => (x.taskId === t.taskId ? fresh : x)));
        this.drafts.update(d => {
          const { [t.taskId]: _removed, ...rest } = d;
          return rest;
        });
        this.persist();
        this.saved.set(`Saved "${fresh.title}".`);
        this.reloadBlocking();
      },
      error: (e: CareerApiError) => {
        if (e.kind === 'conflict' || e.kind === 'precondition') {
          this.setError(
            t.taskId,
            'This task changed elsewhere. We loaded the latest version. Your edits are still shown; review them and save again.'
          );
          this.load();
        } else if (e.kind === 'validation') {
          this.setError(t.taskId, 'Check the status, hours (0.5 to 40) and note, then save again.');
        } else if (e.kind === 'notFound') {
          this.setError(t.taskId, 'That task or linked material no longer exists.');
        } else {
          this.setError(t.taskId, 'We could not save this task. Try again.');
        }
      },
    });
  }
  /** Other tasks may become unblocked or blocked after a save, so refresh the list. */
  private reloadBlocking() {
    this.api.getProgress(this.roadmapId()).subscribe({
      next: p => this.tasks.set(p.tasks ?? this.tasks()),
      error: () => undefined,
    });
  }
  private setError(id: string, text: string) {
    this.errors.update(e => ({ ...e, [id]: text }));
  }
  toggleDep(id: string, on: boolean) {
    this.newDeps.update(list => (on ? [...list, id] : list.filter(x => x !== id)));
  }
  addTask(title: string, hours: string, day: string, reset: () => void) {
    this.addError.set('');
    this.addSaved.set('');
    if (!title.trim()) {
      this.addError.set('Enter a title for your task.');
      return;
    }
    if (!validHours(hours)) {
      this.addError.set(HOURS_ERROR);
      return;
    }
    const deps = this.newDeps();
    this.api
      .addHumanTask(this.roadmapId(), {
        title: title.trim(),
        effortHours: Number(hours),
        milestoneDay: Number(day),
        dependsOn: deps,
      })
      .subscribe({
        next: () => {
          this.addSaved.set(`Added "${title.trim()}".`);
          this.newDeps.set([]);
          reset();
          this.load();
        },
        error: (e: CareerApiError) =>
          this.addError.set(
            e.kind === 'roadmapCycle'
              ? 'These tasks would depend on each other in a loop, so the task was not added. Nothing was changed.'
              : e.kind === 'validation'
                ? 'Check the title, hours (0.5 to 40) and the tasks it depends on.'
                : 'We could not add this task. Try again.'
          ),
      });
  }
  check() {
    this.replanError.set('');
    this.replanNotice.set('');
    this.busy.set(true);
    this.api.replan(this.roadmapId()).subscribe({
      next: r => {
        this.busy.set(false);
        this.replan.set(r);
        this.checked.set([]);
      },
      error: (e: CareerApiError) => {
        this.busy.set(false);
        this.replanFailure(e);
      },
    });
  }
  toggleChange(id: string, on: boolean) {
    this.checked.update(list => (on ? [...list, id] : list.filter(x => x !== id)));
  }
  apply() {
    const r = this.replan();
    if (!r || this.busy()) {
      return;
    }
    if (!this.checked().length) {
      this.replanError.set('Select at least one change, or choose Keep current plan.');
      return;
    }
    this.replanError.set('');
    this.busy.set(true);
    this.api.applyReplan(r.id, this.checked()).subscribe({
      next: roadmap => {
        this.busy.set(false);
        this.replan.set(null);
        this.replanNotice.set('Your selected changes were applied. Your progress was kept.');
        this.applied.emit(roadmap);
        if (roadmap.id === this.roadmapId()) {
          this.load();
        }
      },
      error: (e: CareerApiError) => {
        this.busy.set(false);
        this.replanFailure(e);
      },
    });
  }
  keep() {
    const r = this.replan();
    if (!r) {
      return;
    }
    this.api.rejectReplan(r.id).subscribe({
      next: () => {
        this.replan.set(null);
        this.replanNotice.set('Your plan was kept as it is.');
      },
      error: (e: CareerApiError) => this.replanFailure(e),
    });
  }
  private replanFailure(e: CareerApiError) {
    if (e.kind === 'replanStale') {
      this.replan.set(null);
      this.replanError.set('Your roadmap changed after that check. Check for a new plan again.');
    } else if (e.kind === 'replanClosed') {
      this.replan.set(null);
      this.replanError.set(
        'That check was already applied or kept. Check for a new plan to see the latest.'
      );
    } else if (e.kind === 'roadmapNotAccepted') {
      this.replanError.set('Accept a path before checking for a new plan.');
    } else {
      this.replanError.set('Something went wrong. Try again.');
    }
  }
  kindText(c: ReplanChange) {
    return KIND_LABELS[c.kind];
  }
  fieldText(field: string) {
    return FIELD_LABELS[field] ?? 'Detail';
  }
  valueText(field: string, v: unknown): string {
    if (v === null || v === undefined || v === '') {
      return 'nothing';
    }
    if (Array.isArray(v)) {
      return v.length ? this.titles(v.map(String)).join(', ') : 'nothing';
    }
    if (field === 'milestoneDay' || field === 'milestone' || field === 'day') {
      return this.dayText(Number(v));
    }
    if (field === 'effort' || field === 'effortHours') {
      return this.hoursText(Number(v));
    }
    return String(v);
  }
  keptText(reason: string) {
    return KEPT_REASONS[reason] ?? 'It was kept.';
  }
}
