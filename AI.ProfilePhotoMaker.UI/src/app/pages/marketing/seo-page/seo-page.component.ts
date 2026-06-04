import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, ParamMap, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { AnalyticsService } from '../../../services/analytics.service';
import {
  IntentTrackingService,
  SignupCtaType,
  SignupIntent,
} from '../../../services/intent-tracking.service';
import { SEO_PAGE_INTENT_TAXONOMY } from '../seo-intent-taxonomy';
import { AnimateOnScrollDirective } from '../../../shared/directives/animate-on-scroll.directive';
import { MarketingFooterComponent } from '../../../shared/marketing-footer/marketing-footer.component';
import { MarketingHeaderComponent } from '../../../shared/marketing-header/marketing-header.component';
import {
  SeoBulletsSection,
  SeoCardsSection,
  SeoComparisonSection,
  SeoFaq,
  SeoFaqSection,
  SeoPageContent,
  SeoSection,
  SeoShowcaseSection,
  SeoStepsSection,
  SeoTestimonialsSection,
} from '../seo-pages.data';

@Component({
  selector: 'app-seo-page',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MarketingHeaderComponent,
    MarketingFooterComponent,
    AnimateOnScrollDirective,
  ],
  templateUrl: './seo-page.component.html',
  styleUrls: ['./seo-page.component.sass'],
})
export class SeoPageComponent implements OnInit, OnDestroy {
  page?: SeoPageContent;
  utmParams: Record<string, string> = {};

  private readonly structuredDataId = 'seo-structured-data';
  private readonly faqStructuredDataId = 'seo-faq-structured-data';

  private readonly _meta = inject(Meta);
  private readonly _title = inject(Title);
  private readonly _route = inject(ActivatedRoute);
  private readonly _document = inject(DOCUMENT);
  private readonly _analytics = inject(AnalyticsService);
  private readonly _intentTracking = inject(IntentTrackingService);
  private readonly _subscriptions = new Subscription();

  ngOnInit(): void {
    this._subscriptions.add(
      this._route.data.subscribe(data => {
        this.page = data['seoPage'] as SeoPageContent | undefined;
        if (this.page) {
          this.applySeo(this.page);
          this.trackVerticalPackPageView(this.page);
        }
      })
    );
    this._subscriptions.add(
      this._route.queryParamMap.subscribe(params => {
        this.utmParams = this.extractUtmParams(params);
      })
    );
  }

  ngOnDestroy(): void {
    this._subscriptions.unsubscribe();
    this.removeStructuredData(this.structuredDataId);
    this.removeStructuredData(this.faqStructuredDataId);
  }

  private trackVerticalPackPageView(page: SeoPageContent): void {
    const verticalUseCase = this.getVerticalUseCaseForPage(page.slug);
    if (!verticalUseCase) {
      return;
    }

    this._analytics.trackEvent('vertical_pack_page_view', {
      page: page.slug,
      useCaseCode: verticalUseCase,
      ...this.utmParams,
    });
  }

  private getVerticalUseCaseForPage(slug: string): string | null {
    const map: Record<string, string> = {
      'linkedin-executive-profile-photo': 'linkedin_executive',
      'realtor-profile-photo-pack': 'realtor',
      'founder-press-kit-photo-pack': 'founder_press_kit',
    };
    return map[slug] ?? null;
  }

  onCtaClick(position: string, label: string, href?: string, ctaIntent?: SignupCtaType): void {
    if (!this.page) {
      return;
    }
    const resolvedIntent = this.resolveCtaIntent(href, ctaIntent);
    const ctaSignupIntent = this.buildSignupIntent(resolvedIntent);
    if (ctaSignupIntent && this.getRouterLinkForHref(href, ctaIntent) === '/auth/register') {
      this._intentTracking.storeIntent(ctaSignupIntent);
    }

    this._analytics.trackEvent('seo_cta_click', {
      page: this.page.slug,
      position,
      label,
      destination: href ?? '',
      ctaIntent: resolvedIntent ?? 'none',
      ...this.utmParams,
    });
  }

