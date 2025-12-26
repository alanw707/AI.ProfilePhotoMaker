import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-biometric-consent',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Biometric Consent & Retention Notice</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 20, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Biometric Data Notice</h2>
          <p>
            When you upload photos, we may process biometric data such as face geometry to train
            your model and generate AI profile images. This is done only at your direction to
            provide the service.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. Purpose</h2>
          <p>
            We use biometric data to (a) validate photo quality, (b) train your personalized model,
            and (c) generate AI-enhanced images.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. Retention Schedule</h2>
          <ul>
            <li>Input photos: retained up to 30 days after upload unless you delete them sooner.</li>
            <li>
              Generated images: retained up to 30 days after creation unless you delete them sooner.
            </li>
            <li>
              AI models and training artifacts: retained while your model exists and deleted when
              you delete the model or your account.
            </li>
          </ul>
          <p>
            Third-party processors involved in training or generation may retain data as needed to
            provide their services or meet legal and security obligations. Their retention
            practices are governed by their own policies.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Consent</h2>
          <p>
            By checking the consent box in the upload flow, you provide written consent for us to
            collect and process your biometric data for the purposes described above.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. Withdrawal</h2>
          <p>
            You can withdraw consent by deleting your photos, model, or account in Settings. For
            assistance, contact us at support&#64;aiprofilephotomaker.com.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">6. Contact</h2>
          <p>
            Questions about biometric data? Contact us at support&#64;aiprofilephotomaker.com.
          </p>
        </div>

        <div class="mt-8 text-center">
          <a routerLink="/legal/privacy" class="text-primary-600 hover:text-primary-700 font-medium"
            >← Back to Privacy Policy</a
          >
        </div>
      </div>
    </div>
  `,
  styles: [],
})
export class BiometricConsentComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Biometric Consent - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'Biometric consent and retention notice for AI Profile Photo Maker.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
