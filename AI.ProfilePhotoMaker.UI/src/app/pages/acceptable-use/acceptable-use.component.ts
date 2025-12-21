import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-acceptable-use',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Acceptable Use Policy</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 19, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Summary</h2>
          <p>
            You may use the service only for lawful purposes and in compliance with these rules.
            You are responsible for the content you upload and generate.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. Prohibited Content</h2>
          <ul>
            <li>Content you do not own or lack rights to use.</li>
            <li>Images that impersonate another person or misrepresent identity.</li>
            <li>Illegal, abusive, or hateful content.</li>
            <li>Explicit sexual content or non-consensual imagery.</li>
            <li>Content that violates privacy or confidentiality.</li>
          </ul>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. Prohibited Behavior</h2>
          <ul>
            <li>Attempting to bypass security or access restrictions.</li>
            <li>Using the service to generate content for harassment or deception.</li>
            <li>Automated scraping or excessive usage that harms service stability.</li>
          </ul>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Enforcement</h2>
          <p>
            We may suspend or terminate accounts that violate this policy or our Terms of Service.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">5. Contact</h2>
          <p>
            Questions or concerns? Contact us at legal&#64;aiprofilephotomaker.com.
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
export class AcceptableUseComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('Acceptable Use Policy - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'Acceptable use rules for AI Profile Photo Maker.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
