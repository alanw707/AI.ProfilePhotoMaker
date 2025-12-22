import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-security',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Security & Trust</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 19, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Security Controls</h2>
          <ul>
            <li>Access controls and authentication safeguards to protect user accounts.</li>
            <li>Encrypted transport (HTTPS) for data in transit.</li>
            <li>Input validation and file safety checks for uploads.</li>
            <li>Operational logging for security and reliability monitoring.</li>
          </ul>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. Data Protection</h2>
          <p>
            We apply retention limits for uploaded and generated images, and provide user controls
            for deleting photos, models, and accounts. See the
            <a routerLink="/legal/privacy">Privacy Policy</a>
            for details.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. Third-Party Providers</h2>
          <p>
            We use trusted providers for AI processing and payments. See our
            <a routerLink="/legal/subprocessors">Subprocessors</a>
            list for details.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Incident Reporting</h2>
          <p>
            If you believe your account or data is at risk, contact us immediately at
            security&#64;aiprofilephotomaker.com.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. Responsible Disclosure</h2>
          <p>
            We welcome responsible security reports. Please provide clear reproduction steps and
            any relevant context when reporting potential issues.
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
export class SecurityComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Security & Trust - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'Security and trust practices for AI Profile Photo Maker.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
