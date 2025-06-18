import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService, LoginDto } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { ConfigService } from '../../services/config.service';

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
    private route: ActivatedRoute,
    public themeService: ThemeService,
    private configService: ConfigService
  ) {
    this.loginForm = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });

    // Get return URL from route parameters or default to profile
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
  }

  ngOnInit() {
    console.log('LoginComponent ngOnInit');
    console.log('Return URL:', this.returnUrl);
    
    // Check if user is already logged in
    const isAuthenticated = this.authService.isAuthenticated();
    console.log('User already authenticated:', isAuthenticated);
    
    if (isAuthenticated) {
      console.log('Redirecting authenticated user to:', this.returnUrl);
      this.router.navigate([this.returnUrl]);
      return;
    }

    // Check if this is an OAuth callback
    this.route.queryParams.subscribe(params => {
      console.log('Query params:', params);
      
      // Check if token is directly in params
      if (params['token']) {
        console.log('Direct OAuth token detected');
        this.authService.handleOAuthCallback(params['token'], params['expiration']);
        this.router.navigate(['/dashboard']);
        return;
      }
      
      // Check if token is embedded in returnUrl (OAuth callback scenario)
      if (params['returnUrl'] && params['returnUrl'].includes('token=')) {
        console.log('OAuth token found in returnUrl');
        const urlObj = new URL('http://dummy.com' + params['returnUrl']);
        const token = urlObj.searchParams.get('token');
        const expiration = urlObj.searchParams.get('expiration');
        
        if (token) {
          console.log('Extracted token from returnUrl');
          this.authService.handleOAuthCallback(token, expiration || undefined);
          this.router.navigate(['/dashboard']);
          return;
        }
      }
      
      if (params['error']) {
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
    console.log('Login form submitted');
    this.error = '';
    
    if (this.loginForm.invalid) {
      console.log('Form is invalid');
      return;
    }

    this.loading = true;
    const loginData: LoginDto = {
      email: this.f['email'].value,
      password: this.f['password'].value
    };

    console.log('Attempting login with:', { email: loginData.email });

    this.authService.login(loginData).subscribe({
      next: (response) => {
        console.log('Login successful, response:', response);
        console.log('Navigating to:', this.returnUrl);
        this.loading = false;
        this.router.navigate([this.returnUrl]);
      },
      error: (error) => {
        console.error('Login error:', error);
        this.error = error.message || 'Login failed. Please try again.';
        this.loading = false;
      }
    });
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  navigateToRegister() {
    this.router.navigate(['/register']);
  }

  loginWithGoogle() {
    // Use configuration-based URL for Google OAuth
    window.location.href = `${this.configService.appBaseUrl}/api/auth/external-login/Google?returnUrl=${this.returnUrl}`;
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
