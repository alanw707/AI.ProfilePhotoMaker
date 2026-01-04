import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-ip-dmca',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">IP / DMCA Policy</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 19, 2025</p>

          <h2>1. Copyright and IP</h2>
          <p>
            You may only upload content that you own or have permission to use. We respect the
            intellectual property rights of others and expect users to do the same.
          </p>

          <h2>2. Takedown Requests</h2>
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

          <h2>3. Counter-Notice</h2>
          <p>
            If you believe a takedown was submitted in error, you may send a counter-notice with
            sufficient details and a statement consenting to jurisdiction where applicable.
          </p>

          <h2>4. Contact</h2>
          <p>Send IP/DMCA requests to support&#64;aiprofilephotomaker.com.</p>
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
