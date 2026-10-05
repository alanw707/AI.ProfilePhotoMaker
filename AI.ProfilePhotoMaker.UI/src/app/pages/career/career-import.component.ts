import {
  Component,
  ElementRef,
  Injector,
  OnDestroy,
  OnInit,
  afterNextRender,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import {
  CareerApiError,
  CareerProfileProposalDto,
  CareerProfileService,
  ProposalField,
  ProposalItem,
  ResumeDocumentDto,
} from '../../services/career-profile.service';

export const MAX_RESUME_BYTES = 10 * 1024 * 1024;
const TOO_LARGE = 'This file is too large. Files can be up to 10 MB and 25 pages.';
const WRONG_TYPE = 'Choose a PDF or DOCX file. Older .doc files are not supported.';
const UNREADABLE =
  'We could not read text from this file (it may be a scan). Paste your resume text instead.';
const STALE =
  'Your profile changed since these suggestions were made. Reload your profile and review again.';

const FIELD_ORDER: ProposalField[] = [
  'currentTitle',
  'industry',
  'yearsExperience',
  'location',
  'summary',
  'skills',
  'highlights',
];
const FIELD_LABELS: Record<ProposalField, string> = {
  currentTitle: 'Current title',
  industry: 'Industry',
  yearsExperience: 'Years of experience',
  location: 'Location',
  summary: 'Summary',
  skills: 'Skills',
  highlights: 'Highlights',
};
const FLAG_LABELS: Record<string, string> = {
  conflict: 'Dates conflict - check this',
  ambiguous: 'Vague - add specifics',
};

type Phase = 'idle' | 'uploading';

@Component({
  standalone: true,
  selector: 'app-career-import',
  imports: [RouterLink, DatePipe],
  templateUrl: './career-import.component.html',
  styleUrl: './career.scss',
})
export class CareerImportComponent implements OnInit, OnDestroy {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private injector = inject(Injector);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private timers: ReturnType<typeof setTimeout>[] = [];

  consent = signal(false);
  file = signal<File | null>(null);
  fieldErrors = signal<{ consent?: string; file?: string }>({});
  error = signal('');
  errorKind = signal<CareerApiError['kind'] | ''>('');
  stale = signal(false);
  phase = signal<Phase>('idle');
  progress = signal('');
  status = signal('');
  resumes = signal<ResumeDocumentDto[]>([]);
  confirmRemoveId = signal<string | null>(null);
  result = signal<ResumeDocumentDto | null>(null);
  pasteText = signal('');
  pasteError = signal('');
  pasting = signal(false);
  proposal = signal<CareerProfileProposalDto | null>(null);
  selected = signal<ReadonlySet<string>>(new Set());
  accepting = signal(false);

  groups = computed(() => {
    const items = this.proposal()?.items ?? [];
    return FIELD_ORDER.map(field => ({
      field,
      label: FIELD_LABELS[field],
      items: items.filter(i => i.field === field),
    })).filter(g => g.items.length > 0);
  });
  selectedCount = computed(() => this.selected().size);
  showPaste = computed(() => {
    const result = this.result();
    return !this.proposal() && !!result && result.state !== 'ready';
  });

  ngOnInit() {
    // Loads the current profile so its ETag is known before accepting.
    this.api.getProfile().subscribe({ error: e => this.redirectIfDisabled(e) });
    this.loadResumes();
  }
  ngOnDestroy() {
    this.clearTimers();
  }

  loadResumes() {
    this.api.listResumes().subscribe({
      next: list => this.resumes.set(list),
      error: e => this.redirectIfDisabled(e),
    });
  }

  onConsent(checked: boolean) {
    this.consent.set(checked);
    this.fieldErrors.update(errors => ({ ...errors, consent: undefined }));
  }

  onFile(input: HTMLInputElement) {
    const file = input.files?.[0] ?? null;
    this.file.set(null);
    this.fieldErrors.update(errors => ({ ...errors, file: undefined }));
    if (!file) {
      return;
    }
    if (!/\.(pdf|docx)$/i.test(file.name)) {
      this.fieldErrors.update(errors => ({ ...errors, file: WRONG_TYPE }));
      return;
    }
    if (file.size > MAX_RESUME_BYTES) {
      this.fieldErrors.update(errors => ({ ...errors, file: TOO_LARGE }));
      return;
    }
    this.file.set(file);
  }

  upload() {
    const file = this.file();
    const errors: { consent?: string; file?: string } = { ...this.fieldErrors() };
    if (!this.consent()) {
      errors.consent = 'Confirm that you agree before uploading.';
    }
    if (!file && !errors.file) {
      errors.file = 'Choose a PDF or DOCX file to upload.';
    }
    this.fieldErrors.set(errors);
    if (!this.consent() || !file || errors.file) {
      return this.fail('Please correct the following before uploading.', 'validation');
    }
    this.resetResult();
    this.phase.set('uploading');
    this.startProgress();
    this.api.uploadResume(file, true).subscribe({
      next: doc => {
        this.stopProgress('Done.');
        this.result.set(doc);
        this.loadResumes();
        if (doc.state === 'ready' && doc.proposalId) {
          this.openProposal(doc.proposalId);
        } else {
          this.focusResult();
        }
      },
      error: (e: CareerApiError) => {
        this.stopProgress('');
        this.handleUploadError(e);
      },
    });
  }

  retry() {
    this.upload();
  }

  submitPaste() {
    const text = this.pasteText().trim();
    if (!text) {
      this.pasteError.set('Paste some resume text first.');
      return this.fail('Please correct the following before continuing.', 'validation');
    }
    this.pasteError.set('');
    this.pasting.set(true);
    this.api.createPastedProposal(text).subscribe({
      next: proposal => {
        this.pasting.set(false);
        this.showProposal(proposal);
      },
      error: (e: CareerApiError) => {
        this.pasting.set(false);
        if (e.kind === 'validation') {
          this.pasteError.set(e.fieldErrors?.['text'] ?? 'Paste between 1 and 100,000 characters.');
        }
        this.fail(this.messageFor(e), e.kind);
      },
    });
  }

  openProposal(id: string) {
    this.clearError();
    this.api.getProposal(id).subscribe({
      next: proposal => this.showProposal(proposal),
      error: (e: CareerApiError) => this.fail(this.messageFor(e), e.kind),
    });
  }

  toggle(item: ProposalItem, checked: boolean) {
    const next = new Set(this.selected());
    if (checked) {
      next.add(item.id);
    } else {
      next.delete(item.id);
    }
    this.selected.set(next);
  }

  accept() {
    const proposal = this.proposal();
    if (!proposal || this.selected().size === 0 || this.accepting()) {
      return;
    }
    this.clearError();
    this.accepting.set(true);
    this.api.acceptProposal(proposal.id, [...this.selected()]).subscribe({
      next: () => {
        this.accepting.set(false);
        this.router.navigateByUrl('/app/career/profile', {
          state: { status: 'Profile updated from your resume' },
        });
      },
      error: (e: CareerApiError) => {
        this.accepting.set(false);
        if (e.kind === 'conflict' || e.kind === 'precondition') {
          this.stale.set(true);
          this.fail(STALE, e.kind);
        } else {
          this.fail(this.messageFor(e), e.kind);
        }
      },
    });
  }

  dismiss() {
    const proposal = this.proposal();
    if (!proposal) {
      return;
    }
    this.api.dismissProposal(proposal.id).subscribe({
      next: () => {
        this.resetResult();
        this.loadResumes();
        this.status.set('Suggestions dismissed. Nothing was added to your profile.');
      },
      error: (e: CareerApiError) => this.fail(this.messageFor(e), e.kind),
    });
  }

  /** After a stale error: drop the outdated suggestions so the user can start again. */
  startOver() {
    const proposal = this.proposal();
    if (proposal) {
      this.api.dismissProposal(proposal.id).subscribe({ error: () => undefined });
    }
    this.resetResult();
    this.loadResumes();
    this.api.getProfile().subscribe({ error: () => undefined });
    this.status.set('Upload your resume again or paste its text to get fresh suggestions.');
  }

  askRemove(id: string) {
    this.confirmRemoveId.set(id);
  }
  cancelRemove() {
    this.confirmRemoveId.set(null);
  }
  remove(doc: ResumeDocumentDto) {
    this.api.deleteResume(doc.id).subscribe({
      next: () => {
        this.confirmRemoveId.set(null);
        this.resumes.update(list => list.filter(r => r.id !== doc.id));
        if (this.result()?.id === doc.id) {
          this.resetResult();
        }
        this.status.set(`${doc.fileName} was removed.`);
      },
      error: (e: CareerApiError) => {
        this.confirmRemoveId.set(null);
        this.fail(this.messageFor(e), e.kind);
      },
    });
  }

  download(doc: ResumeDocumentDto) {
    this.api.downloadResume(doc.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = doc.fileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (e: CareerApiError) => this.fail(this.messageFor(e), e.kind),
    });
  }

  fieldLabel(field: ProposalField) {
    return FIELD_LABELS[field];
  }
  flagLabel(flag: string) {
    return FLAG_LABELS[flag] ?? flag;
  }
  source(item: ProposalItem): string {
    const parts: string[] = [];
    if (item.page) {
      parts.push(`Page ${item.page}`);
    }
    if (item.section) {
      parts.push(item.section);
    }
    if (item.excerpt) {
      parts.push(`"${item.excerpt}"`);
    }
    return parts.join(' · ');
  }
  size(bytes: number) {
    return bytes >= 1024 * 1024
      ? `${(bytes / 1024 / 1024).toFixed(1)} MB`
      : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  private showProposal(proposal: CareerProfileProposalDto) {
    this.proposal.set(proposal);
    this.selected.set(new Set());
    this.stale.set(proposal.isStale);
    this.focusResult();
  }

  private handleUploadError(e: CareerApiError) {
    if (e.kind === 'disabled') {
      return this.redirectIfDisabled(e);
    }
    if (e.kind === 'validation') {
      this.fieldErrors.set({
        consent: e.fieldErrors?.['consent'],
        file: e.fieldErrors?.['file'],
      });
    }
    this.fail(this.messageFor(e), e.kind);
  }

  private messageFor(e: CareerApiError): string {
    switch (e.kind) {
      case 'tooLarge':
        return TOO_LARGE;
      case 'unsupported':
        return e.detail ? `${WRONG_TYPE} (${e.detail})` : WRONG_TYPE;
      case 'rejected':
        return 'This file did not pass our safety check and was not kept. Try a different file.';
      case 'scannerUnavailable':
        return e.retryAfterSeconds
          ? `We cannot check files right now, so nothing was uploaded. Try again in about ${e.retryAfterSeconds} seconds.`
          : 'We cannot check files right now, so nothing was uploaded. Try again shortly.';
      case 'validation':
        return 'Please correct the following before uploading.';
      case 'notFound':
        return 'That item no longer exists. It may have been removed or expired.';
      default:
        return e.message;
    }
  }

  private startProgress() {
    this.progress.set('Uploading your file…');
    this.timers = [
      setTimeout(() => this.progress.set('Checking the file…'), 1500),
      setTimeout(() => this.progress.set('Reading it…'), 4000),
    ];
  }
  private stopProgress(message: string) {
    this.clearTimers();
    this.phase.set('idle');
    this.progress.set(message);
  }
  private clearTimers() {
    this.timers.forEach(clearTimeout);
    this.timers = [];
  }

  private clearError() {
    this.error.set('');
    this.errorKind.set('');
  }
  private resetResult() {
    this.clearError();
    this.stale.set(false);
    this.result.set(null);
    this.proposal.set(null);
    this.selected.set(new Set());
    this.pasteText.set('');
    this.pasteError.set('');
    this.status.set('');
    this.progress.set('');
  }
  private fail(message: string, kind: CareerApiError['kind']) {
    this.error.set(message);
    this.errorKind.set(kind);
    this.focus('[data-error-summary]');
  }
  private focusResult() {
    this.focus('[data-result-heading]');
  }
  private focus(selector: string) {
    // Wait for the new heading or summary to be rendered before moving focus.
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), {
      injector: this.injector,
    });
  }
  private redirectIfDisabled(e: CareerApiError) {
    if (e.kind === 'disabled') {
      this.router.navigateByUrl('/app');
    }
  }
}
