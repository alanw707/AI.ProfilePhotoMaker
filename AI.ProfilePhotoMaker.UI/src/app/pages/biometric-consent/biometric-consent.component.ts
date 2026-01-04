import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-biometric-consent',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">Biometric Consent & Retention Notice</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 20, 2025</p>

          <h2>1. Biometric Data Notice</h2>
          <p>
            When you upload photos, we may process biometric data such as face geometry to train
            your model and generate AI profile images. This is done only at your direction to
            provide the service.
          </p>

          <h2>2. Purpose</h2>
          <p>
            We use biometric data to (a) validate photo quality, (b) train your personalized model,
            and (c) generate AI-enhanced images.
          </p>

          <h2>3. Retention Schedule</h2>
          <ul>
            <li>
              Input photos: retained up to 30 days after upload unless you delete them sooner.
            </li>
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
            provide their services or meet legal and security obligations. Their retention practices
            are governed by their own policies.
          </p>

          <h2>4. Consent</h2>
          <p>
            By checking the consent box in the upload flow, you provide written consent for us to
            collect and process your biometric data for the purposes described above.
          </p>

          <h2>5. Withdrawal</h2>
          <p>
            You can withdraw consent by deleting your photos, model, or account in Settings. For
            assistance, contact us at support&#64;aiprofilephotomaker.com.
          </p>

          <h2>6. Contact</h2>
          <p>Questions about biometric data? Contact us at support&#64;aiprofilephotomaker.com.</p>
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
