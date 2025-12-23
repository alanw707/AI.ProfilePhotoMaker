import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-privacy',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Privacy Policy</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 20, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Scope</h2>
          <p>
            This Privacy Policy explains how AI Profile Photo Maker (we, us, our) collects, uses,
            shares, and retains personal information when you use our website and services.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. Information We Collect</h2>
          <ul>
            <li>
              <strong>Account information:</strong> email address, name, and profile details you
              provide.
            </li>
            <li>
              <strong>Photos and outputs:</strong> uploaded photos, generated images, and enhancement
              results.
            </li>
            <li>
              <strong>Usage and activity:</strong> credits usage, feature activity, and timestamps.
            </li>
            <li>
              <strong>Device and technical data:</strong> IP address, browser type, and diagnostics.
            </li>
            <li>
              <strong>Payments:</strong> payment method details are processed by Stripe; we receive
              limited transaction metadata.
            </li>
            <li>
              <strong>Support communications:</strong> messages you send to us.
            </li>
          </ul>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. How We Use Information</h2>
          <p>
            We use information to provide the service, authenticate users, process payments, manage
            credits, maintain security and reliability, comply with legal obligations, and improve
            the product.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Legal Bases (EEA/UK)</h2>
          <p>
            If you are in the EEA/UK, we process personal data based on contract performance,
            consent (where required), legitimate interests, and legal obligations.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. AI Processing and Biometric Information</h2>
          <p>
            We process photos to create AI-generated profile images. In some jurisdictions, facial
            images may be treated as biometric information. We process this data to provide the
            service at your direction and, where required, based on consent.
          </p>
          <p>
            See our
            <a routerLink="/legal/biometric-consent">Biometric Consent & Retention Notice</a>
            for details.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">6. Sharing and Third Parties</h2>
          <p>We share data with service providers that help us operate the service, including:</p>
          <ul>
            <li>Replicate for model training and image generation.</li>
            <li>OpenAI for select photo enhancements.</li>
            <li>Stripe for payments and billing.</li>
            <li>Google for optional OAuth login.</li>
            <li>Hosting and storage providers if configured.</li>
          </ul>
          <p>
            See our
            <a routerLink="/legal/subprocessors">Subprocessors page</a>
            for the current list of providers.
          </p>
          <p>
            We do not sell personal information or share it for cross-context behavioral
            advertising.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">7. Retention</h2>
          <p>We retain data based on the service requirements and our retention policy:</p>
          <ul>
            <li>Input photos: retained up to 30 days after upload unless you delete them sooner.</li>
            <li>
              Generated images: retained up to 30 days after creation unless you delete them
              sooner.
            </li>
            <li>
              AI models and training files: retained while your model exists; deleted when you
              delete the model or your account.
            </li>
            <li>Account and billing records: retained as required by law and operations.</li>
          </ul>
          <p>
            See our
            <a routerLink="/legal/retention-policy">Data Retention Policy</a>
            for details.
          </p>
          <p>
            Third-party providers may retain inputs or outputs as needed to provide their services
            or meet legal and security obligations. Their retention practices are governed by their
            own policies.
          </p>
          <p>
            Backups may retain data for a limited period. If cloud storage soft delete is enabled,
            deleted blobs may be retained for up to 7 days.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">8. Your Rights and Choices</h2>
          <p>
            Depending on your location, you may have rights to access, correct, delete, or export
            your data. Data export currently includes account metadata and usage records (not photo
            files).
          </p>
          <p>
            You can delete photos, models, or your account in Settings. For requests, contact us at
            privacy&#64;aiprofilephotomaker.com.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">9. International Transfers</h2>
          <p>
            We may process data in countries where we or our service providers operate. Where
            required, we use appropriate safeguards for international transfers.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">10. Security</h2>
          <p>
            We use technical and organizational measures to protect data. No system can be
            guaranteed 100% secure.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">11. Children&apos;s Privacy</h2>
          <p>
            Our service is not directed to children under 13. If we learn that we collected data
            from a child under 13, we will delete it.
          </p>
          <p>
            See our
            <a routerLink="/legal/children-privacy">Children&apos;s Privacy Policy</a>
            for more details.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">12. Changes</h2>
          <p>
            We may update this policy. If changes are material, we will post notice in the app or
            on our website.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">13. Contact Us</h2>
          <p>
            If you have any questions about this Privacy Policy, please contact us at
            privacy&#64;aiprofilephotomaker.com.
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
export class PrivacyComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Privacy Policy - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content:
        'Privacy Policy for AI Profile Photo Maker, including data use, retention, and rights.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
