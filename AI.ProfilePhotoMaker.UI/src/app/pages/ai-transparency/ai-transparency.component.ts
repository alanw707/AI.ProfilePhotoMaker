import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-ai-transparency',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">AI Transparency</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 19, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. How AI Is Used</h2>
          <p>
            AI Profile Photo Maker uses third-party AI services to train custom models on your
            photos and generate profile images. We use AI to enhance and stylize images based on
            your selections.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. Limitations</h2>
          <p>
            AI outputs may contain inaccuracies, artifacts, or unexpected results. AI-generated
            images are not guaranteed to be accurate, authentic, or suitable for any particular
            purpose. Review outputs before use.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. Responsible Use</h2>
          <p>
            Do not use AI-generated outputs for identity verification, government IDs, or
            impersonation. You are responsible for ensuring your use complies with applicable laws
            and platform policies where you publish images.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Data and Consent</h2>
          <p>
            We process your photos only at your direction to deliver the service. For details on
            data handling and retention, see our
            <a routerLink="/legal/privacy">Privacy Policy</a>.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. Contact</h2>
          <p>
            Questions about AI usage? Contact us at legal&#64;aiprofilephotomaker.com.
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
