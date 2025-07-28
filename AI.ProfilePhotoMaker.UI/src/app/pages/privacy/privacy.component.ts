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
          <p class="text-gray-600 mb-6">Last updated: January 28, 2024</p>
          
          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Information We Collect</h2>
          <p>We collect information you provide directly to us, such as when you create an account, upload photos, or contact us for support.</p>
          
          <h2 class="text-2xl font-semibold mt-8 mb-4">2. How We Use Your Information</h2>
          <p>We use the information we collect to provide, maintain, and improve our services, process transactions, and communicate with you.</p>
          
          <h2 class="text-2xl font-semibold mt-8 mb-4">3. Data Security</h2>
          <p>We implement appropriate technical and organizational measures to protect your personal information against unauthorized access, alteration, disclosure, or destruction.</p>
          
          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Photo Processing and Deletion</h2>
          <p>All uploaded photos are processed securely and automatically deleted from our servers within 24 hours after processing. We do not store or share your photos with third parties.</p>
          
          <h2 class="text-2xl font-semibold mt-8 mb-4">5. Contact Us</h2>
          <p>If you have any questions about this Privacy Policy, please contact us at privacy@aiprofilephotomaker.com.</p>
        </div>
        
        <div class="mt-8 text-center">
          <a routerLink="/" class="text-primary-600 hover:text-primary-700 font-medium">← Back to Home</a>
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
        'Privacy Policy for AI Profile Photo Maker. Learn how we protect your data and photos.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
