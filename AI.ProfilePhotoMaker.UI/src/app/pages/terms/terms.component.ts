import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-terms',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Terms of Service</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: January 28, 2024</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Acceptance of Terms</h2>
          <p>
            By accessing and using AI Profile Photo Maker, you accept and agree to be bound by the
            terms and provision of this agreement.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. Use License</h2>
          <p>
            We grant you a personal, non-exclusive, non-transferable license to use our service for
            creating AI-enhanced profile photos. You retain all rights to your original photos and
            the enhanced versions we create.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. User Obligations</h2>
          <p>
            You agree to use the service only for lawful purposes and in accordance with these
            Terms. You must not upload inappropriate, offensive, or copyrighted content that you
            don't own.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Payment Terms</h2>
          <p>
            Purchases are final and non-refundable, except as required by law or as explicitly
            stated in our refund policy. We offer a 7-day satisfaction guarantee.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. Limitation of Liability</h2>
          <p>
            AI Profile Photo Maker shall not be liable for any indirect, incidental, special,
            consequential, or punitive damages resulting from your use of the service.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">6. Contact Information</h2>
          <p>
            For any questions regarding these Terms of Service, please contact us at
            legal&#64;aiprofilephotomaker.com.
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
