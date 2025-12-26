import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-ip-dmca',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 py-12">
      <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-4xl">
        <h1 class="text-4xl font-bold text-gray-900 mb-8">IP / DMCA Policy</h1>

        <div class="bg-white rounded-lg shadow-sm p-8 prose prose-gray max-w-none">
          <p class="text-gray-600 mb-6">Last updated: December 19, 2025</p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">1. Copyright and IP</h2>
          <p>
            You may only upload content that you own or have permission to use. We respect the
            intellectual property rights of others and expect users to do the same.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">2. Takedown Requests</h2>
          <p>
            If you believe content on our service infringes your rights, please contact us with the
            following:
          </p>
          <ul>
            <li>Your contact information.</li>
            <li>A description of the copyrighted work.</li>
            <li>The URL or location of the allegedly infringing content.</li>
            <li>A statement of good-faith belief that use is not authorized.</li>
            <li>A statement under penalty of perjury that the information is accurate.</li>
          </ul>

          <h2 class="text-2xl font-semibold mt-8 mb-4">3. Counter-Notice</h2>
          <p>
            If you believe a takedown was submitted in error, you may send a counter-notice with
            sufficient details and a statement consenting to jurisdiction where applicable.
          </p>

          <h2 class="text-2xl font-semibold mt-8 mb-4">4. Contact</h2>
          <p>
            Send IP/DMCA requests to support&#64;aiprofilephotomaker.com.
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
export class IpDmcaComponent implements OnInit {
  constructor(
    private meta: Meta,
    private title: Title
  ) {}

  ngOnInit() {
    this.title.setTitle('IP / DMCA Policy - AI Profile Photo Maker');
    this.meta.updateTag({
      name: 'description',
      content: 'IP and DMCA policy for AI Profile Photo Maker.',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }
}
