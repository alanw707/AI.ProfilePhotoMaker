import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { SeoPageComponent } from './seo-page.component';
import { AnalyticsService } from '../../../services/analytics.service';
import { IntentTrackingService } from '../../../services/intent-tracking.service';
import { SeoPageContent } from '../seo-pages.types';

describe('SeoPageComponent intent routing', () => {
  const utmSourceKey = 'utm_source';
  const analyticsService = jasmine.createSpyObj('AnalyticsService', ['trackEvent']);
  const intentTrackingService = jasmine.createSpyObj('IntentTrackingService', ['storeIntent']);

  const mockPage: SeoPageContent = {
    slug: 'linkedin-headshots',
    title: 'LinkedIn Headshots',
    description: 'desc',
    keywords: 'k1,k2',
    h1: 'LinkedIn Headshots',
    ctaIntent: 'headshots',
    hero: {
      headline: 'hero',
      subhead: 'subhead',
      ctaLabel: 'Start',
      ctaHref: '/pricing',
    },
    sections: [],
    cta: {
      title: 'Footer',
      description: 'Footer desc',
      label: 'Start now',
      href: '/pricing',
    },
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeoPageComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            data: of({ seoPage: mockPage }),
            queryParamMap: of(
              convertToParamMap({
                [utmSourceKey]: 'google',
              })
            ),
          },
        },
        { provide: AnalyticsService, useValue: analyticsService },
        { provide: IntentTrackingService, useValue: intentTrackingService },
      ],
    }).compileComponents();
  });

  it('routes pricing CTA to register when intent is headshots', () => {
    const fixture = TestBed.createComponent(SeoPageComponent);
    const component = fixture.componentInstance;
    component.page = mockPage;

    expect(component.getRouterLinkForHref('/pricing', 'headshots')).toBe('/auth/register');
  });

  it('does not put signup intent in URL query params for register route', () => {
    // Intent is stored in sessionStorage via onCtaClick, never in the URL
    // (prevents Googlebot from crawling /auth/register?intent={...} URLs)
    const fixture = TestBed.createComponent(SeoPageComponent);
    const component = fixture.componentInstance;
    component.page = mockPage;
    component.utmParams = { [utmSourceKey]: 'google' };

    const queryParams = component.getQueryParamsForHref('/pricing', 'headshots');

    expect(queryParams?.['utm_source']).toBe('google');
    expect(queryParams?.['intent']).toBeUndefined();
  });

  it('stores intent when CTA click leads to register', () => {
    const fixture = TestBed.createComponent(SeoPageComponent);
    const component = fixture.componentInstance;
    component.page = mockPage;

    component.onCtaClick('hero_primary', 'Start', '/pricing', 'headshots');

    expect(intentTrackingService.storeIntent).toHaveBeenCalled();
    expect(analyticsService.trackEvent).toHaveBeenCalled();
  });

  it('uses taxonomy fallback when page-level cta intent is not provided', () => {
    const fixture = TestBed.createComponent(SeoPageComponent);
    const component = fixture.componentInstance;
    component.page = {
      ...mockPage,
      slug: 'compare/aragon-ai',
      ctaIntent: undefined,
    };

    expect(component.getRouterLinkForHref('/pricing')).toBe('/pricing');
  });
});
