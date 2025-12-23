import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-retention-policy',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Data Retention Policy</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 23, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Scope</h2>
          <p>
            This policy explains how long AI Profile Photo Maker retains data related to your
            account, photos, and AI outputs. Retention periods are maximums; we may delete data
            sooner if you request it or if data is no longer needed.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. Retention Schedule</h2>
          <ul>
            <li><strong>Input photos:</strong> retained up to 30 days after upload.</li>
            <li><strong>Generated images:</strong> retained up to 30 days after creation.</li>
            <li>
              <strong>AI models and training files:</strong> retained while your headshot images
              remain available, then deleted after those images expire or if you delete the model
              or your account.
            </li>
            <li>
              <strong>Account and billing records:</strong> retained as required by law and
              operational needs.
            </li>
          </ul>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. User Controls</h2>
          <p>
            You can delete photos, models, or your entire account in Settings. If you request a
            deletion, we remove data from active systems according to this policy.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Backups and Logs</h2>
          <p>
            Backups may retain data for a limited period. If cloud storage soft delete is enabled,
            deleted blobs may be retained for up to 7 days. Operational logs are retained to
            maintain service reliability and security.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. Third-Party Processing</h2>
          <p>
            We use third-party AI providers for model training and image generation. We align our
            deletion actions with these providers where supported and disclose them on our
            <a routerLink="/legal/subprocessors">Subprocessors page</a>.
          </p>
          <p>
            Third-party providers may retain inputs or outputs as needed to provide their services
            or meet legal and security obligations. Their retention practices are governed by their
            own policies.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">6. Contact Us</h2>
          <p>
            If you have questions about retention, contact us at
            privacy&#64;aiprofilephotomaker.com.
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
export class RetentionPolicyComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Data Retention Policy - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content:
        'Data Retention Policy for AI Profile Photo Maker, including photo and model retention periods.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
