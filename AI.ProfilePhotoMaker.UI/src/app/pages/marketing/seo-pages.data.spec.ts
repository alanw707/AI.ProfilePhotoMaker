import { seoPages } from './seo-pages.data';

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
});
