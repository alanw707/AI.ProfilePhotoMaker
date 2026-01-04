import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

@Component({
  selector: 'app-children-privacy',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingFooterComponent],
  template: `
    <div class="marketing-legal">
      <div class="legal-container">
        <h1 class="legal-title">Children's Privacy Policy</h1>

        <div class="legal-card legal-prose">
          <p class="legal-updated">Last updated: December 19, 2025</p>

          <h2>1. Not for Children Under 13</h2>
          <p>
            AI Profile Photo Maker is not directed to children under 13. If you are under 13, do not
            use the service or provide personal information.
          </p>

          <h2>2. If We Learn of Underage Use</h2>
          <p>
            If we learn that we have collected personal information from a child under 13, we will
            delete it. Parents or guardians can contact us to request removal.
          </p>

          <h2>3. Contact</h2>
          <p>
            For children's privacy questions, contact us at support&#64;aiprofilephotomaker.com.
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
