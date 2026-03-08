import { ChangeDetectorRef, Component, NgZone, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil, timeout } from 'rxjs/operators';
import { AdminService, AdminUserDiagnosticsDto } from '../../services/admin.service';

@Component({
  selector: 'app-admin-user-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-user-detail.component.html',
  styleUrls: ['../admin-shared.sass', './admin-user-detail.component.sass'],
})
export class AdminUserDetailComponent implements OnInit, OnDestroy {
  diagnostics: AdminUserDiagnosticsDto | null = null;
  isLoading = false;
  error: string | null = null;

  private readonly destroy$ = new Subject<void>();
  private readonly cancelLoad$ = new Subject<void>();
  private loadWatchdog: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private _route: ActivatedRoute,
    private _adminService: AdminService,
    private _cdr: ChangeDetectorRef,
    private _ngZone: NgZone
  ) {}

  ngOnInit(): void {
    this._route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(paramMap => {
      const userId = paramMap.get('userId');
      if (!userId) {
        this.resetLoadStream();
        this.diagnostics = null;
        this.error = 'No user was selected. Return to the user list and try again.';
        this._cdr.detectChanges();
        return;
      }

      this.loadUserDiagnostics(userId);
    });
  }

  ngOnDestroy(): void {
    this.clearLoadWatchdog();
    this.cancelLoad$.next();
    this.cancelLoad$.complete();
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadUserDiagnostics(userId: string | null = this._route.snapshot.paramMap.get('userId')): void {
    if (!userId) {
      this.diagnostics = null;
      this.error = 'No user was selected. Return to the user list and try again.';
      this.isLoading = false;
      this._cdr.detectChanges();
      return;
    }

    this.resetLoadStream();
    this.clearLoadWatchdog();
    this.isLoading = true;
    this.error = null;
    this.loadWatchdog = setTimeout(() => {
      this._ngZone.run(() => {
        if (!this.isLoading) {
          return;
        }

        this.cancelLoad$.next();
        this.error = 'User diagnostics request timed out. Please retry.';
        this.isLoading = false;
        this._cdr.detectChanges();
      });
    }, 16000);

    this._adminService
      .getUserDetail(userId)
      .pipe(
        timeout(15000),
        takeUntil(this.cancelLoad$),
        takeUntil(this.destroy$),
        finalize(() => {
          this.clearLoadWatchdog();
          this.isLoading = false;
          this._cdr.detectChanges();
        })
      )
      .subscribe({
        next: diagnostics => {
          this._ngZone.run(() => {
            this.diagnostics = diagnostics;
            this.error = null;
            this._cdr.detectChanges();
          });
        },
        error: err => {
          this._ngZone.run(() => {
            this.diagnostics = null;
            this.error = err?.message || 'Failed to load user diagnostics. Please try again.';
            this._cdr.detectChanges();
          });
        },
      });
  }

  cancelPendingLoad(): void {
    if (!this.isLoading) {
      return;
    }

    this.cancelLoad$.next();
  }

  get hasDiagnostics(): boolean {
    return !!this.diagnostics;
  }

  private clearLoadWatchdog(): void {
    if (!this.loadWatchdog) {
      return;
    }

    clearTimeout(this.loadWatchdog);
    this.loadWatchdog = null;
  }

  private resetLoadStream(): void {
    this.cancelLoad$.next();
  }
}
