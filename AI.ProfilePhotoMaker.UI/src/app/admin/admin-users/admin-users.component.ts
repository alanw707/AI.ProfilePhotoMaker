import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Observable } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { AdminService } from '../../services/admin.service';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-users.component.html',
  styleUrls: ['../admin-shared.sass', './admin-users.component.sass'],
})
export class AdminUsersComponent implements OnInit {
  constructor(
    private _adminService: AdminService,
    private _cdr: ChangeDetectorRef
  ) {}
  users: any[] = [];
  isLoading = false;
  error: string | null = null;
  search = '';
  page = 1;
  pageSize = 20;
  totalCount = 0;
  creditAmount = 0;
  reason = '';
  selectedUserId: string | null = null;
  selectedUserLabel = '';
  pendingAction: 'deactivate' | 'reactivate' | 'delete' | 'credits' | null = null;
  isSubmitting = false;
  actionMessage = '';
  actionMessageType: 'success' | 'error' | '' = '';

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.isLoading = true;
    this.error = null;
    this._adminService
      .getUsers(this.page, this.pageSize, this.search)
      .pipe(finalize(() => (this.isLoading = false)))
      .subscribe({
        next: data => {
          this.users = data.users || [];
          this.totalCount = data.totalCount || 0;
        },
        error: err => (this.error = err?.message || 'Failed to load users. Please retry.'),
      });
  }

  onSearch(): void {
    this.page = 1;
    this.loadUsers();
  }

  startAction(user: any, action: 'deactivate' | 'reactivate' | 'delete' | 'credits'): void {
    this.selectedUserId = user.id;
    const fullName = `${user.firstName || ''} ${user.lastName || ''}`.trim();
    this.selectedUserLabel = fullName ? `${fullName} (${user.email})` : user.email;
    this.pendingAction = action;
    this.reason = '';
    this.creditAmount = 0;
    this.isSubmitting = false;
    this.actionMessage = '';
    this.actionMessageType = '';
  }

  cancelAction(): void {
    this.selectedUserId = null;
    this.selectedUserLabel = '';
    this.pendingAction = null;
    this.reason = '';
    this.creditAmount = 0;
    this.isSubmitting = false;
    this._cdr.detectChanges();
  }

  submitAction(): void {
    if (this.isSubmitting) {
      return;
    }

    if (!this.selectedUserId || !this.pendingAction || !this.reason.trim()) {
      this.actionMessage = 'Reason is required.';
      this.actionMessageType = 'error';
      return;
    }

    const userId = this.selectedUserId;
    const reason = this.reason.trim();
    const amount = Number(this.creditAmount);

    if (this.pendingAction === 'credits' && (!Number.isFinite(amount) || amount === 0)) {
      this.actionMessage = 'Enter a non-zero credit amount.';
      this.actionMessageType = 'error';
      return;
    }

    let request$: Observable<any>;

    if (this.pendingAction === 'deactivate') {
      request$ = this._adminService.deactivateUser(userId, reason);
    } else if (this.pendingAction === 'reactivate') {
      request$ = this._adminService.reactivateUser(userId, reason);
    } else if (this.pendingAction === 'delete') {
      request$ = this._adminService.deleteUser(userId, reason);
    } else {
      request$ = this._adminService.adjustCredits({ userId, amount, reason });
    }

    this.isSubmitting = true;
    request$
      .pipe(
        finalize(() => {
          this.isSubmitting = false;
          this._cdr.detectChanges();
        })
      )
      .subscribe({
        next: () => {
          this.isSubmitting = false;
          this.actionMessage = 'Action completed successfully.';
          this.actionMessageType = 'success';
          this.cancelAction();
          this._cdr.detectChanges();
          this.loadUsers();
        },
        error: error => {
          this.isSubmitting = false;
          this.actionMessage = error?.message || 'Action failed. Please try again.';
          this.actionMessageType = 'error';
          this._cdr.detectChanges();
        },
      });
  }

  nextPage(): void {
    if (this.page * this.pageSize >= this.totalCount) {
      return;
    }

    this.page += 1;
    this.loadUsers();
  }

  prevPage(): void {
    if (this.page <= 1) {
      return;
    }

    this.page -= 1;
    this.loadUsers();
  }
}
