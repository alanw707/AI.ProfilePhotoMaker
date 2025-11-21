import { ChangeDetectionStrategy, Component } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService, RegisterDto } from '../../services/auth.service';
import { ConfigService } from '../../services/config.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  registerForm: FormGroup;
  loading = false;
  error = '';

  constructor(
    private _formBuilder: FormBuilder,
    private _authService: AuthService,
    private _router: Router,
    private _configService: ConfigService
  ) {
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
      const allErrors = Object.values(error.error.errors)
        .flat()
        .filter(Boolean)
        .join(' ');
      if (allErrors) {
        return allErrors;
      }
    }

    if (error?.error?.title) {
      return error.error.title;
    }

    return error?.message || 'Registration failed. Please try again.';
  }

  onSubmit(): void {
    this.error = '';

    if (this.registerForm.invalid) {
      return;
    }

    this.loading = true;
    const registerData: RegisterDto = {
      firstName: this.f['firstName'].value,
      lastName: this.f['lastName'].value,
      email: this.f['email'].value,
      password: this.f['password'].value,
      gender: this.f['gender'].value,
      ethnicity: this.f['ethnicity'].value,
    };

    this._authService.register(registerData).subscribe({
      next: _response => {
        // Registration successful, navigate to dashboard
        this._router.navigate(['/app/dashboard']);
      },
      error: error => {
        const apiMessage = this.extractErrorMessage(error);
        this.error = apiMessage;
        this.loading = false;
      },
    });
  }

  navigateToLogin(): void {
    this._router.navigate(['/auth/login']);
  }

  registerWithGoogle(): void {
    // Use consistent OAuth base URL method for registration
    const oauthBaseUrl = this._configService.getOAuthBaseUrl();
    const fullReturnUrl = `${this._configService.frontendBaseUrl}/app/dashboard`;

    const oauthUrl = `${oauthBaseUrl}/api/auth/external-login/Google?returnUrl=${encodeURIComponent(fullReturnUrl)}`;
    window.location.href = oauthUrl;
  }

  registerWithFacebook(): void {
    // Placeholder for optional Facebook OAuth integration if product roadmap requires it
    this.error = 'Facebook registration not yet implemented.';
  }

  registerWithApple(): void {
    // Placeholder for optional Apple OAuth integration if product roadmap requires it
    this.error = 'Apple registration not yet implemented.';
  }
}
