import { ChangeDetectorRef, Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil, timeout } from 'rxjs/operators';
import {
  MarketingService,
  CampaignDto,
  CampaignDetailDto,
  CreateCampaignRequest,
} from '../../services/marketing.service';

const FIRST_CAMPAIGN_BODY = `<p>We heard you — getting 10+ selfies together was a real barrier.</p>
<p><strong>We've lowered the minimum to just 5 selfies.</strong></p>
<p>That's enough for our AI to learn your face and generate professional headshots in any style — LinkedIn, executive, casual, you name it.</p>
<p style="margin:16px 0;">Here's what you'll get:</p>
<ul style="margin:0 0 16px; padding-left:20px; line-height:1.8;">
  <li>AI-trained model built from your 5+ selfies</li>
  <li>Unlimited styled generations (credits permitting)</li>
  <li>LinkedIn, executive, casual, and 20+ styles</li>
  <li>Ready in minutes, not hours</li>
</ul>
<p>
  <a href="https://aiprofilephotomaker.com/dashboard"
     style="display:inline-block; background:#0ea5e9; color:#ffffff; text-decoration:none;
            padding:12px 24px; border-radius:8px; font-weight:600; font-size:16px;">
    Get my headshots now
  </a>
</p>
<p style="margin-top:16px; font-size:14px; color:#64748b;">
  Already uploaded photos? You may already be eligible — just head to your dashboard to check.
</p>`;

type View = 'list' | 'create' | 'detail' | 'logs';

@Component({
  selector: 'app-admin-campaigns',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-campaigns.component.html',
  styleUrls: ['../admin-shared.sass', './admin-campaigns.component.sass'],
})
export class AdminCampaignsComponent implements OnInit, OnDestroy {
  view: View = 'list';
  campaigns: CampaignDto[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;

  selectedCampaign: CampaignDetailDto | null = null;
  logs: any[] = [];
  logTotal = 0;
  logPage = 1;

  segmentCount: number | null = null;
  segmentCounting = false;

  model: CreateCampaignRequest = {
    name: '',
    subject: 'Good news: you can now create AI headshots with just 5 photos',
    htmlBody: FIRST_CAMPAIGN_BODY,
    segmentFilter: 'no-uploads',
    scheduledAt: null,
  };

  scheduleDate = '';
  testEmail = '';
  confirmCancelId: string | null = null;

  isLoading = false;
  isSaving = false;
  error: string | null = null;
  success: string | null = null;

  readonly segmentOptions = [
    { value: 'no-uploads', label: 'No Uploads — verified, never uploaded' },
    { value: 'stuck-under-minimum', label: 'Stuck Under Minimum — 1-4 uploads' },
    { value: 'has-uploads-no-model', label: 'Has Uploads, No Model — 5+ uploads, not trained' },
    { value: 'inactive-30d', label: 'Inactive 30d — has uploads, no activity' },
    { value: 'all-verified', label: 'All Verified — everyone opted in' },
  ];

  private destroy$ = new Subject<void>();

  constructor(
    private _marketing: MarketingService,
    private _cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadCampaigns();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── LIST ───────────────────────────────────────────────────────────────────

  loadCampaigns(): void {
    this.isLoading = true;
    this.error = null;
    this._marketing
      .getCampaigns(this.page, this.pageSize)
      .pipe(timeout(12000), takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.campaigns = res.campaigns;
          this.totalCount = res.totalCount;
          this.isLoading = false;
          this._cdr.detectChanges();
        },
        error: () => {
          this.error = 'Failed to load campaigns. Please refresh to try again.';
          this.isLoading = false;
          this._cdr.detectChanges();
        },
      });
  }

  // ── CREATE ─────────────────────────────────────────────────────────────────

  showCreate(): void {
    this.view = 'create';
    this.model = {
      name: '',
      subject: 'Good news: you can now create AI headshots with just 5 photos',
      htmlBody: FIRST_CAMPAIGN_BODY,
      segmentFilter: 'no-uploads',
      scheduledAt: null,
    };
    this.segmentCount = null;
    this.error = null;
    this.success = null;
  }

  previewSegmentCount(): void {
    if (!this.model.segmentFilter) {
      return;
    }
    this.segmentCounting = true;
    this._marketing
      .previewSegment(this.model.segmentFilter)
      .pipe(
        timeout(12000),
        takeUntil(this.destroy$),
        finalize(() => {
          this.segmentCounting = false;
          this._cdr.detectChanges();
        })
      )
      .subscribe({
        next: res => {
          this.segmentCount = res.count;
          this._cdr.detectChanges();
        },
        error: () => {
          this.error = 'Segment count timed out or failed. Please try again.';
        },
      });
  }

