import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, distinctUntilChanged, map, switchMap, tap } from 'rxjs';
import {
  CareerApiError,
  CareerProfileService,
  CareerRunDto,
  ResumeChange,
  ResumeContact,
  ResumeMaterialDto,
  ResumeProposalDto,
  ResumeSection,
  ResumeSectionKey,
  ResumeVersionInfo,
} from '../../services/career-profile.service';
import {
  clearStartKey,
  isActive,
  latestRun,
  pollRun,
  releaseStartKey,
  startKey,
} from './career-run';
import { dateText } from './market-format';

const START_KEY = 'career-resume-start-key';
const SAVE_DELAY_MS = 800;
const SECTION_LABELS: Record<ResumeSectionKey, string> = {
  headline: 'Headline',
  summary: 'Summary',
  experience_highlights: 'Experience highlights',
  skills: 'Skills',
};
const CONTACT_FIELDS: { key: keyof ResumeContact; label: string }[] = [
  { key: 'name', label: 'Name' },
  { key: 'email', label: 'Email' },
  { key: 'phone', label: 'Phone' },
  { key: 'location', label: 'Location' },
  { key: 'links', label: 'Links' },
];
const AUTHORS: Record<string, string> = {
  user: 'You',
  agent: 'Drafted for you',
  system: 'Drafted for you',
};
const KIND_LABELS = { added: 'New line', removed: 'Line removed', changed: 'Line changed' };
/** Matches ResumeLimits.MaxLineLength on the API. */
export const MAX_LINE_LENGTH = 600;
const SAVING = 'Saving…';
const SAVED = 'All changes saved';
const CONFLICT = 'Not saved — changed elsewhere';
const DEFAULT_CONTACT: ResumeContact = {
  name: true,
  email: true,
  phone: false,
  location: false,
  links: false,
};
interface StoredDraft {
  etag: string;
  sections: ResumeSection[];
  contact: ResumeContact;
}

@Component({
  standalone: true,
  selector: 'app-career-resume',
  imports: [RouterLink],
  templateUrl: './career-resume.component.html',
  styleUrl: './career.scss',
})
export class CareerResumeComponent implements OnInit {
  readonly maxLineLength = MAX_LINE_LENGTH;
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private timer: ReturnType<typeof setTimeout> | null = null;
  private revision = 0;
  private savedRevision = 0;
  private etag = '';
  material = signal<ResumeMaterialDto | null>(null);
  sections = signal<ResumeSection[]>([]);
  contact = signal<ResumeContact>({ ...DEFAULT_CONTACT });
  run = signal<CareerRunDto | null>(null);
  proposal = signal<ResumeProposalDto | null>(null);
  checked = signal<string[]>([]);
  versions = signal<ResumeVersionInfo[]>([]);
  viewing = signal<ResumeMaterialDto | null>(null);
  latest = signal<ResumeMaterialDto | null>(null);
  status = signal('');
  conflict = signal(false);
  starting = signal(false);
  loading = signal(false);
  busy = signal(false);
  occupationMissing = signal(false);
  connectionLost = signal(false);
  error = signal('');
  notice = signal('');
  proposalError = signal('');
  proposalNotice = signal('');
  contactFields = CONTACT_FIELDS;
  active = computed(() => !!this.run() && isActive(this.run()!.status));
  facts = computed(() => new Map((this.material()?.facts ?? []).map(f => [f.id, f.text])));
  settled = computed(() => this.status() === SAVED || this.status() === '');
  staleText = computed(() => {
    const reasons = (this.material()?.staleReasons ?? []).join(' ').toLowerCase();
    const profile = reasons.includes('profile');
    const goal = reasons.includes('goal');
    return profile && goal
      ? 'Your profile and your career goal changed'
      : goal
        ? 'Your career goal changed'
        : 'Your profile changed';
  });

