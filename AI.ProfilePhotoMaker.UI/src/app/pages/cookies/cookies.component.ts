import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { CookieConsentService } from '../../services/cookie-consent.service';

@Component({
  selector: 'app-cookies',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Cookie Policy</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 19, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Scope</h2>
          <p>
            This Cookie Policy explains how AI Profile Photo Maker uses cookies and similar
            technologies (such as local storage) on our website and services.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. What Are Cookies?</h2>
          <p>
            Cookies are small text files placed on your device when you visit a website. We may also
            use similar technologies to store preferences and keep you signed in.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. How We Use Cookies</h2>
          <p>We currently use cookies or similar storage for:</p>
          <ul>
            <li>Authentication and session management.</li>
            <li>Security and fraud prevention.</li>
            <li>Remembering preferences and settings.</li>
          </ul>
          <p>
            If we add analytics or marketing cookies in the future, we will update this policy and
            request consent where required by law.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Third-Party Cookies</h2>
          <p>
            Some third-party services may set cookies during their workflows, such as Google OAuth
            login or Stripe payments. These providers control their own cookies.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. Your Choices</h2>
          <p>
            You can control cookies through your browser settings. Disabling cookies may impact
            login and certain features.
          </p>
          <div class="mt-4">
            <button
              type="button"
              class="rounded-md bg-gray-900 px-4 py-2 text-sm font-semibold text-white transition hover:bg-gray-800"
              (click)="openCookiePreferences()">
              Manage Cookie Preferences
            </button>
          </div>

          <h2 class="text-2xl font-semibold mt-8 mb-4">6. Changes</h2>
          <p>
            We may update this policy. If changes are material, we will post notice in the app or on
            our website.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">7. Contact Us</h2>
          <p>
            If you have questions about this Cookie Policy, contact us at
            support&#64;aiprofilephotomaker.com.
          </p>
        </div>

        <div class="mt-8 text-center">
          <a routerLink="/" class="text-primary-600 hover:text-primary-700 font-medium"
            >← Back to Home</a
          >
        </div>
      </div>
    </div>
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
