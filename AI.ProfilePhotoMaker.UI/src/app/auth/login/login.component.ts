import { Component, OnInit } from '@angular/core';
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
  styleUrls: ['./login.component.sass']
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
    console.log('=== LoginComponent ngOnInit ===');
    console.log('Current URL:', window.location.href);
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
      console.log('=== OAuth Callback Detection ===');
      console.log('All query params:', params);
      console.log('Current URL search params:', window.location.search);
      
      // Check if token is directly in params
      if (params['token']) {
        console.log('✅ Direct OAuth token detected in params');
        console.log('Token preview:', params['token'].substring(0, 50) + '...');
        console.log('Expiration:', params['expiration']);
        
        try {
          this.authService.handleOAuthCallback(params['token'], params['expiration']);
          console.log('✅ OAuth callback handled successfully');
          console.log('Navigating to dashboard...');
          this.router.navigate(['/dashboard']).then(success => {
            console.log('Navigation result:', success);
          });
        } catch (error) {
          console.error('❌ Error handling OAuth callback:', error);
          this.error = 'Failed to process OAuth token';
        }
        return;
      }
      
      // Check if token is embedded in returnUrl (OAuth callback scenario)
      if (params['returnUrl']?.includes('token=')) {
        console.log('✅ OAuth token found in returnUrl');
        console.log('ReturnUrl with token:', params['returnUrl']);
        
        try {
          const urlObj = new URL('http://dummy.com' + params['returnUrl']);
          const token = urlObj.searchParams.get('token');
          const expiration = urlObj.searchParams.get('expiration');
          
          if (token) {
            console.log('✅ Extracted token from returnUrl');
            console.log('Token preview:', token.substring(0, 50) + '...');
            console.log('Expiration:', expiration);
            
            this.authService.handleOAuthCallback(token, expiration || undefined);
            console.log('✅ OAuth callback handled successfully');
            console.log('Navigating to dashboard...');
            this.router.navigate(['/dashboard']).then(success => {
              console.log('Navigation result:', success);
            });
          } else {
            console.log('❌ No token found in returnUrl after parsing');
          }
        } catch (error) {
          console.error('❌ Error parsing returnUrl:', error);
          this.error = 'Failed to parse OAuth response';
        }
        return;
      }
      
      // Check URL fragment for token (some OAuth flows use fragments)
      const fragment = window.location.hash;
      if (fragment && fragment.includes('token=')) {
        console.log('✅ OAuth token found in URL fragment');
        console.log('Fragment:', fragment);
        
        try {
          const urlObj = new URL('http://dummy.com/?' + fragment.substring(1));
          const token = urlObj.searchParams.get('token');
          const expiration = urlObj.searchParams.get('expiration');
          
          if (token) {
            console.log('✅ Extracted token from URL fragment');
            console.log('Token preview:', token.substring(0, 50) + '...');
            console.log('Expiration:', expiration);
            
            this.authService.handleOAuthCallback(token, expiration || undefined);
            console.log('✅ OAuth callback handled successfully');
            console.log('Navigating to dashboard...');
            this.router.navigate(['/dashboard']).then(success => {
              console.log('Navigation result:', success);
            });
          }
        } catch (error) {
          console.error('❌ Error parsing URL fragment:', error);
          this.error = 'Failed to parse OAuth response';
        }
        return;
      }
      
      if (params['error']) {
        console.log('❌ OAuth error detected:', params['error']);
        this.error = 'OAuth login failed: ' + params['error'];
      }
      
      if (!params['token'] && !params['returnUrl']?.includes('token=') && !fragment?.includes('token=')) {
        console.log('ℹ️ No OAuth token detected - normal page load');
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
    // Get OAuth base URL from config service
    const oauthBaseUrl = this.configService.getOAuthBaseUrl();
    
    // Construct the OAuth URL with returnUrl parameter
    const oauthUrl = `${oauthBaseUrl}/api/auth/external-login/Google?returnUrl=${encodeURIComponent(this.returnUrl)}`;
    
    // Redirect to OAuth endpoint
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
