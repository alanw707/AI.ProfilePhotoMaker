
import { Inject, Injectable, DOCUMENT } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { CookieConsentService } from './cookie-consent.service';
import { environment } from '../../environments/environment';

type GtagFunction = (...args: unknown[]) => void;

declare global {
  interface Window {
    dataLayer?: unknown[];
    gtag?: GtagFunction;
  }
}

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly measurementId = environment.analytics?.ga4MeasurementId ?? '';
  private initialized = false;
  private enabled = false;

  constructor(
    private readonly router: Router,
    private readonly cookieConsentService: CookieConsentService,
    @Inject(DOCUMENT) private readonly document: Document
  ) {}

  init(): void {
    if (this.initialized || typeof window === 'undefined') {
      return;
    }
    this.initialized = true;

    if (!this.measurementId) {
      return;
    }

    this.cookieConsentService.consent$.subscribe(consent => {
      const allowAnalytics = !!consent?.analytics;
      if (allowAnalytics) {
        this.enableAnalytics();
      } else {
        this.disableAnalytics();
      }
    });

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(event => {
        if (!this.enabled) {
          return;
        }
        const navigation = event as NavigationEnd;
        this.trackPageView(navigation.urlAfterRedirects);
      });
  }

  trackEvent(eventName: string, params: Record<string, unknown> = {}): void {
    if (!this.enabled) {
      return;
    }
    this.gtag('event', eventName, params);
  }

  private enableAnalytics(): void {
    if (this.enabled) {
      return;
    }
    this.enabled = true;
    if (!this.getGtag()) {
      return;
    }
    // eslint-disable-next-line @typescript-eslint/naming-convention -- GA4 param name required
    this.gtag('consent', 'update', { analytics_storage: 'granted' });
    this.trackPageView(this.router.url || window.location.pathname + window.location.search);
  }

  private disableAnalytics(): void {
    this.enabled = false;
    if (this.getGtag()) {
      // eslint-disable-next-line @typescript-eslint/naming-convention -- GA4 param name required
      this.gtag('consent', 'update', { analytics_storage: 'denied' });
    }
  }

  private trackPageView(path: string): void {
    this.gtag('config', this.measurementId, {
      // eslint-disable-next-line @typescript-eslint/naming-convention -- GA4 param name required
      page_path: path,
      // eslint-disable-next-line @typescript-eslint/naming-convention -- GA4 param name required
      page_location: window.location.href,
      // eslint-disable-next-line @typescript-eslint/naming-convention -- GA4 param name required
      page_title: this.document.title,
    });
  }

  private gtag(...args: unknown[]): void {
    const gtagFn = this.getGtag();
    if (!gtagFn) {
      return;
    }
    gtagFn(...args);
  }

  private getGtag(): GtagFunction | null {
    if (typeof window === 'undefined') {
      return null;
    }
    return typeof window.gtag === 'function' ? window.gtag : null;
  }
}
