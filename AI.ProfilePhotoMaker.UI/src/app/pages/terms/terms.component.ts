import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-terms',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">Terms of Service</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 19, 2025</p>

          <h2>1. Acceptance of Terms</h2>
          <p>
            By accessing and using AI Profile Photo Maker, you accept and agree to be bound by the
            terms and provision of this agreement.
          </p>

          <h2>2. Use License</h2>
          <p>
            We grant you a personal, non-exclusive, non-transferable license to use our service for
            creating AI-enhanced profile photos. You retain all rights to your original photos. We
            grant you a license to use the AI-generated outputs created for you.
          </p>

          <h2>3. User Obligations</h2>
          <p>
            You agree to use the service only for lawful purposes and in accordance with these
            Terms. You must not upload inappropriate, offensive, or copyrighted content that you do
            not own or have rights to use.
          </p>

          <h2>4. AI Processing</h2>
          <p>
            By using the service, you direct us to process your photos using third-party AI
            providers to create outputs. You are responsible for ensuring you have the rights and
            permissions to upload and process the photos you submit.
          </p>

          <h2>5. Privacy and Data</h2>
          <p>
            Our Privacy Policy explains how we collect, use, retain, and share data, including
            retention of input photos and generated images. Data export currently includes account
            metadata and usage records, not photo files. By using the service, you agree to the
            Privacy Policy.
          </p>

          <h2>6. Payment Terms</h2>
          <p>
            Purchases are final and non-refundable, except as required by law or as explicitly
            stated in our
            <a routerLink="/legal/refund-policy">Refund Policy</a>. We offer a 14-day satisfaction
            guarantee.
          </p>

          <h2>7. Limitation of Liability</h2>
          <p>
            AI Profile Photo Maker shall not be liable for any indirect, incidental, special,
            consequential, or punitive damages resulting from your use of the service.
          </p>

          <h2>8. Contact Information</h2>
          <p>
            For any questions regarding these Terms of Service, please contact us at
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
export class TermsComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Terms of Service - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'Terms of Service for AI Profile Photo Maker. Read our terms and conditions.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
