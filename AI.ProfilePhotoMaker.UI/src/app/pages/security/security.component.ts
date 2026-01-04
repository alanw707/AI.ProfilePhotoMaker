import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-security',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">Security & Trust</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 19, 2025</p>

          <h2>1. Security Controls</h2>
          <ul>
            <li>Access controls and authentication safeguards to protect user accounts.</li>
            <li>Encrypted transport (HTTPS) for data in transit.</li>
            <li>Input validation and file safety checks for uploads.</li>
            <li>Operational logging for security and reliability monitoring.</li>
          </ul>

          <h2>2. Data Protection</h2>
          <p>
            We apply retention limits for uploaded and generated images, and provide user controls
            for deleting photos, models, and accounts. See the
            <a routerLink="/legal/privacy">Privacy Policy</a>
            for details.
          </p>

          <h2>3. Third-Party Providers</h2>
          <p>
            We use trusted providers for AI processing and payments. See our
            <a routerLink="/legal/subprocessors">Subprocessors</a>
            list for details.
          </p>

          <h2>4. Incident Reporting</h2>
          <p>
            If you believe your account or data is at risk, contact us immediately at
            support&#64;aiprofilephotomaker.com.
          </p>

          <h2>5. Responsible Disclosure</h2>
          <p>
            We welcome responsible security reports. Please provide clear reproduction steps and any
            relevant context when reporting potential issues.
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
