import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-refund-policy',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Refund Policy</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 20, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Scope</h2>
          <p>
            This Refund Policy explains when refunds are available for AI Profile Photo Maker
            purchases. It applies to paid credit packages and subscriptions unless required by
            law to provide additional rights.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. 7-Day Satisfaction Guarantee</h2>
          <p>
            We offer a 7-day satisfaction guarantee from the date of purchase. If you are not
            satisfied, contact us within 7 days and we will review your request.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. Eligibility</h2>
          <ul>
            <li>Requests must be submitted within 7 days of purchase.</li>
            <li>Refunds may be adjusted if credits have been heavily used.</li>
            <li>We may decline refunds for misuse, abuse, or policy violations.</li>
          </ul>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Subscriptions and Cancellations</h2>
          <p>
            You can cancel a subscription at any time. Cancellation takes effect at the end of the
            current billing period unless required by law.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. How to Request a Refund</h2>
          <p>
            Email legal&#64;aiprofilephotomaker.com with your account email, purchase details, and
            reason for the request. We will respond with next steps.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">6. Contact Us</h2>
          <p>
            Questions about refunds? Contact legal&#64;aiprofilephotomaker.com.
          </p>
        </div>

        <div class="mt-8 text-center">
          <a routerLink="/" class="text-primary-600 hover:text-primary-700 font-medium"
            >&larr; Back to Home</a
          >
        </div>
      </div>
    </div>
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
      content: 'Refund Policy for AI Profile Photo Maker purchases and subscriptions.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
