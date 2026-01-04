import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-subprocessors',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">Subprocessors</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 20, 2025</p>

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
            and security obligations. Their retention practices are governed by their own policies.
          </p>

          <p>
            If local storage is used and Google sign-in is disabled, those providers do not apply.
          </p>
        </div>

        <div class="legal-footer-link">
          <a routerLink="/legal/privacy" class="legal-link">← Back to Privacy Policy</a>
        </div>
      </div>
    </div>
    <app-marketing-footer></app-marketing-footer>
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
