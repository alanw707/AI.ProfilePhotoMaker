import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService, LoginDto } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { ConfigService } from '../../services/config.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent implements OnInit, OnDestroy {
  loginForm: FormGroup;
  loading = false;
  error = '';
  returnUrl = '';
  private _authSub?: any;

  // Use inject function to reduce constructor parameters
  private readonly _formBuilder = inject(FormBuilder);
  private readonly _authService = inject(AuthService);
  private readonly _router = inject(Router);
  private readonly _route = inject(ActivatedRoute);
  public readonly themeService = inject(ThemeService);
  private readonly _configService = inject(ConfigService);

  constructor() {
    this.loginForm = this._formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
    });

    // Get return URL from route parameters or default to profile
    this.returnUrl = this._route.snapshot.queryParams['returnUrl'] || '/app/dashboard';
  }

  ngOnInit(): void {
    // 1) If we already have client state, go straight to returnUrl
    const isAuthenticated = this._authService.isAuthenticated();
    if (isAuthenticated) {
      this._router.navigate([this.returnUrl]);
      return;
    }

    // 2) Production cookie-session case: proactively probe the session and
    //    auto-redirect once the guard updates auth state from a valid cookie.
    //    Without this, users with a valid cookie can get stuck on the login page
    //    because the initial auth state comes from localStorage only.
    this._authService.probeSession();
    this._authSub = this._authService.isAuthenticated$.subscribe(flag => {
      if (flag) {
        this._router.navigate([this.returnUrl]);
      }
    });

    // Handle OAuth callbacks
    this._route.queryParams.subscribe(params => {
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
        this.error = 'OAuth login failed: ' + params['error'];
      }
    });
  }

  // Ensure we don't leak the subscription
  ngOnDestroy(): void {
    if (this._authSub) {
      this._authSub.unsubscribe?.();
    }
  }

  private _handleDirectTokenParams(params: Record<string, string>): boolean {
    if (params['token']) {
      try {
        this._authService.handleOAuthCallback(params['token'], params['expiration']);
        this._router.navigate(['/app/dashboard']);
      } catch (error) {
        console.error('Error handling OAuth callback:', error);
        this.error = 'Failed to process OAuth token';
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
          this._router.navigate(['/app/dashboard']);
        }
      } catch (error) {
        console.error('Error parsing returnUrl:', error);
        this.error = 'Failed to parse OAuth response';
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
          this._router.navigate(['/app/dashboard']);
        }
      } catch (error) {
        console.error('Error parsing URL fragment:', error);
        this.error = 'Failed to parse OAuth response';
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

  onSubmit(): void {
    this.error = '';

    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    const loginData: LoginDto = {
      email: this.f['email'].value,
      password: this.f['password'].value,
    };

    this._authService.login(loginData).subscribe({
      next: _response => {
        this.loading = false;
        this._router.navigate([this.returnUrl]);
      },
      error: error => {
        console.error('Login error:', error);
        this.error = error.message || 'Login failed. Please try again.';
        this.loading = false;
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
    // Get OAuth base URL from config service via constructor injection
    const oauthBaseUrl = this._configService.getOAuthBaseUrl();
    // Use standard OAuth flow - redirect to the external login endpoint
    const oauthUrl = `${oauthBaseUrl}/api/auth/external-login/google?returnUrl=${encodeURIComponent(this.returnUrl)}`;
    window.location.href = oauthUrl;
  }

  loginWithFacebook(): void {
    // Placeholder for optional Facebook OAuth integration if product roadmap requires it
    this.error = 'Facebook login not yet implemented.';
  }

  loginWithApple(): void {
    // Placeholder for optional Apple OAuth integration if product roadmap requires it
    this.error = 'Apple login not yet implemented.';
  }
}
