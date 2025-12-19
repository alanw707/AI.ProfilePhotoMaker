import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './confirm-email.component.html',
  styleUrls: ['./confirm-email.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmEmailComponent implements OnInit {
  isLoading = true;
  isSuccess = false;
  error = '';
  isAuthenticated = false;

  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _authService = inject(AuthService);
  private readonly _cdr = inject(ChangeDetectorRef);

  ngOnInit(): void {
    this.isAuthenticated = this._authService.isAuthenticated();

    const result = this._route.snapshot.queryParamMap.get('result');
    if (result) {
      this.isLoading = false;
      this.isSuccess = result === 'success';
      this.error = this.isSuccess ? '' : 'Email verification failed. Please request a new verification email.';
      this._cdr.markForCheck();
      return;
    }

    const userId = this._route.snapshot.queryParamMap.get('userId') || '';
    const token = this._route.snapshot.queryParamMap.get('token') || '';

    if (!userId || !token) {
      this.isLoading = false;
      this.error = 'Invalid verification link. Please request a new email verification.';
      this._cdr.markForCheck();
      return;
    }

    this._authService
      .confirmEmail(userId, token)
      .pipe(
        finalize(() => {
          this.isLoading = false;
          this._cdr.markForCheck();
        })
      )
      .subscribe({
        next: response => {
          if (response?.success) {
            this.isSuccess = true;
            this.error = '';
          } else {
            this.isSuccess = false;
            this.error = response?.error?.message || 'Email verification failed. Please try again.';
          }
          this._cdr.markForCheck();
        },
        error: err => {
          this.isSuccess = false;
          this.error =
            err?.error?.error?.message ||
            err?.error?.message ||
            err?.message ||
            'Email verification failed. Please try again.';
          this._cdr.markForCheck();
        },
      });
  }

  goToDashboard(): void {
    const redirectUrl = sessionStorage.getItem('redirectUrl') || '/app/dashboard';
    sessionStorage.removeItem('redirectUrl');
    void this._router.navigateByUrl(redirectUrl);
  }

  goToLogin(): void {
    void this._router.navigate(['/auth/login']);
  }
}
