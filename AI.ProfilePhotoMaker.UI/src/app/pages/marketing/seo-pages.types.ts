export interface SeoLink {
  label: string;
  href: string;
}

export interface SeoHighlight {
  value: string;
  label: string;
}

export interface SeoHero {
  eyebrow?: string;
  headline: string;
  subhead: string;
  ctaLabel: string;
  ctaHref: string;
  secondaryCtaLabel?: string;
  secondaryCtaHref?: string;
  imageSrc?: string;
  imageAlt?: string;
  imageFallbackSrc?: string;
}

export interface SeoCta {
  title: string;
  description: string;
  label: string;
  href: string;
}

export interface SeoSectionBase {
  type: string;
  title: string;
  intro?: string;
}

export interface SeoStep {
  title: string;
  description: string;
}

export interface SeoCard {
  title: string;
  description: string;
}

export interface SeoShowcaseItem extends SeoCard {
  beforeImage?: string;
  afterImage?: string;
  beforeAlt?: string;
  afterAlt?: string;
}

export interface SeoTestimonial {
  quote: string;
  name: string;
  role: string;
}

export interface SeoFaq {
  question: string;
  answer: string;
}

export interface SeoComparisonRow {
  label: string;
  values: string[];
}

export type SeoStepsSection = SeoSectionBase & { type: 'steps'; items: SeoStep[] };
export type SeoCardsSection = SeoSectionBase & { type: 'cards'; items: SeoCard[] };
export type SeoBulletsSection = SeoSectionBase & { type: 'bullets'; items: string[] };
export type SeoShowcaseSection = SeoSectionBase & { type: 'showcase'; items: SeoShowcaseItem[] };
export type SeoTestimonialsSection = SeoSectionBase & {
  type: 'testimonials';
  items: SeoTestimonial[];
};
export type SeoComparisonSection = SeoSectionBase & {
  type: 'comparison';
  columns: string[];
  rows: SeoComparisonRow[];
  note?: string;
};
export type SeoFaqSection = SeoSectionBase & { type: 'faq'; items: SeoFaq[] };

export type SeoSection =
  | SeoStepsSection
  | SeoCardsSection
  | SeoBulletsSection
  | SeoShowcaseSection
  | SeoTestimonialsSection
  | SeoComparisonSection
  | SeoFaqSection;

export interface SeoPageContent {
  slug: string;
  title: string;
  description: string;
  keywords: string;
  h1: string;
  hero: SeoHero;
  highlights?: SeoHighlight[];
  sections: SeoSection[];
  relatedLinks?: SeoLink[];
  cta: SeoCta;
}
