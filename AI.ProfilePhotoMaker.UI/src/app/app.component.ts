import { Component, OnInit } from '@angular/core';
import { RouterOutlet, Router, ActivatedRoute } from '@angular/router';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.sass'
})
export class AppComponent implements OnInit {
  title = 'AI.ProfilePhotoMaker.UI';

  constructor(
    private router: Router,
    private authService: AuthService
  ) {}

  ngOnInit() {
    // Check for OAuth token in URL on app initialization
    this.handleOAuthCallback();
  }

  private handleOAuthCallback() {
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');
    const expiration = urlParams.get('expiration');
    
    if (token) {
      console.log('🔐 OAuth token detected in app component');
      console.log('Token preview:', token.substring(0, 50) + '...');
      
      try {
        // Handle OAuth callback
        this.authService.handleOAuthCallback(token, expiration || undefined);
        
        // Clean up URL parameters
        const cleanUrl = window.location.origin + window.location.pathname;
        window.history.replaceState({}, document.title, cleanUrl);
        
        // Navigate to dashboard
        console.log('✅ OAuth processed at app level, navigating to dashboard');
        this.router.navigate(['/dashboard']);
      } catch (error) {
        console.error('❌ Error handling OAuth callback in app component:', error);
      }
    }
  }
}
