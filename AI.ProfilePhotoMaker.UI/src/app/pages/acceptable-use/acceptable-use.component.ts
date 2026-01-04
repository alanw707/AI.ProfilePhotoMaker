import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-acceptable-use',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">Acceptable Use Policy</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 19, 2025</p>

          <h2>1. Summary</h2>
          <p>
            You may use the service only for lawful purposes and in compliance with these rules. You
            are responsible for the content you upload and generate.
          </p>

          <h2>2. Prohibited Content</h2>
          <ul>
            <li>Content you do not own or lack rights to use.</li>
            <li>Images that impersonate another person or misrepresent identity.</li>
            <li>Illegal, abusive, or hateful content.</li>
            <li>Explicit sexual content or non-consensual imagery.</li>
            <li>Content that violates privacy or confidentiality.</li>
          </ul>

          <h2>3. Prohibited Behavior</h2>
          <ul>
            <li>Attempting to bypass security or access restrictions.</li>
            <li>Using the service to generate content for harassment or deception.</li>
            <li>Automated scraping or excessive usage that harms service stability.</li>
          </ul>

          <h2>4. Enforcement</h2>
          <p>
            We may suspend or terminate accounts that violate this policy or our Terms of Service.
          </p>

          <h2>5. Contact</h2>
          <p>Questions or concerns? Contact us at support&#64;aiprofilephotomaker.com.</p>
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
export class AcceptableUseComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Acceptable Use Policy - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'Acceptable use rules for AI Profile Photo Maker.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
