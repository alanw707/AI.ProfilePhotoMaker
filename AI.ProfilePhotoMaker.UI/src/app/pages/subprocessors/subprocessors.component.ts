import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-subprocessors',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Subprocessors</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 20, 2025</p>

          <p>
            This list covers third-party service providers that process data on our behalf. Some
            providers apply only when specific features are enabled.
          </p>

          <table>
            <thead>
              <tr>
                <th>Provider</th>
                <th>Service</th>
                <th>Purpose</th>
                <th>Data Categories</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Replicate</td>
                <td>Model training and image generation</td>
                <td>Train custom models and generate images</td>
                <td>Uploaded photos, prompts, model metadata</td>
              </tr>
              <tr>
                <td>OpenAI</td>
                <td>Photo enhancement</td>
                <td>Enhance images for select styles</td>
                <td>Uploaded photos, prompts, output images</td>
              </tr>
              <tr>
                <td>Stripe</td>
                <td>Payments</td>
                <td>Process credit package purchases</td>
                <td>Payment metadata, email, transaction IDs</td>
              </tr>
              <tr>
                <td>Google</td>
                <td>OAuth login (optional)</td>
                <td>Authenticate users who choose Google sign-in</td>
                <td>Email, name, OAuth profile ID</td>
              </tr>
              <tr>
                <td>Microsoft Azure (optional)</td>
                <td>Hosting and storage</td>
                <td>API hosting and image storage if configured</td>
                <td>Account data, images, logs</td>
              </tr>
            </tbody>
          </table>

          <p>
            Each provider may retain data as needed to provide their services or comply with legal
            and security obligations. Their retention practices are governed by their own
            policies.
          </p>

          <p>
            If local storage is used and Google sign-in is disabled, those providers do not apply.
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
export class SubprocessorsComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Subprocessors - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'Subprocessors and third-party providers used by AI Profile Photo Maker.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
