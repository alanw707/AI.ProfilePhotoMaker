import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnInit,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService, LoginDto } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { ConfigService } from '../../services/config.service';
import { finalize, filter, take } from 'rxjs';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent implements OnInit {
  loginForm: FormGroup;
  loading = false;
  error = '';
  returnUrl = '';
  readonly ageConfirmationMessage = 'Age confirmation is required to sign in.';
  readonly ageConfirmErrorId = 'age-confirmation-error';
  submitAttempted = false;
  private _redirectInProgress = false;
  private _ensureSessionStarted = false;

  // Use inject function to reduce constructor parameters
  private readonly _formBuilder = inject(FormBuilder);
  private readonly _authService = inject(AuthService);
  private readonly _router = inject(Router);
  private readonly _route = inject(ActivatedRoute);
  private readonly _cdr = inject(ChangeDetectorRef);
  public readonly themeService = inject(ThemeService);
  private readonly _configService = inject(ConfigService);

  constructor() {
    this.loginForm = this._formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      ageConfirmed: [false, [Validators.requiredTrue]],
    });

    this._setReturnUrlFromParams(this._route.snapshot.queryParams);
  }

  ngOnInit(): void {
    this._authService.isAuthenticated$
      .pipe(
        filter(isAuth => isAuth),
        take(1)
      )
      .subscribe(() => this._redirectToReturnUrl());

    // 1) If we already have client state, go straight to returnUrl
    const isAuthenticated = this._authService.isAuthenticated();
    if (isAuthenticated) {
      this._redirectToReturnUrl();
      return;
    }

    // Handle OAuth callbacks
    this._route.queryParams.subscribe(params => {
      this._setReturnUrlFromParams(params);
      if (this._handleDirectTokenParams(params)) {
        return;
      }
      if (this._handleTokenInReturnUrl(params)) {
        return;
      }
      if (this._handleTokenInFragment()) {
        return;
      }

      if (params['error']) {
        const errorCode = params['error'];
        if (errorCode === 'age_confirmation_required') {
          this.error = this.ageConfirmationMessage;
        } else if (errorCode === 'access_denied') {
          this.error = 'OAuth login was denied. Please try again or use email/password.';
        } else {
          this.error = 'OAuth login failed: ' + errorCode;
        }
        this._cdr.markForCheck();
        return;
      }

      if (!this._ensureSessionStarted) {
        const hasCachedUser =
          !!localStorage.getItem('currentUser') || !!localStorage.getItem('current_user');
        if (!hasCachedUser) {
          return;
        }
        this._ensureSessionStarted = true;
        this._authService.ensureSession().pipe(take(1)).subscribe();
      }
    });
  }

  private _setReturnUrlFromParams(params: Record<string, string>): void {
    this.returnUrl = params['returnUrl'] || '/app/enhance';
  }

  private _handleDirectTokenParams(params: Record<string, string>): boolean {
    if (params['token']) {
      try {
        this._authService.handleOAuthCallback(params['token'], params['expiration']);
        this._redirectToReturnUrl();
      } catch (error) {
        console.error('Error handling OAuth callback:', error);
        this.error = 'Failed to process OAuth token';
        this._cdr.markForCheck();
      }
      return true;
    }
    return false;
  }

  private _handleTokenInReturnUrl(params: Record<string, string>): boolean {
    if (params['returnUrl']?.includes('token=')) {
      try {
        const urlObj = new URL('http://dummy.com' + params['returnUrl']);
        const token = urlObj.searchParams.get('token');
        const expiration = urlObj.searchParams.get('expiration');

        if (token) {
          this._authService.handleOAuthCallback(token, expiration || undefined);
          this._redirectToReturnUrl();
        }
      } catch (error) {
        console.error('Error parsing returnUrl:', error);
        this.error = 'Failed to parse OAuth response';
        this._cdr.markForCheck();
      }
      return true;
    }
    return false;
  }

  private _handleTokenInFragment(): boolean {
    const fragment = window.location.hash;
    if (fragment?.includes('token=')) {
      try {
        const urlObj = new URL('http://dummy.com/?' + fragment.substring(1));
        const token = urlObj.searchParams.get('token');
        const expiration = urlObj.searchParams.get('expiration');

        if (token) {
          this._authService.handleOAuthCallback(token, expiration || undefined);
          this._redirectToReturnUrl();
        }
      } catch (error) {
        console.error('Error parsing URL fragment:', error);
        this.error = 'Failed to parse OAuth response';
        this._cdr.markForCheck();
      }
      return true;
    }
    return false;
  }

  private _extractUserFromToken(
    token: string
  ): { email: string; firstName: string; lastName: string } | null {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return {
        email:
          payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
          payload.email,
        firstName: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'] || '',
        lastName: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'] || '',
      };
    } catch {
      return null;
    }
  }

  get f(): Record<string, AbstractControl> {
    return this.loginForm.controls;
  }

  private _extractErrorMessage(error: any): string {
    if (error?.error?.message) {
      return error.error.message;
    }

    // ASP.NET model state validation: { errors: { Field: [msg] } }
    if (error?.error?.errors) {
      const allErrors = Object.values(error.error.errors).flat().filter(Boolean).join(' ');
      if (allErrors) {
        return allErrors;
      }
    }

    if (typeof error?.error === 'string') {
      return error.error;
    }

    if (typeof error?.error?.error === 'string') {
      return error.error.error;
    }

    if (error?.error?.detail) {
      return error.error.detail;
    }

    if (error?.error?.title) {
      return error.error.title;
    }

    if (error?.message) {
      return error.message;
    }

    return 'Login failed. Please check your email and password and try again.';
  }

  onSubmit(): void {
    this.error = '';
    this.submitAttempted = true;

    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      this._cdr.markForCheck();
      return;
    }

    this.loading = true;
    this._cdr.markForCheck();
    const loginData: LoginDto = {
      email: this.f['email'].value,
      password: this.f['password'].value,
      ageConfirmed: this.f['ageConfirmed'].value,
    };

    this._authService
      .login(loginData)
      .pipe(
        finalize(() => {
          this.loading = false;
          this._cdr.markForCheck();
        })
      )
      .subscribe({
        next: _response => {
          this._redirectToReturnUrl();
        },
        error: error => {
          console.error('Login error:', error);
          this.error = this._extractErrorMessage(error);
          this._cdr.markForCheck();
        },
      });
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  navigateToRegister(): void {
    this._router.navigate(['/auth/register']);
  }

  loginWithGoogle(): void {
    if (this.loading) {
      return;
    }

    this.submitAttempted = true;
    if (this.f['ageConfirmed'].value !== true) {
      this.f['ageConfirmed'].markAsTouched();
      this._cdr.markForCheck();
      return;
    }

    this.loading = true;
    this._cdr.markForCheck();

    // Get OAuth base URL from config service via constructor injection
    const oauthBaseUrl = this._configService.getOAuthBaseUrl();
    const query = new URLSearchParams({
      returnUrl: this.returnUrl,
      ageConfirmed: 'true',
    });

    // ngrok free domains can show an interstitial page that drops OAuth nonce cookies.
    // This query parameter bypasses that warning page for local OAuth testing.
    if (oauthBaseUrl.includes('ngrok')) {
      query.set('ngrok-skip-browser-warning', 'true');
    }

    // Use standard OAuth flow - redirect to the external login endpoint
    const oauthUrl = `${oauthBaseUrl}/api/auth/external-login/google?${query.toString()}`;
    window.location.href = oauthUrl;
  }

  private _redirectToReturnUrl(): void {
    if (this._redirectInProgress) {
      return;
    }
    this._redirectInProgress = true;

    this._router
      .navigate([this.returnUrl])
      .then(success => {
        if (success === false) {
          this._redirectInProgress = false;
          this.error = 'Login succeeded, but navigation failed. Please try again.';
          this._cdr.markForCheck();
        }
      })
      .catch(error => {
        console.error('Login navigation error:', error);
        this._redirectInProgress = false;
        this.error = 'Login succeeded, but navigation failed. Please try again.';
        this._cdr.markForCheck();
      });
  }
}
