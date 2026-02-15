import type { SeoPageContent } from './seo-pages.types';
import { seoPagesPart1a } from './seo-pages.records.part1a.data';
import { seoPagesPart1b } from './seo-pages.records.part1b.data';
import { seoPagesPart1c } from './seo-pages.records.part1c.data';
import { seoPagesPart2 } from './seo-pages.records.part2.data';

export const seoPages: Record<string, SeoPageContent> = {
  ...seoPagesPart1a,
  ...seoPagesPart1b,
  ...seoPagesPart1c,
  ...seoPagesPart2,
};
