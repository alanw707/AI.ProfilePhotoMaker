import { Component } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService, RegisterDto } from '../../services/auth.service';
import { ConfigService } from '../../services/config.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.sass']
})
export class RegisterComponent {
  registerForm: FormGroup;
  loading = false;
  error = '';

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private configService: ConfigService
  ) {
    this.registerForm = this.formBuilder.group({
      firstName: ['', [Validators.required, Validators.minLength(2)]],
      lastName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', Validators.required],
      gender: ['', Validators.required],
      ethnicity: ['', Validators.required]
    }, {
      validators: this.passwordMatchValidator
    });
  }

  get f() { return this.registerForm.controls; }

  passwordMatchValidator(control: AbstractControl): Record<string, any> | null {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');
    
    if (password && confirmPassword && password.value !== confirmPassword.value) {
      return { passwordMismatch: true };
    }
    return null;
  }

  onSubmit() {
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
      ethnicity: this.f['ethnicity'].value
    };

    this.authService.register(registerData).subscribe({
      next: (response) => {
        // Registration successful, navigate to dashboard
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.error = error.message || 'Registration failed. Please try again.';
        this.loading = false;
      }
    });
  }

  navigateToLogin() {
    this.router.navigate(['/login']);
  }

  registerWithGoogle() {
    // Use configuration-based URL for Google OAuth registration
    // Use configuration-based URL for Google OAuth with dynamic redirect handling
    const oauthBaseUrl = this.configService.getOAuthRedirectUrl();
    const fullReturnUrl = `${this.configService.frontendBaseUrl}/dashboard`;
    
    console.log('OAuth redirect details (register):', {
      oauthBaseUrl,
      fullReturnUrl,
      isExternalAccess: this.configService.isExternalAccess(),
      currentOrigin: window.location.origin
    });
    
    window.location.href = `${oauthBaseUrl}/api/auth/external-login/Google?returnUrl=${encodeURIComponent(fullReturnUrl)}`;
  }

  registerWithFacebook() {
    // TODO: Implement Facebook OAuth when needed
    this.error = 'Facebook registration not yet implemented.';
  }

  registerWithApple() {
    // TODO: Implement Apple OAuth when needed
    this.error = 'Apple registration not yet implemented.';
  }
}
