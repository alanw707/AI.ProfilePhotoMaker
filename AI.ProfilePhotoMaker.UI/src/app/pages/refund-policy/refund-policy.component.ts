import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-refund-policy',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">Refund Policy</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 20, 2025</p>

          <h2>1. Scope</h2>
          <p>
            This Refund Policy explains when refunds are available for AI Profile Photo Maker
            purchases. It applies to paid credit packages unless required by law to provide
            additional rights.
          </p>

          <h2>2. 14-Day Satisfaction Guarantee</h2>
          <p>
            We offer a 14-day satisfaction guarantee from the date of purchase. If you are not
            satisfied, contact us within 14 days and we will review your request.
          </p>

          <h2>3. Eligibility</h2>
          <ul>
            <li>Requests must be submitted within 14 days of purchase.</li>
            <li>Refunds may be adjusted if credits have been heavily used.</li>
            <li>We may decline refunds for misuse, abuse, or policy violations.</li>
          </ul>

          <h2>4. How to Request a Refund</h2>
          <p>
            Email support&#64;aiprofilephotomaker.com with your account email, purchase details, and
            reason for the request. We will respond with next steps.
          </p>

          <h2>5. Contact Us</h2>
          <p>Questions about refunds? Contact support&#64;aiprofilephotomaker.com.</p>
        </div>

        <div class="legal-footer-link">
          <a routerLink="/" class="legal-link">&larr; Back to Home</a>
        </div>
      </div>
    </div>
    <app-marketing-footer></app-marketing-footer>
  `,
  styles: [],
})
export class RefundPolicyComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Refund Policy - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'Refund Policy for AI Profile Photo Maker purchases and credit packages.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
