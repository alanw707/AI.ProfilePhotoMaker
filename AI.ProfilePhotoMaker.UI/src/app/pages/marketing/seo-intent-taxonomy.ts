import { SignupCtaType } from '../../services/intent-tracking.service';

const SEO_PAGE_INTENT_ENTRIES: [string, SignupCtaType][] = [
  ['how-it-works', 'pricing'],
  ['examples', 'headshots'],
  ['corporate-headshot', 'headshots'],
  ['ai-headshot-generator', 'headshots'],
  ['linkedin-headshots', 'headshots'],
  ['professional-headshots', 'headshots'],
  ['headshots-for-job-search', 'headshots'],
  ['realtor-headshots', 'headshots'],
  ['lawyer-headshots', 'headshots'],
  ['doctor-headshots', 'headshots'],
  ['compare/aragon-ai', 'pricing'],
  ['compare/headshotpro', 'pricing'],
  ['features', 'pricing'],
  ['dating-app-headshots', 'headshots'],
  ['real-estate-agent-headshots', 'headshots'],
  ['medical-professional-headshots', 'headshots'],
  ['pricing', 'pricing'],
  ['help', 'pricing'],
  ['nurse-headshots', 'headshots'],
  ['teacher-headshots', 'headshots'],
];

export const SEO_PAGE_INTENT_TAXONOMY = Object.fromEntries(SEO_PAGE_INTENT_ENTRIES);
