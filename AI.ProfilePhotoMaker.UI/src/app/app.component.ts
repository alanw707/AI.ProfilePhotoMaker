import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from './services/auth.service';
import { ThemeService } from './services/theme.service';
import { ConfigService } from './services/config.service';
import { AnalyticsService } from './services/analytics.service';
import { NotificationComponent } from './components/shared/notification/notification.component';
import { CookieConsentComponent } from './components/shared/cookie-consent/cookie-consent.component';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NotificationComponent, CookieConsentComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.sass',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit {
  title = 'AI.ProfilePhotoMaker.UI';

  private readonly _router = inject(Router);
  private readonly _authService = inject(AuthService);
  private readonly _themeService = inject(ThemeService);
  private readonly _config = inject(ConfigService);
  private readonly _analyticsService = inject(AnalyticsService);
  private readonly _document = inject(DOCUMENT);

  ngOnInit(): void {
    // Initialize theme service to ensure proper theme application
    this._themeService.setTheme(this._themeService.getCurrentTheme());

    // Initialize analytics tracking once consent is granted
    this._analyticsService.init();

    // Clean up OAuth URL token if present and navigate
    this._handleOAuthCallback();

    // Probe session only when navigating to protected routes
    this._router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(e => {
        const navigation = e as NavigationEnd;
        this._authService.probeSessionForUrl(navigation.urlAfterRedirects);
        this._updateCanonicalUrl(navigation.urlAfterRedirects);
      });
  }

  private _handleOAuthCallback(): void {
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');
    const host = window.location.hostname || '';
    const isDevLikeOrigin =
      host.includes('localhost') || host.includes('127.0.0.1') || host.includes('ngrok') || environment.name === 'docker-local';
    const shouldHandleToken = !!token && isDevLikeOrigin;

    if (shouldHandleToken) {
      // In development OAuth flow, set cookie via same-origin endpoint then clean URL
      const setCookieUrl = this._config.getFullUrl('/auth/set-cookie');

      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 2500);

      fetch(setCookieUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token }),
        credentials: 'include',
        signal: controller.signal,
      }).finally(() => {
        clearTimeout(timeout);
        const cleanUrl = window.location.origin + window.location.pathname;
        window.history.replaceState({}, document.title, cleanUrl);
        this._router.navigate(['/app/dashboard']);
      });
    }
  }

  private _updateCanonicalUrl(url: string): void {
    const origin = this._document.location?.origin ?? window.location.origin;
    const canonicalUrl = new URL(url, origin);
    canonicalUrl.search = '';
    canonicalUrl.hash = '';

    let canonicalLink = this._document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!canonicalLink) {
      canonicalLink = this._document.createElement('link');
      canonicalLink.setAttribute('rel', 'canonical');
      this._document.head.appendChild(canonicalLink);
    }

    canonicalLink.setAttribute('href', canonicalUrl.toString());
  }
}