  onHeroImageError(event: Event, fallbackSrc?: string): void {
    const image = event.target as HTMLImageElement | null;
    if (!image || !fallbackSrc || image.src.endsWith(fallbackSrc)) {
      return;
    }
    image.src = fallbackSrc;
  }

  getQueryParamsForHref(href?: string, _ctaIntent?: SignupCtaType): Record<string, string> | null {
    if (!href) {
      return null;
    }

    // Intent is stored in sessionStorage via onCtaClick — never put it in the URL
    // (avoids Googlebot crawling /auth/register?intent={...} URLs). Existing product
    // query params such as /app/enhance?useCase=realtor must still survive routerLink.
    const queryParams: Record<string, string> = {
      ...this.extractQueryParamsFromHref(href),
      ...this.utmParams,
    };

    return Object.keys(queryParams).length > 0 ? queryParams : null;
  }

  getRouterLinkForHref(href?: string, ctaIntent?: SignupCtaType): string | null {
    if (!href) {
      return null;
    }
    const resolvedIntent = this.resolveCtaIntent(href, ctaIntent);
    if (this.isPricingLink(href) && resolvedIntent && resolvedIntent !== 'pricing') {
      return '/auth/register';
    }
    if (this.isReviewsLink(href)) {
      return '/';
    }

    return href.split(/[?#]/, 1)[0] || '/';
  }

  private extractQueryParamsFromHref(href: string): Record<string, string> {
    const query = href.split('#', 1)[0].split('?')[1];
    if (!query) {
      return {};
    }

    return Object.fromEntries(new URLSearchParams(query).entries());
  }

  getFragmentForHref(href?: string): string | undefined {
    return href && this.isReviewsLink(href) ? 'testimonials' : undefined;
  }

  private applySeo(page: SeoPageContent): void {
    const canonicalUrl = this.buildCanonicalUrl(page.slug);

    this._title.setTitle(page.title);

    this._meta.updateTag({ name: 'description', content: page.description });
    this._meta.updateTag({ name: 'keywords', content: page.keywords });
    this._meta.updateTag({ name: 'robots', content: 'index, follow' });

    this._meta.updateTag({ property: 'og:title', content: page.title });
    this._meta.updateTag({ property: 'og:description', content: page.description });
    this._meta.updateTag({ property: 'og:type', content: 'website' });
    this._meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this._meta.updateTag({
      property: 'og:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({
      property: 'og:image:secure_url',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({ property: 'og:image:type', content: 'image/png' });
    this._meta.updateTag({ property: 'og:site_name', content: 'AI Profile Photo Maker' });

    this._meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this._meta.updateTag({ name: 'twitter:title', content: page.title });
    this._meta.updateTag({ name: 'twitter:description', content: page.description });
    this._meta.updateTag({
      name: 'twitter:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({
      name: 'twitter:image:alt',
      content: 'AI Profile Photo Maker preview card',
    });
    this._meta.updateTag({ name: 'twitter:url', content: canonicalUrl });
    this._meta.updateTag({ name: 'twitter:creator', content: '@aiprofilephoto' });

    this.setCanonicalUrl(canonicalUrl);

    const structuredData = {
      '@context': 'https://schema.org',
      '@type': 'WebPage',
      name: page.h1,
      description: page.description,
      url: canonicalUrl,
      isPartOf: {
        '@type': 'WebSite',
        name: 'AI Profile Photo Maker',
        url: 'https://aiprofilephotomaker.com/',
      },
      publisher: {
        '@type': 'Organization',
        name: 'AI Profile Photo Maker',
        url: 'https://aiprofilephotomaker.com/',
        logo: 'https://aiprofilephotomaker.com/Logo.PNG',
      },
    };

    this.injectStructuredData(this.structuredDataId, structuredData);

    const faqItems = this.collectFaqItems(page);
    if (faqItems.length > 0) {
      const faqData = {
        '@context': 'https://schema.org',
        '@type': 'FAQPage',
        mainEntity: faqItems.map(faq => ({
          '@type': 'Question',
          name: faq.question,
          acceptedAnswer: {
            '@type': 'Answer',
            text: faq.answer,
          },
        })),
      };
      this.injectStructuredData(this.faqStructuredDataId, faqData);
    } else {
      this.removeStructuredData(this.faqStructuredDataId);
    }
  }

  private buildCanonicalUrl(slug: string): string {
    if (!slug || slug === '/') {
      return 'https://aiprofilephotomaker.com/';
    }
    return `https://aiprofilephotomaker.com/${slug}`;
  }

  private setCanonicalUrl(url: string): void {
    let canonical = this._document.querySelector("link[rel='canonical']") as HTMLLinkElement | null;
    if (!canonical) {
      canonical = this._document.createElement('link');
      canonical.setAttribute('rel', 'canonical');
      this._document.head.appendChild(canonical);
    }
    canonical.setAttribute('href', url);
  }

  private injectStructuredData(id: string, data: object): void {
    this.removeStructuredData(id);

    const script = this._document.createElement('script');
    script.id = id;
    script.type = 'application/ld+json';
    script.text = JSON.stringify(data);
    this._document.head.appendChild(script);
  }

  private removeStructuredData(id: string): void {
    const existing = this._document.getElementById(id);
    if (existing) {
      existing.remove();
    }
  }

  private collectFaqItems(page: SeoPageContent): SeoFaq[] {
    return page.sections
      .filter(section => section.type === 'faq')
      .flatMap(section => (section.type === 'faq' ? section.items : []));
  }

  isStepsSection(section: SeoSection): section is SeoStepsSection {
    return section.type === 'steps';
  }

  isCardsSection(section: SeoSection): section is SeoCardsSection {
    return section.type === 'cards';
  }

  isBulletsSection(section: SeoSection): section is SeoBulletsSection {
    return section.type === 'bullets';
  }

  isShowcaseSection(section: SeoSection): section is SeoShowcaseSection {
    return section.type === 'showcase';
  }

  isTestimonialsSection(section: SeoSection): section is SeoTestimonialsSection {
    return section.type === 'testimonials';
  }

  isComparisonSection(section: SeoSection): section is SeoComparisonSection {
    return section.type === 'comparison';
  }

  isFaqSection(section: SeoSection): section is SeoFaqSection {
    return section.type === 'faq';
  }

  // TrackBy functions for *ngFor performance optimization
  trackByIndex(index: number): number {
    return index;
  }

  trackByLabel(index: number, item: { label: string }): string {
    return item.label;
  }

  trackByTitle(index: number, item: { title: string }): string {
    return item.title;
  }

  trackByQuestion(index: number, item: { question: string }): string {
    return item.question;
  }

  trackByHref(index: number, item: { href: string }): string {
    return item.href;
  }

  private extractUtmParams(paramMap: ParamMap): Record<string, string> {
    const utmKeys = ['utm_source', 'utm_medium', 'utm_campaign', 'utm_term', 'utm_content'];
    const params: Record<string, string> = {};
    utmKeys.forEach(key => {
      const value = paramMap.get(key);
      if (value) {
        params[key] = value;
      }
    });
    return params;
  }

  private isPricingLink(href: string): boolean {
    return href.startsWith('/pricing');
  }

  private isReviewsLink(href: string): boolean {
    return href.startsWith('/reviews');
  }

  private resolveCtaIntent(href?: string, ctaIntent?: SignupCtaType): SignupCtaType | null {
    if (!href || !this.page) {
      return null;
    }

    if (ctaIntent) {
      return ctaIntent;
    }

    if (!this.isPricingLink(href)) {
      return null;
    }

    if (this.page.ctaIntent) {
      return this.page.ctaIntent;
    }

    return SEO_PAGE_INTENT_TAXONOMY[this.page.slug] ?? 'pricing';
  }

  private buildSignupIntent(ctaType: SignupCtaType | null): SignupIntent | null {
    if (!ctaType || !this.page) {
      return null;
    }

    return {
      sourcePage: this.page.slug,
      ctaType,
      timestamp: Date.now(),
    };
  }
}
