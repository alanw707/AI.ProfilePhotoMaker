import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
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
export class LoginComponent implements OnInit {
  loginForm: FormGroup;
  loading = false;
  error = '';
  returnUrl = '';

  constructor(
    private _formBuilder: FormBuilder,
    private _authService: AuthService,
    private _router: Router,
    private _route: ActivatedRoute,
    public themeService: ThemeService
  ) {
    this.loginForm = this._formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
    });

    // Get return URL from route parameters or default to profile
    this.returnUrl = this._route.snapshot.queryParams['returnUrl'] || '/app/dashboard';
  }

  ngOnInit(): void {
    // Check if user is already logged in
    const isAuthenticated = this._authService.isAuthenticated();
    if (isAuthenticated) {
      this._router.navigate([this.returnUrl]);
      return;
    }

    // Handle OAuth callbacks
    this._route.queryParams.subscribe(params => {
      if (this.handleDirectTokenParams(params)) return;
      if (this.handleTokenInReturnUrl(params)) return;
      if (this.handleTokenInFragment()) return;

      if (params['error']) {
        this.error = 'OAuth login failed: ' + params['error'];
      }
    });
  }

  private handleDirectTokenParams(params: any): boolean {
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

  private handleTokenInReturnUrl(params: any): boolean {
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

  private handleTokenInFragment(): boolean {
    const fragment = window.location.hash;
    if (fragment && fragment.includes('token=')) {
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

  private extractUserFromToken(token: string): any {
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

  get f() {
    return this.loginForm.controls;
  }

  onSubmit() {
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
      next: response => {
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

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  navigateToRegister() {
    this._router.navigate(['/auth/register']);
  }

  loginWithGoogle() {
    // Get OAuth base URL from config service using modern inject function
    const configService = inject(ConfigService);
    const oauthBaseUrl = configService.getOAuthBaseUrl();

    // Use standard OAuth flow - redirect to the external login endpoint
    const oauthUrl = `${oauthBaseUrl}/api/auth/external-login/google?returnUrl=${encodeURIComponent(this.returnUrl)}`;

    window.location.href = oauthUrl;
  }

  loginWithFacebook() {
    // TODO: Implement Facebook OAuth when needed
    this.error = 'Facebook login not yet implemented.';
  }

  loginWithApple() {
    // TODO: Implement Apple OAuth when needed
    this.error = 'Apple login not yet implemented.';
  }
}
