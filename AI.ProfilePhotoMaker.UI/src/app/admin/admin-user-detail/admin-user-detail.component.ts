import { ChangeDetectorRef, Component, NgZone, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil, timeout } from 'rxjs/operators';
import {
  AdminGrantPackageEntitlementDto,
  AdminOutcomePackageDefinitionDto,
  AdminPackageEntitlementDto,
  AdminService,
  AdminUserDiagnosticsDto,
} from '../../services/admin.service';

@Component({
  selector: 'app-admin-user-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-user-detail.component.html',
  styleUrls: ['../admin-shared.sass', './admin-user-detail.component.sass'],
})
export class AdminUserDetailComponent implements OnInit, OnDestroy {
  diagnostics: AdminUserDiagnosticsDto | null = null;
  packageDefinitions: AdminOutcomePackageDefinitionDto[] = [];
  isLoading = false;
  isLoadingPackages = false;
  isMutating = false;
  showGrantForm = false;
  selectedPackageDefinitionId: number | null = null;
  grantExpiresAt = '';
  grantReason = '';
  confirmInactiveGrant = false;
  mutationError: string | null = null;
  mutationSuccess: string | null = null;
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

  beginGrant(): void {
    this.showGrantForm = true;
    this.mutationError = null;
    if (this.packageDefinitions.length || this.isLoadingPackages) {
      return;
    }

    this.isLoadingPackages = true;
    this._adminService
      .getPackageDefinitions()
      .pipe(
        finalize(() => (this.isLoadingPackages = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: definitions => (this.packageDefinitions = definitions),
        error: err => (this.mutationError = err?.message || 'Failed to load package definitions.'),
      });
  }

  cancelGrant(): void {
    this.showGrantForm = false;
    this.mutationError = null;
  }

  grantPackage(): void {
    const userId = this._route.snapshot.paramMap.get('userId');
    if (
      !userId ||
      !this.selectedPackageDefinitionId ||
      !this.grantReason.trim() ||
      this.isMutating
    ) {
      this.mutationError = 'Select a package and provide a reason before granting access.';
      return;
    }

    const selected = this.packageDefinitions.find(
      item => item.id === this.selectedPackageDefinitionId
    );
    if (selected && !selected.isActive && !this.confirmInactiveGrant) {
      this.mutationError = 'Confirm the inactive package before granting it.';
      return;
    }

    const dto: AdminGrantPackageEntitlementDto = {
      packageDefinitionId: this.selectedPackageDefinitionId,
      expiresAt: this.grantExpiresAt ? new Date(this.grantExpiresAt).toISOString() : null,
      reason: this.grantReason.trim(),
      confirmInactive: this.confirmInactiveGrant,
    };

    this.isMutating = true;
    this.mutationError = null;
    this.mutationSuccess = null;
    this._adminService
      .grantPackageEntitlement(userId, dto)
      .pipe(
        finalize(() => (this.isMutating = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.showGrantForm = false;
          this.grantReason = '';
          this.grantExpiresAt = '';
          this.confirmInactiveGrant = false;
          this.mutationSuccess = 'Package entitlement granted.';
          this.loadUserDiagnostics(userId);
        },
        error: err => (this.mutationError = err?.message || 'Failed to grant package entitlement.'),
      });
  }

  revokePackage(entitlement: AdminPackageEntitlementDto): void {
    const userId = this._route.snapshot.paramMap.get('userId');
    if (
      !window.confirm(
        `Revoke ${entitlement.packageName}? This preserves history but stops future package use.`
      )
    ) {
      return;
    }

    const reason = window.prompt('Reason for revoking this package entitlement:')?.trim();
    if (!userId || !reason || this.isMutating) {
      return;
    }

    this.isMutating = true;
    this.mutationError = null;
    this.mutationSuccess = null;
    this._adminService
      .revokePackageEntitlement(userId, entitlement.id, reason)
      .pipe(
        finalize(() => (this.isMutating = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.mutationSuccess = 'Package entitlement revoked.';
          this.loadUserDiagnostics(userId);
        },
        error: err =>
          (this.mutationError = err?.message || 'Failed to revoke package entitlement.'),
      });
  }

  get hasDiagnostics(): boolean {
    return !!this.diagnostics;
  }

  get selectedPackageDefinition(): AdminOutcomePackageDefinitionDto | undefined {
    return this.packageDefinitions.find(item => item.id === this.selectedPackageDefinitionId);
  }

  get selectedPackageIsInactive(): boolean {
    return this.selectedPackageDefinition?.isActive === false;
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
