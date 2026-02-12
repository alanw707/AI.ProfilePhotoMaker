import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { AuthService } from '../../services/auth.service';

import { MarketingHeaderComponent } from '../../shared/marketing-header/marketing-header.component';
import { CreditPackagesComponent } from '../../components/credit-packages/credit-packages.component';
import { CreditService, UserCreditStatus } from '../../services/credit.service';
import { NotificationService } from '../../services/notification.service';
import { LoggingService } from '../../services/logging.service';
import { NavigationService } from '../../services/navigation.service';

@Component({
  selector: 'app-premium',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingHeaderComponent, CreditPackagesComponent],
  template: `
    <div class="premium-page-container">
      <!-- Shared Header Navigation -->
      <app-marketing-header></app-marketing-header>

      <!-- Main Premium Content -->
      <main class="premium-main">
        <div class="premium-content">
          <!-- Hero Section -->
          <section class="hero-section">
            <div class="hero-content content-container">
              <h1>AI Headshot Pricing Plans</h1>
              <p class="hero-subtitle">
                Create stunning, personalized profile photos with our advanced AI technology. Train
                your own custom model for unlimited professional results.
              </p>

              <div class="features-grid">
                <div class="feature-item">
                  <div class="feature-icon">🎯</div>
                  <h3>Custom AI Training</h3>
                  <p>
                    Train a personalized AI model with your photos for authentic, professional
                    results
                  </p>
                </div>
                <div class="feature-item">
                  <div class="feature-icon">⚡</div>
                  <h3>Multiple Styles</h3>
                  <p>
                    Generate photos in various professional styles - corporate, creative, casual,
                    and more
                  </p>
                </div>
                <div class="feature-item">
                  <div class="feature-icon">🔒</div>
                  <h3>Privacy First</h3>
                  <p>
                    Input photos deleted after 30 days, AI headshots stored 30 days. Full data
                    control in account settings.
                  </p>
                </div>
                <div class="feature-item">
                  <div class="feature-icon">📸</div>
                  <h3>High Quality</h3>
                  <p>
                    Professional-grade images perfect for LinkedIn, resumes, and business profiles
                  </p>
                </div>
              </div>
            </div>
          </section>

          <!-- Credit Packages Section -->
          <section id="packages-section" class="packages-section">
            <div class="content-container">
              <p class="text-center text-sm text-gray-600 mb-6">
                14-day satisfaction guarantee —
                <a routerLink="/legal/refund-policy" class="text-primary-600 hover:underline"
                  >see Refund Policy</a
                >
              </p>
              <app-credit-packages (packagePurchased)="onCreditPackagePurchased($event)">
              </app-credit-packages>
            </div>
          </section>

          <!-- Divider -->
          <div class="content-container">
            <div class="section-divider"></div>
          </div>

          <!-- How It Works Section -->
          <section class="how-it-works-section">
            <div class="content-container">
              <div class="section-header">
                <h2>How It Works</h2>
                <p>Get professional AI photos in just 4 simple steps</p>
              </div>

              <div class="steps-grid">
                <div class="step-item">
                  <div class="step-number">1</div>
                  <div class="step-content">
                    <h3>Upload Photos</h3>
                    <p>Upload 10-20 high-quality selfies for the best AI training results</p>
                  </div>
                </div>
                <div class="step-item">
                  <div class="step-number">2</div>
                  <div class="step-content">
                    <h3>Select Styles</h3>
                    <p>Choose from professional, creative, casual, and formal photo styles</p>
                  </div>
                </div>
                <div class="step-item">
                  <div class="step-number">3</div>
                  <div class="step-content">
                    <h3>AI Training</h3>
                    <p>Our AI trains a custom model with your photos (15-25 minutes)</p>
                  </div>
                </div>
                <div class="step-item">
                  <div class="step-number">4</div>
                  <div class="step-content">
                    <h3>Download Photos</h3>
                    <p>Get your professional AI-generated photos ready for use</p>
                  </div>
                </div>
              </div>
            </div>
          </section>

          <!-- Already Have Credits Section -->
          <section
            class="existing-package-section"
            *ngIf="userCreditStatus && userCreditStatus.credits > 0"
          >
            <div class="content-container">
              <div class="existing-package-card">
                <div class="package-status">
                  <h3>🎯 You Have Credits Available!</h3>
                  <div class="status-details">
                    <span class="package-name">Credit Balance</span>
                    <span class="credits-remaining"
                      >{{ userCreditStatus.credits }} credits available</span
                    >
                  </div>
                </div>
                <button class="btn btn-primary" routerLink="/app/dashboard">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <rect x="3" y="3" width="7" height="9" stroke="currentColor" stroke-width="2" />
                    <rect
                      x="13"
                      y="3"
                      width="8"
                      height="5"
                      stroke="currentColor"
                      stroke-width="2"
                    />
                    <rect
                      x="13"
                      y="12"
                      width="8"
                      height="9"
                      stroke="currentColor"
                      stroke-width="2"
                    />
                    <rect
                      x="3"
                      y="16"
                      width="7"
                      height="5"
                      stroke="currentColor"
                      stroke-width="2"
                    />
                  </svg>
                  Go to Studio
                </button>
              </div>
            </div>
          </section>
        </div>
      </main>
    </div>
  `,
  styleUrls: ['./premium.component.sass'],
})
export class PremiumComponent implements OnInit, OnDestroy {
  userCreditStatus: UserCreditStatus | null = null;
  private readonly seoTagSelectors = [
    "name='description'",
    "name='keywords'",
    "name='robots'",
    "property='og:title'",
    "property='og:description'",
    "property='og:type'",
    "property='og:url'",
    "property='og:image'",
    "property='og:site_name'",
    "name='twitter:card'",
    "name='twitter:title'",
    "name='twitter:description'",
    "name='twitter:image'",
    "name='twitter:url'",
    "name='twitter:creator'",
  ];

