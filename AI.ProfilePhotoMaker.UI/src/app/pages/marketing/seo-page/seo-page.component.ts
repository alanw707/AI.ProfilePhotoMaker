import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, ParamMap, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { AnalyticsService } from '../../../services/analytics.service';
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
  imports: [CommonModule, RouterModule, MarketingHeaderComponent, MarketingFooterComponent],
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
  private readonly _subscriptions = new Subscription();

  ngOnInit(): void {
    this._subscriptions.add(
      this._route.data.subscribe(data => {
        this.page = data['seoPage'] as SeoPageContent | undefined;
        if (this.page) {
          this.applySeo(this.page);
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

  onCtaClick(position: string, label: string, href?: string): void {
    if (!this.page) {
      return;
    }
    this._analytics.trackEvent('seo_cta_click', {
      page: this.page.slug,
      position,
      label,
      destination: href ?? '',
      ...this.utmParams,
    });
  }

  getQueryParamsForHref(href?: string): Record<string, string> | null {
    if (!href) {
      return null;
    }
    return this.isPricingLink(href) && Object.keys(this.utmParams).length > 0
      ? this.utmParams
      : null;
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
      content: 'https://aiprofilephotomaker.com/assets/og-image.svg',
    });
    this._meta.updateTag({ property: 'og:site_name', content: 'AI Profile Photo Maker' });

    this._meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this._meta.updateTag({ name: 'twitter:title', content: page.title });
    this._meta.updateTag({ name: 'twitter:description', content: page.description });
    this._meta.updateTag({
      name: 'twitter:image',
      content: 'https://aiprofilephotomaker.com/assets/twitter-card.svg',
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
}