  ngOnInit() {
    this.destroyRef.onDestroy(() => this.timer && clearTimeout(this.timer));
    this.api.getGoal().subscribe({
      next: goal => this.occupationMissing.set(!goal.occupation),
      error: e => this.handle(e),
    });
    this.route.queryParamMap
      .pipe(
        map(p => p.get('material')),
        distinctUntilChanged(),
        tap(id => {
          this.material.set(null);
          this.loading.set(!!id);
          this.status.set('');
          this.conflict.set(false);
        }),
        switchMap(id => (id ? this.api.getResumeMaterial(id) : EMPTY)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: m => {
          this.loading.set(false);
          this.show(m, true);
          this.loadVersions();
          this.loadProposal();
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
          if (run.status === 'completed' && run.materialId) {
            if (run.materialId === this.material()?.id) {
              this.loadProposal(run.proposalId);
            }
            this.router.navigate([], {
              relativeTo: this.route,
              queryParams: {
                material: run.materialId,
                run: null,
                proposal: run.proposalId ?? null,
              },
              queryParamsHandling: 'merge',
              replaceUrl: true,
            });
          }
        },
        error: e => this.handle(e),
      });
  }

  private get draftKey() {
    return `career-resume-draft:${this.material()?.id}`;
  }
  private show(m: ResumeMaterialDto, restore: boolean) {
    this.material.set(m);
    this.etag = m.etag ?? `"material-v${m.currentVersion}"`;
    this.sections.set(m.sections);
    this.contact.set({ ...DEFAULT_CONTACT, ...m.contact });
    this.revision = this.savedRevision = 0;
    if (restore) {
      this.restoreDraft();
    }
  }
  private restoreDraft() {
    try {
      const raw = sessionStorage.getItem(this.draftKey);
      if (!raw) {
        return;
      }
      const d = JSON.parse(raw) as StoredDraft;
      if (!Array.isArray(d.sections)) {
        return;
      }
      this.sections.set(d.sections);
      this.contact.set({ ...DEFAULT_CONTACT, ...d.contact });
      this.etag = d.etag || this.etag;
      this.revision = 1;
      this.notice.set('We restored your unsaved changes.');
      this.queueSave(0);
    } catch {
      // Ignore an unreadable saved draft.
    }
  }
  private persist() {
    try {
      const d: StoredDraft = {
        etag: this.etag,
        sections: this.sections(),
        contact: this.contact(),
      };
      sessionStorage.setItem(this.draftKey, JSON.stringify(d));
    } catch {
      // Storage may be unavailable; the draft then lives for this page view only.
    }
  }
  private clearDraft() {
    try {
      sessionStorage.removeItem(this.draftKey);
    } catch {
      // Nothing to clear.
    }
  }
  private loadVersions() {
    const id = this.material()?.id;
    if (!id) {
      return;
    }
    this.api.listResumeVersions(id).subscribe({
      next: v => this.versions.set(v.versions),
      error: () => undefined,
    });
  }
  private loadProposal(proposalId?: string | null) {
    const id = this.material()?.id;
    const pid = proposalId ?? this.route.snapshot.queryParamMap.get('proposal');
    if (!id || !pid) {
      return;
    }
    this.proposalNotice.set('');
    this.api.getResumeProposal(id, pid).subscribe({
      next: p => {
        this.proposal.set(p);
        this.checked.set([]);
      },
      error: () => undefined,
    });
  }

