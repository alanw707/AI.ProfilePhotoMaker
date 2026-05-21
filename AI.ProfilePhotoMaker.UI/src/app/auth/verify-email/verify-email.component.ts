import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';
import { IntentTrackingService, SignupIntent } from '../../services/intent-tracking.service';
import {
  getHeadshotsWelcomeCopy,
  getIntentPreviewImages,
} from '../../services/signup-intent-personalization';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './verify-email.component.html',
  styleUrls: ['./verify-email.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VerifyEmailComponent implements OnInit {
  isLoading = true;
  isEmailConfirmed = false;
  isResending = false;
  isDevConfirming = false;
  message = '';
  error = '';
  signupIntent: SignupIntent | null = null;
  readonly showDevBypass =
    !environment.production &&
    (window.location.hostname.includes('localhost') ||
      window.location.hostname.includes('127.0.0.1'));

  private readonly _authService = inject(AuthService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _cdr = inject(ChangeDetectorRef);
  private readonly _intentTracking = inject(IntentTrackingService);

  ngOnInit(): void {
    this.signupIntent = this._intentTracking.getIntent();
    const message = this._route.snapshot.queryParamMap.get('message');
    if (message) {
      this.message = message;
    }
    this.refreshStatus();
  }

  refreshStatus(): void {
    this.isLoading = true;
    this.error = '';
    this._cdr.markForCheck();

    if (!this._authService.isAuthenticated()) {
      this.isLoading = false;
      if (!this.message) {
        this.message = 'Please sign in to continue.';
      }
      this._cdr.markForCheck();
      return;
    }

    this._authService
      .getAccountStatus()
      .pipe(
        finalize(() => {
          this.isLoading = false;
          this._cdr.markForCheck();
        })
      )
      .subscribe({
        next: response => {
          this.isEmailConfirmed = !!response?.data?.emailConfirmed;
          if (this.isEmailConfirmed) {
            this.continue();
          }
        },
        error: err => {
          if (err?.status === 401 || err?.status === 403) {
            this._authService.clearAuthCache();
            void this._router.navigate(['/auth/login'], {
              queryParams: {
                message: 'Please sign in to continue.',
                returnUrl: '/app/enhance',
              },
            });
            return;
          }
          this.error =
            err?.error?.error?.message ||
            err?.error?.message ||
            err?.message ||
            'Unable to check verification status. Please refresh and try again.';
        },
      });
  }

  resend(): void {
    if (this.isResending) {
      return;
    }

    this.isResending = true;
    this.message = '';
    this.error = '';
    this._cdr.markForCheck();

    this._authService
      .resendConfirmationEmail()
      .pipe(
        finalize(() => {
          this.isResending = false;
          this._cdr.markForCheck();
        })
      )
      .subscribe({
        next: response => {
          if (response?.success) {
            this.message = 'Verification email sent. Please check your inbox (and spam).';
          } else {
            this.error =
              response?.error?.message || 'Failed to send verification email. Please try again.';
          }
        },
        error: err => {
          if (err?.status === 401 || err?.status === 403) {
            void this._router.navigate(['/auth/login'], {
              queryParams: {
                message: 'Please sign in to continue.',
                returnUrl: '/app/enhance',
              },
            });
            return;
          }
          this.error =
            err?.error?.error?.message ||
            err?.error?.message ||
            err?.message ||
            'Failed to send verification email. Please try again.';
        },
      });
  }

  devConfirm(): void {
    if (!this.showDevBypass || this.isDevConfirming) {
      return;
    }

    this.isDevConfirming = true;
    this.message = '';
    this.error = '';
    this._cdr.markForCheck();

    this._authService
      .devConfirmEmail()
      .pipe(
        finalize(() => {
          this.isDevConfirming = false;
          this._cdr.markForCheck();
        })
      )
      .subscribe({
        next: response => {
          if (response?.success) {
            this.message = 'Email marked as verified (development only).';
            this.refreshStatus();
          } else {
            this.error = response?.error?.message || 'Failed to confirm email (development only).';
          }
        },
        error: err => {
          this.error =
            err?.error?.error?.message ||
            err?.error?.message ||
            err?.message ||
            'Failed to confirm email (development only).';
        },
      });
  }

  continue(): void {
    const redirectUrl = sessionStorage.getItem('redirectUrl') || '/app/enhance';
    sessionStorage.removeItem('redirectUrl');
    void this._router.navigateByUrl(redirectUrl);
  }

  getProgressStep(): string {
    return 'Step 2 of 3: Verify Email';
  }

  getUrgencyMessage(): string {
    if (!this.signupIntent) {
      return 'Your 25 free credits are waiting! Complete verification to get started.';
    }

    if (this.signupIntent.ctaType === 'headshots') {
      return getHeadshotsWelcomeCopy(this.signupIntent).urgency;
    }

    if (this.signupIntent.ctaType === 'enhance') {
      return 'Your 25 free credits are waiting! Complete verification to enhance your photos.';
    }

    return 'Your 25 free credits are waiting! Complete verification to start creating your profile photos.';
  }

  getPreviewImages(): { src: string; alt: string }[] {
    return getIntentPreviewImages(this.signupIntent);
  }

  logout(): void {
    this._authService.logout('home');
  }
}
