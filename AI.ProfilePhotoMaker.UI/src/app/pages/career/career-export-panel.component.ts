import { Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import {
  CareerApiError,
  CareerExportDto,
  CareerProfileService,
  ExportFormat,
  ResumeVersionInfo,
} from '../../services/career-profile.service';

/** "in about 3 hours" style wording; never shows the raw timestamp. */
export function expiryText(expiresAt: string, now = Date.now()): string {
  const minutes = Math.round((Date.parse(expiresAt) - now) / 60000);
  if (!Number.isFinite(minutes) || minutes <= 0) {
    return 'Link expired';
  }
  if (minutes < 60) {
    return 'Link works for less than an hour';
  }
  const hours = Math.round(minutes / 60);
  return `Link works for about ${hours} ${hours === 1 ? 'hour' : 'hours'}`;
}

@Component({
  standalone: true,
  selector: 'app-career-export-panel',
  templateUrl: './career-export-panel.component.html',
  styleUrl: './career.scss',
})
export class CareerExportPanelComponent implements OnInit {
  private api = inject(CareerProfileService);
  materialId = input.required<string>();
  currentVersion = input.required<number>();
  versions = input<ResumeVersionInfo[]>([]);

  format = signal<ExportFormat>('pdf');
  /** null means "the current version". */
  version = signal<number | null>(null);
  withPhoto = signal(false);
  hasPhoto = signal(false);
  busy = signal(false);
  status = signal('');
  error = signal('');
  recent = signal<CareerExportDto[]>([]);

  showPhotoOption = computed(() => this.hasPhoto() && this.format() === 'pdf');

  constructor() {
    effect(() => {
      if (this.materialId()) {
        this.loadRecent();
      }
    });
  }

  ngOnInit() {
    this.api.listPhotos().subscribe({
      next: list => this.hasPhoto.set(list.selectedPhotoId !== null && list.selectedPhotoAvailable),
      error: () => undefined,
    });
  }

  setFormat(format: ExportFormat) {
    this.format.set(format);
    if (format !== 'pdf') {
      this.withPhoto.set(false);
    }
  }
  setVersion(value: string) {
    this.version.set(value === 'current' ? null : Number(value));
  }
  formatText(e: CareerExportDto) {
    const kind = e.format === 'pdf' ? 'PDF' : 'Word';
    return e.includesPhoto ? `${kind} with my photo` : kind;
  }
  expiry(e: CareerExportDto) {
    return expiryText(e.expiresAt);
  }

  download() {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.status.set('Making your file…');
    this.error.set('');
    const body: { format: ExportFormat; version?: number; includePhoto: boolean } = {
      format: this.format(),
      includePhoto: this.showPhotoOption() && this.withPhoto(),
    };
    const version = this.version();
    if (version !== null && version !== this.currentVersion()) {
      body.version = version;
    }
    this.api.createExport(this.materialId(), body).subscribe({
      next: made => {
        this.fetch(made);
        this.loadRecent();
      },
      error: (e: CareerApiError) => this.fail(e),
    });
  }

  again(e: CareerExportDto) {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.status.set('Getting your file…');
    this.error.set('');
    this.fetch(e);
  }

  private fetch(e: CareerExportDto) {
    this.api.downloadExport(e.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = e.fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        this.busy.set(false);
        this.status.set('Your file was downloaded. Check it before you send it.');
      },
      error: (err: CareerApiError) => {
        this.fail(err);
        this.loadRecent();
      },
    });
  }

  private fail(e: CareerApiError) {
    this.busy.set(false);
    this.status.set('');
    if (e.kind === 'exportExpired') {
      this.error.set('That download link expired. Make a new file to get a fresh link.');
    } else if (e.kind === 'exportPhotoUnavailable') {
      this.error.set('Choose a photo first, or leave the photo option off.');
      this.hasPhoto.set(false);
      this.withPhoto.set(false);
    } else if (e.kind === 'notFound') {
      this.error.set('We could not find that file. Make a new one.');
    } else {
      this.error.set('We could not make your file. Try again.');
    }
  }

  private loadRecent() {
    const id = this.materialId();
    if (!id) {
      return;
    }
    this.api.listExports(id).subscribe({
      next: r => this.recent.set(r.exports ?? []),
      error: () => undefined,
    });
  }
}
