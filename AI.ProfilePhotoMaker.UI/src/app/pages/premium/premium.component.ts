import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { MarketingHeaderComponent } from '../../shared/marketing-header/marketing-header.component';
import { CreditPackagesComponent } from '../../components/credit-packages/credit-packages.component';
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
              <h1>Profile Photo Packages</h1>
              <p class="hero-subtitle">
                Get your best professional profile photo from one upload: score it, generate focused
                candidates, choose the best shot, refine it, and download platform-ready exports.
              </p>

              <div class="features-grid">
                <div class="feature-item">
                  <div class="feature-icon">🎯</div>
                  <h3>Profile Photo Score</h3>
                  <p>
                    Understand what is working before you buy, then see how much your final photo
                    improves.
                  </p>
                </div>
                <div class="feature-item">
                  <div class="feature-icon">⚡</div>
                  <h3>Best Shot Selector</h3>
                  <p>
                    Generate a focused candidate set and get guidance on which image is strongest.
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
                  <h3>Platform Export Kit</h3>
                  <p>
                    Download user-selected crops for LinkedIn, Gmail, Slack, GitHub, resumes, and
                    websites.
                  </p>
                </div>
              </div>
            </div>
          </section>

          <!-- Outcome Packages Section -->
          <section id="packages-section" class="packages-section">
            <div class="content-container">
              <p class="text-center text-sm text-gray-600 mb-6">
                14-day satisfaction guarantee —
                <a routerLink="/legal/refund-policy" class="text-primary-600 hover:underline"
                  >see Refund Policy</a
                >
              </p>
              <app-credit-packages (packagePurchased)="onCreditPackagePurchased()">
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
                <p>Get a ready-to-use professional profile photo package in four steps</p>
              </div>

              <div class="steps-grid">
                <div class="step-item">
                  <div class="step-number">1</div>
                  <div class="step-content">
                    <h3>Upload One Photo</h3>
                    <p>Start with one clear photo or an existing professional headshot.</p>
                  </div>
                </div>
                <div class="step-item">
                  <div class="step-number">2</div>
                  <div class="step-content">
                    <h3>Score and Choose Role</h3>
                    <p>See profile-photo readiness and pick the role you want to optimize for.</p>
                  </div>
                </div>
                <div class="step-item">
                  <div class="step-number">3</div>
                  <div class="step-content">
                    <h3>Generate Candidates</h3>
                    <p>Create a focused set of candidates and pick the best shot.</p>
                  </div>
                </div>
                <div class="step-item">
                  <div class="step-number">4</div>
                  <div class="step-content">
                    <h3>Download Package</h3>
                    <p>
                      Choose platform exports and download a ready-to-use profile photo package.
                    </p>
                  </div>
                </div>
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
    private router: Router,
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
  }

  onCreditPackagePurchased() {
    this.notificationService.success(
      'Package Unlocked!',
      'Your profile photo package is ready. Start from the workspace to score, generate, and export your best shot.'
    );

    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    const outcomePackage = this.route.snapshot.queryParamMap.get('outcomePackage');
    if (returnUrl?.startsWith('/app/enhance') && outcomePackage) {
      const [path, query = ''] = returnUrl.split('?');
      const params = new URLSearchParams(query);
      params.set('upgraded', outcomePackage);
      this.router.navigate([path], { queryParams: Object.fromEntries(params.entries()) });
      return;
    }

    // Redirect to Photo Workspace to start the workflow
    this.router.navigate(['/app/enhance']);
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
        'Compare AI headshot pricing plans. Choose the best package for instant LinkedIn-ready photos, optional advanced styles, and high-quality professional headshots.',
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
        'Choose a plan for instant professional AI headshots, premium styles, and high-resolution downloads.',
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
      content: 'Compare AI headshot pricing plans with instant generation and premium styles.',
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
        'AI headshot pricing plans with instant generation and professional-quality downloads.',
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
