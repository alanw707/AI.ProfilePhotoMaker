import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService, RegisterDto } from '../../services/auth.service';
import { ConfigService } from '../../services/config.service';

@Component({
  selector: 'app-register',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './register.component.html',
  styleUrl: './register.component.sass'
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

  passwordMatchValidator(control: AbstractControl): { [key: string]: any } | null {
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
    window.location.href = `${this.configService.appBaseUrl}/api/auth/external-login/Google?returnUrl=/dashboard`;
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