  // ---- drafting -------------------------------------------------------------
  start(refresh = false) {
    if (this.starting()) {
      return;
    }
    this.error.set('');
    this.proposalError.set('');
    this.proposalNotice.set('');
    this.starting.set(true);
    const key = START_KEY + (refresh ? '-refresh' : '');
    this.api
      .createRun(startKey(key), 'targeted_resume', refresh ? this.material()?.id : undefined)
      .subscribe({
        next: run => {
          this.starting.set(false);
          clearStartKey(key);
          this.run.set(run);
          this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { run: run.id },
            queryParamsHandling: 'merge',
          });
        },
        error: (e: CareerApiError) => {
          this.starting.set(false);
          releaseStartKey(key, e);
          this.handle(e);
        },
      });
  }

  // ---- editing --------------------------------------------------------------
  sectionLabel(key: ResumeSectionKey) {
    return SECTION_LABELS[key] ?? 'Section';
  }
  basedOn(ids: string[]) {
    return ids
      .map(id => this.facts().get(id))
      .filter(Boolean)
      .join('; ');
  }
  editLine(key: ResumeSectionKey, lineId: string, text: string) {
    this.update(key, lines =>
      lines.map(l => (l.id === lineId ? { ...l, text, origin: 'human' as const } : l))
    );
  }
  addLine(key: ResumeSectionKey, text = '') {
    const id = `line-${crypto.randomUUID()}`;
    this.update(key, lines => [...lines, { id, text, factIds: [], origin: 'human' as const }]);
    setTimeout(() => (document.getElementById(`ln-${id}`) as HTMLElement | null)?.focus());
  }
  removeLine(key: ResumeSectionKey, lineId: string) {
    this.update(key, lines => lines.filter(l => l.id !== lineId));
  }
  private update(
    key: ResumeSectionKey,
    fn: (lines: ResumeSection['lines']) => ResumeSection['lines']
  ) {
    this.sections.update(list => list.map(s => (s.key === key ? { ...s, lines: fn(s.lines) } : s)));
    this.changed();
  }
  toggleContact(key: keyof ResumeContact, on: boolean) {
    this.contact.update(c => ({ ...c, [key]: on }));
    this.changed();
  }
  private changed() {
    this.revision++;
    this.persist();
    if (!this.conflict()) {
      this.status.set(SAVING);
      this.queueSave(SAVE_DELAY_MS);
    }
  }
  private queueSave(delay: number) {
    if (this.timer) {
      clearTimeout(this.timer);
    }
    this.status.set(SAVING);
    this.timer = setTimeout(() => this.save(), delay);
  }
  private save() {
    const m = this.material();
    if (!m || this.conflict()) {
      return;
    }
    const sent = this.revision;
    this.api
      .saveResume(m.id, { sections: this.sections(), contact: this.contact() }, this.etag)
      .subscribe({
        next: fresh => {
          this.etag = fresh.etag ?? `"material-v${fresh.currentVersion}"`;
          this.savedRevision = sent;
          // Metadata always follows the server; line text only when no newer local edit exists.
          this.material.update(cur =>
            cur
              ? {
                  ...cur,
                  etag: this.etag,
                  currentVersion: fresh.currentVersion,
                  pinned: fresh.pinned ?? cur.pinned,
                  questions: fresh.questions ?? cur.questions,
                  facts: fresh.facts ?? cur.facts,
                  stale: fresh.stale ?? cur.stale,
                  staleReasons: fresh.staleReasons ?? cur.staleReasons,
                  contact: fresh.contact ?? cur.contact,
                }
              : cur
          );
          if (this.revision === sent) {
            if (fresh.contact) {
              this.contact.set({ ...DEFAULT_CONTACT, ...fresh.contact });
            }
            this.clearDraft();
            this.status.set(SAVED);
          } else {
            this.persist();
            this.queueSave(SAVE_DELAY_MS);
          }
          this.loadVersions();
        },
        error: (e: CareerApiError) => {
          if (e.kind === 'conflict' || e.kind === 'precondition') {
            this.conflict.set(true);
            this.status.set(CONFLICT);
          } else if (e.kind === 'unsupportedClaim') {
            this.status.set('Not saved');
            this.error.set(
              'A line we wrote is no longer backed by your profile. Change its wording to make it yours, or remove it. Your text is kept.'
            );
          } else if (e.kind === 'validation') {
            this.status.set('Not saved');
            this.error.set('A line is too long or there are too many lines. Your text is kept.');
          } else {
            this.status.set('Not saved — will retry when you edit');
          }
        },
      });
  }

  // ---- conflict choices -----------------------------------------------------
  compare() {
    const id = this.material()?.id;
    if (!id) {
      return;
    }
    this.api.getResumeMaterial(id).subscribe({
      next: m => this.latest.set(m),
      error: e => this.handle(e),
    });
  }
  keepMine() {
    const id = this.material()?.id;
    if (!id) {
      return;
    }
    this.api.getResumeMaterial(id).subscribe({
      next: m => {
        this.etag = m.etag ?? `"material-v${m.currentVersion}"`;
        this.latest.set(null);
        this.conflict.set(false);
        this.queueSave(0);
      },
      error: e => this.handle(e),
    });
  }
  useLatest() {
    const m = this.latest();
    const go = (fresh: ResumeMaterialDto) => {
      this.clearDraft();
      this.show(fresh, false);
      this.latest.set(null);
      this.conflict.set(false);
      this.status.set(SAVED);
      this.loadVersions();
    };
    if (m) {
      go(m);
    } else if (this.material()) {
      this.api
        .getResumeMaterial(this.material()!.id)
        .subscribe({ next: go, error: e => this.handle(e) });
    }
  }

  // ---- versions -------------------------------------------------------------
  authorText(a: string) {
    return AUTHORS[a] ?? 'Someone else';
  }
  dateText(iso: string) {
    return dateText(iso.slice(0, 10));
  }
  viewVersion(n: number) {
    this.api.getResumeVersion(this.material()!.id, n).subscribe({
      next: v => {
        const m = this.material()!;
        this.viewing.set({
          id: m.id,
          title: m.title,
          etag: `"material-v${v.number}"`,
          currentVersion: v.number,
          pinned: v.pinned,
          stale: false,
          staleReasons: [],
          contact: v.contact,
          sections: v.sections,
          questions: v.questions,
          facts: v.facts,
        });
      },
      error: e => this.handle(e),
    });
  }
  closeVersion() {
    this.viewing.set(null);
  }
  restore(n: number) {
    const m = this.material();
    if (!m || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set('');
    this.api.restoreResumeVersion(m.id, n, this.etag).subscribe({
      next: () => {
        this.busy.set(false);
        this.viewing.set(null);
        this.clearDraft();
        this.api.getResumeMaterial(m.id).subscribe({
          next: fresh => {
            this.show(fresh, false);
            this.status.set(SAVED);
            this.notice.set(`Version ${n} was restored as a new version. Nothing was deleted.`);
            this.loadVersions();
          },
          error: e => this.handle(e),
        });
      },
      error: (e: CareerApiError) => {
        this.busy.set(false);
        if (e.kind === 'conflict' || e.kind === 'precondition') {
          this.error.set('This resume changed elsewhere. Reload it and try again.');
        } else {
          this.handle(e);
        }
      },
    });
  }

  // ---- proposal -------------------------------------------------------------
  kindText(c: ResumeChange) {
    return `${KIND_LABELS[c.kind]} in ${SECTION_LABELS[c.section] ?? 'your resume'}`;
  }
  toggleChange(id: string, on: boolean) {
    this.checked.update(l => (on ? [...l, id] : l.filter(x => x !== id)));
  }
  apply() {
    const m = this.material();
    const p = this.proposal();
    if (!m || !p || this.busy()) {
      return;
    }
    if (!this.checked().length) {
      this.proposalError.set('Select at least one change, or choose Keep my version.');
      return;
    }
    this.proposalError.set('');
    this.busy.set(true);
    this.api.applyResumeProposal(m.id, p.id, this.checked(), this.etag).subscribe({
      next: () => {
        this.busy.set(false);
        this.proposal.set(null);
        this.clearProposalParam();
        this.clearDraft();
        this.api.getResumeMaterial(m.id).subscribe({
          next: fresh => {
            this.show(fresh, false);
            this.status.set(SAVED);
            this.proposalNotice.set('Your selected changes were applied. Nothing else changed.');
            this.loadVersions();
          },
          error: e => this.handle(e),
        });
      },
      error: (e: CareerApiError) => {
        this.busy.set(false);
        if (e.kind === 'conflict' || e.kind === 'precondition') {
          this.proposal.set(null);
          this.clearProposalParam();
          this.proposalError.set(
            'Your resume changed after that check, so nothing was applied. Your edits were kept. Refresh from my profile to check again.'
          );
        } else {
          this.proposalError.set('We could not apply those changes. Your edits were kept.');
        }
      },
    });
  }
  keep() {
    const m = this.material();
    const p = this.proposal();
    if (!m || !p) {
      return;
    }
    this.api.rejectResumeProposal(m.id, p.id).subscribe({
      next: () => {
        this.proposal.set(null);
        this.clearProposalParam();
        this.proposalNotice.set('Your version was kept as it is.');
      },
      error: () => this.proposalError.set('Something went wrong. Try again.'),
    });
  }
  private clearProposalParam() {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { proposal: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
  private handle(e: CareerApiError) {
    if (e.kind === 'disabled') {
      this.router.navigateByUrl('/app');
    } else if (e.kind === 'unauthorized') {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: '/app/career/resume' } });
    } else if (e.kind === 'occupationRequired' || e.kind === 'goalRequired') {
      this.occupationMissing.set(true);
    } else if (e.kind === 'profileRequired') {
      this.error.set('Add your profile first so we have facts to use.');
    } else if (e.kind === 'allowance') {
      this.error.set('You have used this period’s agent runs. Try again later.');
    } else if (e.kind === 'notFound') {
      this.error.set('We could not find that resume.');
    } else {
      this.error.set('Something went wrong. Try again.');
    }
  }
}
