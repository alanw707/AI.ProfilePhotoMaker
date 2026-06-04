import { Injectable } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Location } from '@angular/common';
import { filter } from 'rxjs/operators';

@Injectable({
  providedIn: 'root',
})
export class NavigationService {
  private navigationHistory: string[] = [];
  private maxHistoryLength = 10;

  constructor(
    private router: Router,
    private location: Location
  ) {
    // Track navigation history (router.events may be undefined in tests/mocks)
    const eventsStream: any = (this.router as any)?.events;
    if (eventsStream && typeof eventsStream.pipe === 'function') {
      eventsStream
        .pipe(filter((event: any) => event instanceof NavigationEnd))
        .subscribe((event: NavigationEnd) => {
          this.addToHistory(event.url);
        });
    }
  }

  private addToHistory(url: string): void {
    // Remove the current URL if it already exists to avoid duplicates
    const index = this.navigationHistory.indexOf(url);
    if (index > -1) {
      this.navigationHistory.splice(index, 1);
    }

    // Add to the beginning of the array
    this.navigationHistory.unshift(url);

    // Keep only the most recent entries
    if (this.navigationHistory.length > this.maxHistoryLength) {
      this.navigationHistory = this.navigationHistory.slice(0, this.maxHistoryLength);
    }
  }

  /**
   * Navigate to a route with enhanced UX
   */
  navigateTo(
    route: string | string[],
    options?: {
      queryParams?: any;
      fragment?: string;
      replaceUrl?: boolean;
    }
  ): Promise<boolean> {
    const navigationExtras = {
      queryParams: options?.queryParams,
      fragment: options?.fragment,
      replaceUrl: options?.replaceUrl || false,
    };

    if (typeof route === 'string') {
      return this.router.navigate([route], navigationExtras);
    } else {
      return this.router.navigate(route, navigationExtras);
    }
  }

  /**
   * Navigate back to the previous page or fallback route
   */
  goBack(fallbackRoute = '/'): void {
    if (this.navigationHistory.length > 1) {
      // Go to the previous page in our history
      const previousUrl = this.navigationHistory[1];
      this.router.navigateByUrl(previousUrl);
    } else if (window.history.length > 1) {
      // Use browser's back functionality
      this.location.back();
    } else {
      // Fallback to specified route
      this.router.navigate([fallbackRoute]);
    }
  }

  /**
   * Get the current navigation history
   */
  getHistory(): string[] {
    return [...this.navigationHistory];
  }

  /**
   * Check if we can go back
   */
  canGoBack(): boolean {
    return this.navigationHistory.length > 1 || window.history.length > 1;
  }

  /**
   * Navigate to home page
   */
  goHome(): Promise<boolean> {
    return this.navigateTo('/');
  }

  /**
   * Navigate to authentication routes
   */
  goToLogin(): Promise<boolean> {
    return this.navigateTo('/auth/login');
  }

  goToRegister(): Promise<boolean> {
    return this.navigateTo('/auth/register');
  }

  /**
   * Navigate to app routes (protected)
   */
  goToDashboard(): Promise<boolean> {
    return this.goToWorkspace();
  }

  goToWorkspace(): Promise<boolean> {
    return this.navigateTo('/app/enhance');
  }

  goToGallery(): Promise<boolean> {
    return this.navigateTo('/app/gallery');
  }

  goToEnhance(): Promise<boolean> {
    return this.navigateTo('/app/enhance');
  }

  goToSettings(): Promise<boolean> {
    return this.navigateTo('/app/settings');
  }

  goToSupport(): Promise<boolean> {
    return this.navigateTo('/app/support');
  }

  /**
   * Navigate to public pages
   */
  goToPricing(): Promise<boolean> {
    return this.navigateTo('/pricing');
  }

  goToPricingPlans(): Promise<boolean> {
    // Navigate to pricing and aggressively scroll to the packages once they render
    const targetId = 'packages-section';
    return this.navigateTo('/pricing').then(success => {
      if (success) {
        let attempts = 0;
        const maxAttempts = 8;
        const interval = setInterval(() => {
          const el = document.getElementById(targetId);
          if (el) {
            this.scrollToSection(targetId, 150);
            clearInterval(interval);
          } else if (++attempts >= maxAttempts) {
            clearInterval(interval);
          }
        }, 150);
      }
      return success;
    });
  }

  goToFeatures(): Promise<boolean> {
    return this.navigateTo('/features');
  }

  goToExamples(): Promise<boolean> {
    return this.navigateTo('/examples');
  }

  goToHelp(): Promise<boolean> {
    return this.navigateTo('/help');
  }

  /**
   * Navigate to legal pages
   */
  goToPrivacy(): Promise<boolean> {
    return this.navigateTo('/legal/privacy');
  }

  goToTerms(): Promise<boolean> {
    return this.navigateTo('/legal/terms');
  }

  /**
   * Scroll to section with smooth animation
   */
  scrollToSection(sectionId: string, offset = 80): void {
    setTimeout(() => {
      const element = document.getElementById(sectionId);
      if (element) {
        const elementPosition = element.getBoundingClientRect().top;
        const offsetPosition = elementPosition + window.pageYOffset - offset;

        window.scrollTo({
          top: offsetPosition,
          behavior: 'smooth',
        });
      }
    }, 100);
  }

  /**
   * Navigate to landing page section
   */
  navigateToSection(
    section: 'features' | 'examples' | 'pricing' | 'testimonials' | 'faq'
  ): Promise<boolean> {
    return this.navigateTo('/', { fragment: section }).then(success => {
      if (success) {
        this.scrollToSection(section);
      }
      return success;
    });
  }

  /**
   * Get current route information
   */
  getCurrentRoute(): string {
    return this.router.url;
  }

  /**
   * Check if current route matches
   */
  isCurrentRoute(route: string): boolean {
    return this.router.url === route;
  }

  /**
   * Check if current route starts with given path
   */
  isRouteActive(routePath: string): boolean {
    return this.router.url.startsWith(routePath);
  }
}