  constructor(
    private authService: AuthService,
    private router: Router,
    private creditService: CreditService,
    private notificationService: NotificationService,
    private logging: LoggingService,
    private meta: Meta,
    private title: Title,
    private route: ActivatedRoute,
    private navigationService: NavigationService
  ) {}

  ngOnInit() {
    this.logging.debug('Premium page initialized');
    this.route.fragment.subscribe(fragment => {
      if (fragment) {
        setTimeout(() => this.navigationService.scrollToSection(fragment, 120), 200);
      }
    });
    this.setupSEO();

    // Only load credit status after a verified session to avoid anonymous 401s
    if (this.authService.isAuthenticated() && this.authService.hasVerifiedSession()) {
      this.loadCreditStatus();
    }
  }

  loadCreditStatus() {
    this.creditService.getCreditStatus().subscribe({
      next: response => {
        if (response.success) {
          this.userCreditStatus = response.data;
        }
      },
      error: error => {
        this.logging.error('Failed to load credit status', error);
        // User might not have credits yet, that's fine for this page
      },
    });
  }

  onCreditPackagePurchased(creditStatus: UserCreditStatus) {
    this.logging.debug('Credit package purchased', creditStatus);
    this.userCreditStatus = creditStatus;

    this.notificationService.success(
      'Credits Purchased!',
      'Credits added to your account! You can now use premium features like model training and styled generation.'
    );

    // Redirect to dashboard to start the workflow
    this.router.navigate(['/app/dashboard']);
  }

  ngOnDestroy(): void {
    this.seoTagSelectors.forEach(selector => this.meta.removeTag(selector));
    this.setCanonicalUrl('https://aiprofilephotomaker.com/');

    const existingScript = document.getElementById('pricing-structured-data');
    if (existingScript) {
      existingScript.remove();
    }
  }

  private setupSEO(): void {
    const canonicalUrl = 'https://aiprofilephotomaker.com/pricing';

    this.setCanonicalUrl(canonicalUrl);
    this.title.setTitle('AI Headshot Pricing - AI Profile Photo Maker');

    this.meta.updateTag({
      name: 'description',
      content:
        'Compare AI headshot pricing plans. Choose the best package for LinkedIn-ready photos, custom AI training, and high-quality professional headshots.',
    });
    this.meta.updateTag({
      name: 'keywords',
      content:
        'AI headshot pricing, AI headshot generator cost, LinkedIn headshot pricing, professional headshot plans, AI profile photo pricing',
    });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });

    this.meta.updateTag({
      property: 'og:title',
      content: 'AI Headshot Pricing Plans - AI Profile Photo Maker',
    });
    this.meta.updateTag({
      property: 'og:description',
      content:
        'Choose a plan for professional AI headshots with custom AI training, premium styles, and high-resolution downloads.',
    });
    this.meta.updateTag({ property: 'og:type', content: 'product' });
    this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this.meta.updateTag({
      property: 'og:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this.meta.updateTag({
      property: 'og:image:secure_url',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this.meta.updateTag({ property: 'og:image:type', content: 'image/png' });
    this.meta.updateTag({ property: 'og:site_name', content: 'AI Profile Photo Maker' });

    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({
      name: 'twitter:title',
      content: 'AI Headshot Pricing Plans - AI Profile Photo Maker',
    });
    this.meta.updateTag({
      name: 'twitter:description',
      content: 'Compare AI headshot pricing plans with custom model training and premium styles.',
    });
    this.meta.updateTag({
      name: 'twitter:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this.meta.updateTag({
      name: 'twitter:image:alt',
      content: 'AI Profile Photo Maker pricing preview',
    });
    this.meta.updateTag({ name: 'twitter:url', content: canonicalUrl });
    this.meta.updateTag({ name: 'twitter:creator', content: '@aiprofilephoto' });

    const structuredData = {
      '@context': 'https://schema.org',
      '@type': 'Product',
      name: 'AI Headshot Packages',
      description:
        'AI headshot pricing plans with custom model training and professional-quality downloads.',
      image: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
      url: canonicalUrl,
      brand: {
        '@type': 'Organization',
        name: 'AI Profile Photo Maker',
        url: 'https://aiprofilephotomaker.com/',
        logo: 'https://aiprofilephotomaker.com/Logo.PNG',
      },
      offers: {
        '@type': 'AggregateOffer',
        priceCurrency: 'USD',
        lowPrice: '9',
        highPrice: '39',
        offerCount: '3',
        url: canonicalUrl,
      },
    };

    const existingScript = document.getElementById('pricing-structured-data');
    if (existingScript) {
      existingScript.remove();
    }

    const script = document.createElement('script');
    script.id = 'pricing-structured-data';
    script.type = 'application/ld+json';
    script.text = JSON.stringify(structuredData);
    document.head.appendChild(script);
  }

  private setCanonicalUrl(url: string): void {
    let canonical = document.querySelector("link[rel='canonical']") as HTMLLinkElement | null;
    if (!canonical) {
      canonical = document.createElement('link');
      canonical.setAttribute('rel', 'canonical');
      document.head.appendChild(canonical);
    }
    canonical.setAttribute('href', url);
  }
}
