import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { AuthService } from './services/auth.service';
import { ThemeService } from './services/theme.service';
import { NotificationComponent } from './components/shared/notification/notification.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NotificationComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.sass',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit {
  title = 'AI.ProfilePhotoMaker.UI';

  private readonly _router = inject(Router);
  private readonly _authService = inject(AuthService);
  private readonly _themeService = inject(ThemeService);

  ngOnInit(): void {
    // Initialize theme service to ensure proper theme application
    this._themeService.setTheme(this._themeService.getCurrentTheme());

    // Check for OAuth token in URL on app initialization
    this._handleOAuthCallback();
  }

  private _handleOAuthCallback(): void {
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');
    const expiration = urlParams.get('expiration');

    if (token) {
      try {
        // Handle OAuth callback
        this._authService.handleOAuthCallback(token, expiration || undefined);

        // Clean up URL parameters
        const cleanUrl = window.location.origin + window.location.pathname;
        window.history.replaceState({}, document.title, cleanUrl);

        // Navigate to dashboard
        this._router.navigate(['/dashboard']);
      } catch (error) {
        console.error('Error handling OAuth callback:', error);
      }
    }
  }
}
