import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-children-privacy',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">Children's Privacy Policy</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 19, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Not for Children Under 13</h2>
          <p>
            AI Profile Photo Maker is not directed to children under 13. If you are under 13, do
            not use the service or provide personal information.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. If We Learn of Underage Use</h2>
          <p>
            If we learn that we have collected personal information from a child under 13, we will
            delete it. Parents or guardians can contact us to request removal.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. Contact</h2>
          <p>
            For children's privacy questions, contact us at support&#64;aiprofilephotomaker.com.
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
export class ChildrenPrivacyComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle("Children's Privacy - AI Profile Photo Maker");
    this.meta.updateTag({
      name: 'description',
      content: "Children's privacy policy for AI Profile Photo Maker.",
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
