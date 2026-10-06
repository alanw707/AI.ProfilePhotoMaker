import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  CareerApiError,
  CareerControlsDto,
  CareerProfileService,
  CareerUsageDto,
} from '../../services/career-profile.service';

type Switch = 'generationDisabled' | 'sourcesDisabled';
const ACTION_LABELS: Record<string, string> = {
  profile_summary: 'Profile summary',
  occupation_match: 'Occupation match',
  market_brief: 'Market brief',
  pay_analysis: 'Pay analysis',
  roadmap: 'Roadmap',
  targeted_resume: 'Targeted resume',
  professional_summary: 'Professional summary',
  export: 'Export',
  source_call: 'Job source call',
};
/** Readable name for an action key; unknown keys become sentence case, never raw codes. */
export function actionLabel(key: string): string {
  const known = ACTION_LABELS[key];
  if (known) {
    return known;
  }
  const text = key.replace(/[_-]+/g, ' ').trim();
  return text.charAt(0).toUpperCase() + text.slice(1);
}
const SWITCHES: { key: Switch; label: string; on: string; off: string; confirm: string }[] = [
  {
    key: 'generationDisabled',
    label: 'Pause drafting',
    on: 'New drafts are paused for everyone.',
    off: 'New drafts are running.',
    confirm: 'Pause all new career drafting? Saved work stays available.',
  },
  {
    key: 'sourcesDisabled',
    label: 'Pause job sources',
    on: 'External job sources are off.',
    off: 'External job sources are on.',
    confirm: 'Turn off external job sources?',
  },
];

@Component({
  selector: 'app-admin-career-usage',
  standalone: true,
  imports: [RouterModule, DatePipe, DecimalPipe, PercentPipe],
  templateUrl: './admin-career-usage.component.html',
  styleUrls: ['../admin-shared.sass'],
})
export class AdminCareerUsageComponent implements OnInit {
  private api = inject(CareerProfileService);
  usage = signal<CareerUsageDto | null>(null);
  controls = signal<CareerControlsDto | null>(null);
  error = signal('');
  notice = signal('');
  saving = signal(false);
  pending = signal<Switch | null>(null);
  switches = SWITCHES;
  label = actionLabel;
  /** The API reports failures and a count; the rate is derived here. */
  failureRate = (a: { count: number; failures: number }) =>
    a.count > 0 ? a.failures / a.count : 0;

  ngOnInit() {
    this.api.getAdminUsage().subscribe({
      next: u => this.usage.set(u),
      error: (e: CareerApiError) => this.error.set(e.message),
    });
    this.api.getAdminControls().subscribe({
      next: c => this.controls.set(c),
      error: (e: CareerApiError) => this.error.set(e.message),
    });
  }
  /** Asks first; nothing is sent until confirm(). */
  request(key: Switch) {
    this.notice.set('');
    this.pending.set(key);
  }
  cancel() {
    this.pending.set(null);
  }
  confirm() {
    const key = this.pending();
    const c = this.controls();
    if (!key || !c || this.saving()) {
      return;
    }
    this.saving.set(true);
    const next = {
      generationDisabled: c.generationDisabled,
      sourcesDisabled: c.sourcesDisabled,
      [key]: !c[key],
    };
    this.api.putAdminControls(next).subscribe({
      next: saved => {
        this.controls.set(saved);
        this.saving.set(false);
        this.pending.set(null);
        this.notice.set('Saved.');
      },
      error: (e: CareerApiError) => {
        this.saving.set(false);
        this.pending.set(null);
        this.error.set(e.message);
      },
    });
  }
  pendingText() {
    return SWITCHES.find(s => s.key === this.pending())?.confirm ?? '';
  }
}
