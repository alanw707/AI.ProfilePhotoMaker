import { ChangeDetectorRef, Component, NgZone, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationStart, Router, RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { filter, finalize, takeUntil, timeout } from 'rxjs/operators';
import { AdminService } from '../../services/admin.service';
import { CookieConsentService } from '../../services/cookie-consent.service';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['../admin-shared.sass', './admin-dashboard.component.sass'],
})
export class AdminDashboardComponent implements OnInit, OnDestroy {
  dashboard = {
    totalUsers: 0,
    activeUsers: 0,
    totalCreditsOutstanding: 0,
    totalCreditsPurchased: 0,
    activeCoupons: 0,
  };

  isLoading = false;
  error: string | null = null;
  private destroy$ = new Subject<void>();
  private cancelLoad$ = new Subject<void>();
  private loadWatchdog: ReturnType<typeof setTimeout> | null = null;
  private loadingHeartbeat: ReturnType<typeof setInterval> | null = null;

  constructor(
    private _adminService: AdminService,
    private _router: Router,
    private _cdr: ChangeDetectorRef,
    private _ngZone: NgZone,
    private _cookieConsentService: CookieConsentService
  ) {}

  ngOnInit(): void {
    this._router.events
      .pipe(
        filter((event): event is NavigationStart => event instanceof NavigationStart),
        takeUntil(this.destroy$)
      )
      .subscribe(event => {
        if (!event.url.startsWith('/admin/dashboard')) {
          this.cancelPendingDashboardLoad();
        }
      });

    this._cookieConsentService.consent$.pipe(takeUntil(this.destroy$)).subscribe(consent => {
      if (!consent) {
        return;
      }

      if (this.isLoading || this.error) {
        this.loadDashboard();
      }
    });

    this.loadDashboard();
  }

  ngOnDestroy(): void {
    this.clearLoadingHeartbeat();
    this.clearLoadWatchdog();
    this.cancelLoad$.next();
    this.cancelLoad$.complete();
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDashboard(): void {
    this.startLoadingHeartbeat();
    this.clearLoadWatchdog();
    this.cancelLoad$.next();
    this.isLoading = true;
    this.error = null;
    this.loadWatchdog = setTimeout(() => {
      this._ngZone.run(() => {
        if (!this.isLoading) {
          return;
        }

        this.cancelLoad$.next();
        this.error = 'Dashboard request timed out. Please retry.';
        this.isLoading = false;
        this._cdr.detectChanges();
      });
    }, 16000);

    this._adminService
      .getDashboard()
      .pipe(
        timeout(15000),
        takeUntil(this.cancelLoad$),
        takeUntil(this.destroy$),
        finalize(() => {
          this.clearLoadingHeartbeat();
          this.clearLoadWatchdog();
          this.isLoading = false;
          this._cdr.detectChanges();
        })
      )
      .subscribe({
        next: data => {
          this._ngZone.run(() => {
            this.dashboard = data;
            this._cdr.detectChanges();
          });
        },
        error: err => {
          this._ngZone.run(() => {
            console.error('Failed to load dashboard:', err);
            this.error = err?.message || 'Failed to load dashboard statistics. Please try again.';
            this._cdr.detectChanges();
          });
        },
      });
  }

  cancelPendingDashboardLoad(): void {
    if (!this.isLoading) {
      return;
    }

    this.cancelLoad$.next();
  }

  private clearLoadWatchdog(): void {
    if (!this.loadWatchdog) {
      return;
    }

    clearTimeout(this.loadWatchdog);
    this.loadWatchdog = null;
  }

  private startLoadingHeartbeat(): void {
    this.clearLoadingHeartbeat();
    this.loadingHeartbeat = setInterval(() => {
      this._ngZone.run(() => {
        this._cdr.detectChanges();
      });
    }, 1000);
  }

  private clearLoadingHeartbeat(): void {
    if (!this.loadingHeartbeat) {
      return;
    }

    clearInterval(this.loadingHeartbeat);
    this.loadingHeartbeat = null;
  }
}
