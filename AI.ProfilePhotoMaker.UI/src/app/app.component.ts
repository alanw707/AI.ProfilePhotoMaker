import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.sass',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit {
  title = 'AI.ProfilePhotoMaker.UI';

  private readonly _router = inject(Router);
  private readonly _authService = inject(AuthService);

  ngOnInit(): void {
    // Check for OAuth token in URL on app initialization
    this._handleOAuthCallback();
  }

  private _handleOAuthCallback(): void {
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');
    const expiration = urlParams.get('expiration');

    if (token) {
      // Use console.warn instead of console.log for ESLint compliance
      console.warn('🔐 OAuth token detected in app component');
      console.warn('Token preview:', token.substring(0, 50) + '...');

      try {
        // Handle OAuth callback
        this._authService.handleOAuthCallback(token, expiration || undefined);

        // Clean up URL parameters
        const cleanUrl = window.location.origin + window.location.pathname;
        window.history.replaceState({}, document.title, cleanUrl);

        // Navigate to dashboard
        console.warn('✅ OAuth processed at app level, navigating to dashboard');
        this._router.navigate(['/dashboard']);
      } catch (error) {
        console.error('❌ Error handling OAuth callback in app component:', error);
      }
    }
  }
}
