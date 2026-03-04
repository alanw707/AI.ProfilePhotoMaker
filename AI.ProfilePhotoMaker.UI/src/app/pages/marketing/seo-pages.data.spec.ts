import { seoPages } from './seo-pages.data';
import { SEO_PAGE_INTENT_TAXONOMY } from './seo-intent-taxonomy';

describe('seoPages role hero mappings', () => {
  it('maps doctor hero to medical style preview with medical alt text and local fallback', () => {
    const doctorHero = seoPages['doctor-headshots'].hero;

    expect(doctorHero.imageSrc).toContain('/style-previews/medical.jpg');
    expect(doctorHero.imageAlt?.toLowerCase()).toContain('medical');
    expect(doctorHero.imageFallbackSrc).toBe('/assets/marketing/before-after/set-3-after.png');
  });

  it('maps lawyer hero to executive style preview with legal alt text and local fallback', () => {
    const lawyerHero = seoPages['lawyer-headshots'].hero;

    expect(lawyerHero.imageSrc).toContain('/style-previews/executive.jpg');
    expect(lawyerHero.imageAlt?.toLowerCase()).toContain('attorney');
    expect(lawyerHero.imageFallbackSrc).toBe('/assets/marketing/before-after/set-2-after.png');
  });

  it('maintains explicit CTA intent taxonomy coverage for all SEO slugs', () => {
    const slugs = Object.values(seoPages).map(page => page.slug);

    expect(Object.keys(SEO_PAGE_INTENT_TAXONOMY).sort()).toEqual(slugs.sort());
    slugs.forEach(slug => {
      expect(SEO_PAGE_INTENT_TAXONOMY[slug]).toBeDefined();
    });
  });
});
