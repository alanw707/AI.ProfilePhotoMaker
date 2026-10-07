import {
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  CareerApiError,
  CareerDeletionDto,
  CareerProfileService,
  CareerRetentionDto,
  DeletionScope,
} from '../../services/career-profile.service';

const PRIVACY_PATH = '/app/career/privacy';
export const DELETION_KEY = 'career-privacy-deletion-id';

@Component({
  standalone: true,
  selector: 'app-career-privacy',
  imports: [RouterLink],
  templateUrl: './career-privacy.component.html',
  styleUrl: './career.scss',
})
export class CareerPrivacyComponent implements OnInit, OnDestroy {
  static readonly POLL_MS = 1500;
  static readonly REVOKE_DELAY_MS = 60_000;
  readonly path = PRIVACY_PATH;
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private objectUrls = new Set<string>();
  private timer: ReturnType<typeof setTimeout> | null = null;
  private confirmDialog = viewChild<ElementRef<HTMLDialogElement>>('confirm');

  retention = signal<CareerRetentionDto | null>(null);
  retentionError = signal('');
  downloading = signal(false);
  exportStatus = signal('');
  exportError = signal('');
  scope = signal<DeletionScope | null>(null);
  typed = signal('');
  busy = signal(false);
  reauth = signal(false);
  deleteError = signal('');
  deletion = signal<CareerDeletionDto | null>(null);
  /** An id we are tracking whose first status has not arrived yet. */
  checking = signal(false);

  state = computed(() => {
    const d = this.deletion();
    if (d) {
      return d.status === 'completed' ? 'completed' : d.status === 'failed' ? 'failed' : 'pending';
    }
    return this.checking() ? 'pending' : 'none';
  });
  statusText = computed(() => {
    switch (this.state()) {
      case 'pending':
        return 'Deleting…';
      case 'completed':
        return 'Deleted';
      case 'failed':
        return 'Not finished — some files could not be removed yet';
      default:
        return '';
    }
  });
  canConfirm = computed(() => {
    if (this.scope() === 'career_profile') {
      return this.typed().trim() === 'DELETE';
    }
    return this.scope() === 'raw_documents';
  });

  ngOnInit() {
    this.api.getRetention().subscribe({
      next: r => this.retention.set(r),
      error: (e: CareerApiError) => this.handle(e, msg => this.retentionError.set(msg)),
    });
    const id = this.stored();
    if (id) {
      this.checking.set(true);
      this.poll(id);
    }
  }

  ngOnDestroy() {
    this.stopPolling();
    this.objectUrls.forEach(url => URL.revokeObjectURL(url));
    this.objectUrls.clear();
  }

  download() {
    if (this.downloading()) {
      return;
    }
    this.downloading.set(true);
    this.exportStatus.set('Getting your file…');
    this.exportError.set('');
    this.api.downloadPrivacyExport().subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'career-data.json';
        document.body.appendChild(link);
        link.click();
        link.remove();
        this.objectUrls.add(url);
        setTimeout(() => {
          if (this.objectUrls.delete(url)) {
            URL.revokeObjectURL(url);
          }
        }, CareerPrivacyComponent.REVOKE_DELAY_MS);
        this.downloading.set(false);
        this.exportStatus.set('Your career data was downloaded.');
      },
      error: (e: CareerApiError) => {
        this.downloading.set(false);
        this.exportStatus.set('');
        this.handle(e, msg => this.exportError.set(msg));
      },
    });
  }

  ask() {
    if (!this.scope()) {
      return;
    }
    this.reauth.set(false);
    this.deleteError.set('');
    this.typed.set('');
    this.confirmDialog()?.nativeElement.showModal();
  }
  cancel() {
    this.confirmDialog()?.nativeElement.close();
  }

  confirmDelete() {
    const scope = this.scope();
    if (!scope || !this.canConfirm() || this.busy()) {
      return;
    }
    this.confirmDialog()?.nativeElement.close();
    this.busy.set(true);
    this.api.requestDeletion(scope).subscribe({
      next: d => this.started(d),
      error: (e: CareerApiError) => this.failed(e),
    });
  }

  retry() {
    const d = this.deletion();
    if (!d || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.reauth.set(false);
    this.deleteError.set('');
    this.api.retryDeletion(d.id).subscribe({
      next: next => this.started(next),
      error: (e: CareerApiError) => this.failed(e),
    });
  }

  private started(d: CareerDeletionDto) {
    this.busy.set(false);
    this.remember(d.id);
    this.track(d);
  }

  private failed(e: CareerApiError) {
    this.busy.set(false);
    if (e.kind === 'reauth') {
      this.reauth.set(true);
    } else {
      this.handle(e, msg => this.deleteError.set(msg));
    }
  }

  private track(d: CareerDeletionDto) {
    this.checking.set(false);
    this.deletion.set(d);
    if (d.status === 'completed') {
      this.forget();
      this.stopPolling();
    } else if (d.status === 'failed') {
      this.stopPolling();
    } else {
      this.schedule(d.id);
    }
  }

  private poll(id: string) {
    this.api.getDeletion(id).subscribe({
      next: d => this.track(d),
      error: (e: CareerApiError) => {
        if (e.kind === 'notFound') {
          this.forget();
          this.checking.set(false);
        } else if (e.kind === 'reauth' || e.kind === 'unauthorized') {
          this.handle(e, msg => this.deleteError.set(msg));
        } else {
          // Transient failure: keep saying "Deleting…" and try again.
          this.schedule(id);
        }
      },
    });
  }

  private schedule(id: string) {
    this.stopPolling();
    this.timer = setTimeout(() => this.poll(id), CareerPrivacyComponent.POLL_MS);
  }
  private stopPolling() {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private stored(): string | null {
    try {
      return sessionStorage.getItem(DELETION_KEY);
    } catch {
      return null;
    }
  }
  private remember(id: string) {
    try {
      sessionStorage.setItem(DELETION_KEY, id);
    } catch {
      /* storage unavailable: status still shows until reload */
    }
  }
  private forget() {
    try {
      sessionStorage.removeItem(DELETION_KEY);
    } catch {
      /* ignore */
    }
  }

  private handle(e: CareerApiError, show: (message: string) => void) {
    if (e.kind === 'unauthorized') {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: PRIVACY_PATH } });
    } else if (e.kind === 'disabled') {
      this.router.navigateByUrl('/app');
    } else {
      show(e.message);
    }
  }
}