  createCampaign(): void {
    if (!this.model.name || !this.model.subject || !this.model.htmlBody) {
      this.error = 'Name, subject, and body are required.';
      return;
    }
    this.isSaving = true;
    this.error = null;
    const payload = {
      ...this.model,
      scheduledAt: this.model.scheduledAt || null,
    };
    this._marketing
      .createCampaign(payload)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: campaign => {
          this.isSaving = false;
          this.view = 'list';
          this.loadCampaigns();
          this.success = `Campaign "${campaign.name}" created.`;
          this._cdr.detectChanges();
        },
        error: err => {
          this.error = err?.error?.error?.message || 'Failed to create campaign.';
          this.isSaving = false;
          this._cdr.detectChanges();
        },
      });
  }

  // ── DETAIL ─────────────────────────────────────────────────────────────────

  openDetail(id: string): void {
    this.isLoading = true;
    this.error = null;
    this._marketing
      .getCampaign(id)
      .pipe(timeout(12000), takeUntil(this.destroy$))
      .subscribe({
        next: c => {
          this.selectedCampaign = c;
          this.scheduleDate = c.scheduledAt ? c.scheduledAt.slice(0, 16) : '';
          this.view = 'detail';
          this.isLoading = false;
          this._cdr.detectChanges();
        },
        error: () => {
          this.error = 'Failed to load campaign. Please refresh to try again.';
          this.isLoading = false;
          this._cdr.detectChanges();
        },
      });
  }

  scheduleCampaign(): void {
    if (!this.selectedCampaign || !this.scheduleDate) {
      return;
    }
    this.isSaving = true;
    this._marketing
      .scheduleCampaign(this.selectedCampaign.id, new Date(this.scheduleDate).toISOString())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.success = 'Campaign scheduled.';
          this.isSaving = false;
          this.openDetail(this.selectedCampaign!.id);
          this._cdr.detectChanges();
        },
        error: err => {
          this.error = err?.error?.error?.message || 'Failed to schedule.';
          this.isSaving = false;
          this._cdr.detectChanges();
        },
      });
  }

  cancelCampaign(id: string): void {
    this._marketing
      .cancelCampaign(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.confirmCancelId = null;
          this.success = 'Campaign cancelled.';
          if (this.view === 'detail') {
            this.openDetail(id);
          } else {
            this.loadCampaigns();
          }
          this._cdr.detectChanges();
        },
        error: err => {
          this.error = err?.error?.error?.message || 'Failed to cancel.';
          this.confirmCancelId = null;
          this._cdr.detectChanges();
        },
      });
  }

  duplicateCampaign(id: string): void {
    this._marketing
      .duplicateCampaign(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: copy => {
          this.success = `Duplicated as "${copy.name}".`;
          this.loadCampaigns();
          this.view = 'list';
          this._cdr.detectChanges();
        },
        error: () => {
          this.error = 'Failed to duplicate campaign.';
          this._cdr.detectChanges();
        },
      });
  }

  sendTest(): void {
    if (!this.selectedCampaign || !this.testEmail) {
      return;
    }
    this.isSaving = true;
    this._marketing
      .sendTest(this.selectedCampaign.id, this.testEmail)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.success = `Test email sent to ${this.testEmail}.`;
          this.isSaving = false;
          this._cdr.detectChanges();
        },
        error: err => {
          this.error = err?.error?.error?.message || 'Failed to send test.';
          this.isSaving = false;
          this._cdr.detectChanges();
        },
      });
  }

  openPreview(id: string): void {
    window.open(this._marketing.getPreviewUrl(id), '_blank');
  }

  // ── LOGS ───────────────────────────────────────────────────────────────────

  openLogs(id: string): void {
    this.isLoading = true;
    this.logPage = 1;
    this._marketing
      .getLogs(id, this.logPage)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.logs = res.logs;
          this.logTotal = res.totalCount;
          this.view = 'logs';
          this.isLoading = false;
          this._cdr.detectChanges();
        },
        error: () => {
          this.error = 'Failed to load logs.';
          this.isLoading = false;
          this._cdr.detectChanges();
        },
      });
  }

  // ── HELPERS ────────────────────────────────────────────────────────────────

  backToList(): void {
    this.view = 'list';
    this.selectedCampaign = null;
    this.error = null;
    this.success = null;
    this.loadCampaigns();
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  prevPage(): void {
    if (this.page > 1) {
      this.page--;
      this.loadCampaigns();
    }
  }

  nextPage(): void {
    if (this.page < this.totalPages) {
      this.page++;
      this.loadCampaigns();
    }
  }

  statusPillClass(status: string): string {
    return (
      {
        Draft: '',
        Scheduled: 'pill-credit',
        Sending: 'pill-warning',
        Sent: 'pill-success',
        Cancelled: 'pill-danger',
      }[status] ?? ''
    );
  }

  logStatusPillClass(status: string): string {
    return (
      {
        Sent: 'pill-success',
        Failed: 'pill-danger',
        Bounced: 'pill-danger',
        Unsubscribed: 'pill-warning',
        Pending: '',
      }[status] ?? ''
    );
  }

  canSchedule(campaign: CampaignDetailDto | null): boolean {
    return campaign?.status === 'Draft' || campaign?.status === 'Cancelled';
  }

  canCancel(status: string): boolean {
    return status === 'Draft' || status === 'Scheduled';
  }
}
