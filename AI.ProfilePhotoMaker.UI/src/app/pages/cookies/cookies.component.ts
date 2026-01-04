import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { CookieConsentService } from '../../services/cookie-consent.service';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-cookies',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">Cookie Policy</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 25, 2025</p>

          <h2>1. Scope</h2>
          <p>
            This Cookie Policy explains how AI Profile Photo Maker uses cookies and similar
            technologies (such as local storage) on our website and services.
          </p>

          <h2>2. What Are Cookies?</h2>
          <p>
            Cookies are small text files placed on your device when you visit a website. We may also
            use similar technologies to store preferences and keep you signed in.
          </p>

          <h2>3. How We Use Cookies</h2>
          <p>We use cookies or similar storage for:</p>
          <ul>
            <li>Authentication and session management.</li>
            <li>Security and fraud prevention.</li>
            <li>Remembering preferences and settings.</li>
            <li>
              Analytics (Google Analytics 4) to understand usage and improve the product when you
              opt in to analytics cookies.
            </li>
          </ul>
          <p>Analytics cookies are only set after you grant consent.</p>

          <h2>4. Third-Party Cookies</h2>
          <p>
            Some third-party services may set cookies during their workflows, such as Google OAuth
            login or Stripe payments. We also use Google Analytics 4 when you opt in to analytics
            cookies. These providers control their own cookies.
          </p>

          <h2>5. Your Choices</h2>
          <p>
            You can control cookies through your browser settings. Disabling cookies may impact
            login and certain features.
          </p>
          <div class="mt-4">
            <button
              type="button"
              class="rounded-md bg-gray-900 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-800"
              (click)="openCookiePreferences()"
            >
              Manage Cookie Preferences
            </button>
          </div>

          <h2>6. Changes</h2>
          <p>
            We may update this policy. If changes are material, we will post notice in the app or on
            our website.
          </p>

          <h2>7. Contact Us</h2>
          <p>
            If you have questions about this Cookie Policy, contact us at
            support&#64;aiprofilephotomaker.com.
          </p>
        </div>

        <div class="legal-footer-link">
          <a routerLink="/" class="legal-link">← Back to Home</a>
        </div>
      </div>
    </div>
    <app-marketing-footer></app-marketing-footer>
  `,
  styles: [],
})
export class CookiesComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title,
    private cookieConsentService: CookieConsentService
  ) {}

  ngOnInit() {
    this.title.setTitle('Cookie Policy - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'Cookie Policy for AI Profile Photo Maker and how we use cookies.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }

  openCookiePreferences(): void {
    this.cookieConsentService.requestPreferencesOpen();
  }
}
