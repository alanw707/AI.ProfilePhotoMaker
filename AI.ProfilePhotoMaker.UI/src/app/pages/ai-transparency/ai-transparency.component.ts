import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-ai-transparency',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">AI Transparency</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 19, 2025</p>

          <h2>1. How AI Is Used</h2>
          <p>
            AI Profile Photo Maker uses third-party AI services to train custom models on your
            photos and generate profile images. We use AI to enhance and stylize images based on
            your selections.
          </p>

          <h2>2. Limitations</h2>
          <p>
            AI outputs may contain inaccuracies, artifacts, or unexpected results. AI-generated
            images are not guaranteed to be accurate, authentic, or suitable for any particular
            purpose. Review outputs before use.
          </p>

          <h2>3. Responsible Use</h2>
          <p>
            Do not use AI-generated outputs for identity verification, government IDs, or
            impersonation. You are responsible for ensuring your use complies with applicable laws
            and platform policies where you publish images.
          </p>

          <h2>4. Data and Consent</h2>
          <p>
            We process your photos only at your direction to deliver the service. For details on
            data handling and retention, see our
            <a routerLink="/legal/privacy">Privacy Policy</a>.
          </p>

          <h2>5. Contact</h2>
          <p>Questions about AI usage? Contact us at support&#64;aiprofilephotomaker.com.</p>
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
export class AiTransparencyComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('AI Transparency - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'How AI Profile Photo Maker uses AI and what to expect from AI-generated outputs.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
