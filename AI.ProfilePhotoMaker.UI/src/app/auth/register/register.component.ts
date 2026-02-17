import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ViewChild } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService, RegisterDto } from '../../services/auth.service';
import { ConfigService } from '../../services/config.service';
import { TurnstileComponent } from '../../shared/turnstile/turnstile.component';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, TurnstileComponent],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  @ViewChild(TurnstileComponent) turnstile?: TurnstileComponent;
  registerForm: FormGroup;
  loading = false;
  error = '';
  turnstileToken = '';
  readonly turnstileSiteKey: string;

  constructor(
    private _formBuilder: FormBuilder,
    private _authService: AuthService,
    private _router: Router,
    private _configService: ConfigService,
    private _cdr: ChangeDetectorRef
  ) {
    this.turnstileSiteKey = this._configService.turnstileSiteKey;
    this.registerForm = this._formBuilder.group(
      {
        firstName: ['', [Validators.required, Validators.minLength(2)]],
        lastName: ['', [Validators.required, Validators.minLength(2)]],
        email: ['', [Validators.required, Validators.email]],
        password: [
          '',
          [
            Validators.required,
            Validators.minLength(8),
            Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$/), // upper, lower, digit, symbol
          ],
        ],
        confirmPassword: ['', Validators.required],
        gender: ['', Validators.required],
        ethnicity: ['', Validators.required],
        ageConfirmed: [false, Validators.requiredTrue],
      },
      {
        validators: this.passwordMatchValidator,
      }
    );
  }

  get f(): Record<string, AbstractControl> {
    return this.registerForm.controls;
  }

  // Template helper methods to reduce cyclomatic complexity
  shouldShowEmailError(): boolean {
    return this.f['email'].invalid && this.f['email'].touched;
  }

  shouldShowPasswordError(): boolean {
    return this.f['password'].invalid && this.f['password'].touched;
  }
  shouldShowPasswordComplexityError(): boolean {
    return (
      this.f['password'].errors?.['pattern'] &&
      this.f['password'].touched &&
      !this.f['password'].errors?.['minlength']
    );
  }

  shouldShowConfirmPasswordError(): boolean {
    return this.f['confirmPassword'].invalid && this.f['confirmPassword'].touched;
  }

  shouldShowFirstNameError(): boolean {
    return this.f['firstName'].invalid && this.f['firstName'].touched;
  }

  shouldShowLastNameError(): boolean {
    return this.f['lastName'].invalid && this.f['lastName'].touched;
  }

  shouldShowGenderError(): boolean {
    return this.f['gender'].invalid && this.f['gender'].touched;
  }

  shouldShowEthnicityError(): boolean {
    return this.f['ethnicity'].invalid && this.f['ethnicity'].touched;
  }

  shouldShowAgeError(): boolean {
    return this.f['ageConfirmed'].invalid && this.f['ageConfirmed'].touched;
  }

  passwordMatchValidator(control: AbstractControl): Record<string, boolean> | null {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');

    if (password && confirmPassword && password.value !== confirmPassword.value) {
      return { passwordMismatch: true };
    }
    return null;
  }

  private extractErrorMessage(error: any): string {
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

    if (error?.error?.title) {
      return error.error.title;
    }

    return error?.message || 'Registration failed. Please try again.';
  }

  onTurnstileTokenChange(token: string): void {
    this.turnstileToken = token;
    this._cdr.markForCheck();
  }

  onSubmit(): void {
    this.error = '';

    if (this.registerForm.invalid) {
      return;
    }

    if (this.turnstileSiteKey && !this.turnstileToken) {
      this.error = this.turnstile?.error || 'Please complete the verification to continue.';
      this._cdr.markForCheck();
      return;
    }

    this.loading = true;
    this._cdr.markForCheck();
    const registerData: RegisterDto = {
      firstName: this.f['firstName'].value,
      lastName: this.f['lastName'].value,
      email: this.f['email'].value,
      password: this.f['password'].value,
      gender: this.f['gender'].value,
      ethnicity: this.f['ethnicity'].value,
      ageConfirmed: this.f['ageConfirmed'].value,
      turnstileToken: this.turnstileSiteKey ? this.turnstileToken : undefined,
    };

    this._authService
      .register(registerData)
      .pipe(
        finalize(() => {
          this.loading = false;
          this._cdr.markForCheck();
        })
      )
      .subscribe({
        next: _response => {
          // Registration successful. Require email verification before accessing /app.
          this._router
            .navigate(['/auth/verify-email'])
            .then(success => {
              if (success === false) {
                this.error =
                  'Registration succeeded, but navigation failed. Please try logging in.';
                this.turnstileToken = '';
                this.turnstile?.reset();
                this._cdr.markForCheck();
              }
            })
            .catch(error => {
              console.error('Registration navigation error:', error);
              this.error = 'Registration succeeded, but navigation failed. Please try logging in.';
              this.turnstileToken = '';
              this.turnstile?.reset();
              this._cdr.markForCheck();
            });
        },
        error: error => {
          const apiMessage = this.extractErrorMessage(error);
          this.error = apiMessage;
          this.turnstileToken = '';
          this.turnstile?.reset();
          this._cdr.markForCheck();
        },
      });
  }

  navigateToLogin(): void {
    this._router.navigate(['/auth/login']);
  }

  registerWithGoogle(): void {
    if (this.f['ageConfirmed'].value !== true) {
      this.f['ageConfirmed'].markAsTouched();
      return;
    }

    // Use consistent OAuth base URL method for registration
    const oauthBaseUrl = this._configService.getOAuthBaseUrl();
    const fullReturnUrl = `${this._configService.frontendBaseUrl}/app/dashboard`;
    const query = new URLSearchParams({
      returnUrl: fullReturnUrl,
      ageConfirmed: 'true',
    });

    // ngrok free domains can show an interstitial page that drops OAuth nonce cookies.
    // This query parameter bypasses that warning page for local OAuth testing.
    if (oauthBaseUrl.includes('ngrok')) {
      query.set('ngrok-skip-browser-warning', 'true');
    }

    const oauthUrl = `${oauthBaseUrl}/api/auth/external-login/Google?${query.toString()}`;
    window.location.href = oauthUrl;
  }
}
