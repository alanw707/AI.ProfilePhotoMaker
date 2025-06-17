import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService, LoginRequest } from '../../auth.service';

@Component({
  selector: 'app-login',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.sass'
})
export class LoginComponent implements OnInit {
  loginForm: FormGroup;
  loading = false;
  error = '';
  returnUrl = '';

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.loginForm = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });

    // Get return URL from route parameters or default to profile
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
  }

  ngOnInit() {
    // Check if this is an OAuth callback
    this.route.queryParams.subscribe(params => {
      if (params['token']) {
        // OAuth successful - store token and redirect
        localStorage.setItem('authToken', params['token']);
        if (params['expiration']) {
          localStorage.setItem('tokenExpiration', params['expiration']);
        }
        
        // Get user info from token and update auth service
        const user = this.extractUserFromToken(params['token']);
        if (user) {
          localStorage.setItem('currentUser', JSON.stringify(user));
        }
        
        this.router.navigate([this.returnUrl]);
      } else if (params['error']) {
        // OAuth failed
        this.error = 'OAuth login failed: ' + params['error'];
      }
    });
  }

  private extractUserFromToken(token: string): any {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return {
        email: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || payload.email,
        firstName: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'] || '',
        lastName: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'] || ''
      };
    } catch {
      return null;
    }
  }

  get f() { return this.loginForm.controls; }

  onSubmit() {
    this.error = '';
    
    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    const loginData: LoginRequest = {
      email: this.f['email'].value,
      password: this.f['password'].value
    };

    this.authService.login(loginData).subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.router.navigate([this.returnUrl]);
        } else {
          this.error = response.message;
          this.loading = false;
        }
      },
      error: (error) => {
        this.error = error.error?.message || 'Login failed. Please try again.';
        this.loading = false;
      }
    });
  }

  navigateToRegister() {
    this.router.navigate(['/register']);
  }

  loginWithGoogle() {
    // Direct redirect to Google OAuth
    window.location.href = `https://057a-71-38-148-86.ngrok-free.app/api/auth/external-login/Google?returnUrl=${this.returnUrl}`;
  }

  loginWithFacebook() {
    this.authService.externalLogin('Facebook', this.returnUrl).subscribe({
      next: (response) => {
        window.location.href = response.redirectUrl;
      },
      error: (error) => {
        this.error = 'Facebook login failed. Please try again.';
      }
    });
  }

  loginWithApple() {
    this.authService.externalLogin('Apple', this.returnUrl).subscribe({
      next: (response) => {
        window.location.href = response.redirectUrl;
      },
      error: (error) => {
        this.error = 'Apple login failed. Please try again.';
      }
    });
  }
}
