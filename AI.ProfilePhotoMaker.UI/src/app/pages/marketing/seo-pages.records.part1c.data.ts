import type { SeoPageContent } from './seo-pages.types';

const STYLE_PREVIEW_BASE_URL = 'https://aipmstv16j74jubocuukg.blob.core.windows.net/style-previews';
const STYLE_PREVIEW_CACHE_VERSION = '20260110';

const buildRoleStylePreviewUrl = (styleName: 'medical' | 'executive'): string =>
  `${STYLE_PREVIEW_BASE_URL}/${styleName}.jpg?v=${STYLE_PREVIEW_CACHE_VERSION}`;

export const seoPagesPart1c: Record<string, SeoPageContent> = {
  'lawyer-headshots': {
    slug: 'lawyer-headshots',
    ctaIntent: 'headshots',
    title: 'Lawyer Headshots for Attorneys & Law Firms | AI Profile Photo Maker',
    description:
      'Premium attorney headshots that look credible and natural. Ideal for firm websites, LinkedIn, press, and speaking bios.',
    keywords:
      'lawyer headshots, attorney headshots, law firm headshots, professional attorney photo, partner headshot',
    h1: 'Attorney Headshots That Signal Credibility',
    hero: {
      eyebrow: 'Attorney headshots',
      headline: 'Attorney Headshots That Signal Credibility',
      subhead:
        'Create premium, realistic attorney headshots from your photos—designed for firm sites, press, and leadership bios.',
      ctaLabel: 'Create my attorney headshot',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
      imageSrc: buildRoleStylePreviewUrl('executive'),
      imageAlt: 'Professional attorney executive-style headshot',
      imageFallbackSrc: '/assets/marketing/before-after/set-2-after.png',
    },
    highlights: [
      { value: 'Credible', label: 'First impression' },
      { value: 'Team-ready', label: 'Firm consistency' },
      { value: 'Press-ready', label: 'Bio formats' },
    ],
    sections: [
      {
        type: 'bullets',
        title: 'What a strong attorney headshot communicates',
        items: [
          'Professional authority without looking harsh.',
          'Neutral, high-end styling that fits your practice area.',
          'Consistent results across partners and associates.',
          'Bio-ready images for websites, media, and events.',
        ],
      },
      {
        type: 'showcase',
        title: 'Before and after',
        intro:
          'Professional lighting and a clean background make your headshot look established and press-ready.',
        items: [
          {
            title: 'Polished, credible look',
            description: 'A clean, neutral presentation that works for firm sites and press.',
            beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
            afterImage: '/assets/marketing/before-after/set-1-after.jpg',
          },
          {
            title: 'Cleaner background',
            description: 'Reduce distractions so the focus stays on you.',
            beforeImage: '/assets/marketing/before-after/set-2-before.jpg',
            afterImage: '/assets/marketing/before-after/set-2-after.png',
          },
        ],
      },
      {
        type: 'cards',
        title: 'Best styles by practice type',
        items: [
          {
            title: 'Corporate / business law',
            description: 'Clean studio lighting and classic framing.',
          },
          {
            title: 'Litigation',
            description: 'Slightly sharper contrast for confident presence.',
          },
          {
            title: 'Family / trusts',
            description: 'Warmer tone and approachable styling.',
          },
          {
            title: 'Boutique / modern firms',
            description: 'Contemporary, minimal look that feels current.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Perfect for',
        items: [
          'Firm websites and team pages',
          'LinkedIn and professional directories',
          'Press features and speaking bios',
          'Podcast, webinar, and conference materials',
        ],
      },
      {
        type: 'faq',
        title: 'FAQ',
        items: [
          {
            question: 'Will this look AI-generated?',
            answer:
              'We optimize for realism—natural detail, clean lighting, and professional composition—so the result looks like a real headshot.',
          },
          {
            question: 'Can our firm standardize headshots across the team?',
            answer:
              'Yes. Use the same style preset and background approach to keep your team page consistent and premium.',
          },
          {
            question: 'What background works best for attorneys?',
            answer:
              'Neutral studio tones (light gray, off-white) are the most versatile for websites, press, and LinkedIn.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'LinkedIn headshots', href: '/linkedin-headshots' },
      { label: 'Examples', href: '/examples' },
      { label: 'Reviews', href: '/reviews' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Upgrade your attorney headshot',
      description: 'Get a credible, premium look without scheduling a studio session.',
      label: 'Create my attorney headshot',
      href: '/pricing',
    },
  },
  'doctor-headshots': {
    slug: 'doctor-headshots',
    ctaIntent: 'headshots',
    title: 'Doctor Headshots for Clinics & Providers | AI Profile Photo Maker',
    description:
      'Premium medical headshots that feel trustworthy and human. Great for clinic sites, provider directories, and LinkedIn.',
    keywords:
      'doctor headshots, physician headshot, medical professional headshots, provider directory photo, clinic headshots',
    h1: 'Medical Headshots That Feel Trustworthy and Human',
    hero: {
      eyebrow: 'Medical headshots',
      headline: 'Medical Headshots That Feel Trustworthy and Human',
      subhead:
        'Patients decide fast. Create premium, realistic provider headshots from your photos—ideal for directories and clinic websites.',
      ctaLabel: 'Create my provider headshot',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See reviews',
      secondaryCtaHref: '/reviews',
      imageSrc: buildRoleStylePreviewUrl('medical'),
      imageAlt: 'Clinic-ready medical provider headshot',
      imageFallbackSrc: '/assets/marketing/before-after/set-3-after.png',
    },
    highlights: [
      { value: 'Clinic-ready', label: 'Provider pages' },
      { value: 'Trusted', label: 'Patient first impression' },
      { value: 'Consistent', label: 'Team branding' },
    ],
    sections: [
      {
        type: 'bullets',
        title: 'What patients respond to',
        items: [
          'Warm, calm expression (confidence + empathy).',
          'Clean lighting with minimal shadows.',
          'Minimal distractions with simple, professional backgrounds.',
          'Consistency across providers to increase clinic trust.',
        ],
      },
      {
        type: 'showcase',
        title: 'Before and after',
        intro: 'A clean, calm headshot helps patients feel confident before they book.',
        items: [
          {
            title: 'Clinic-ready presentation',
            description: 'A garden selfie becomes a trustworthy medical portrait with white coat.',
            beforeImage: '/assets/marketing/before-after/medical-before.jpg',
            afterImage: '/assets/marketing/before-after/medical.jpg',
            beforeAlt: 'Casual photo before doctor headshot',
            afterAlt: 'Professional doctor headshot with white coat and stethoscope',
          },
          {
            title: 'Faculty medical portrait',
            description:
              'From a casual snap to an academic medical portrait worthy of any department page.',
            beforeImage: '/assets/marketing/before-after/academic2-before.jpg',
            afterImage: '/assets/marketing/before-after/academic2-after.jpg',
            beforeAlt: 'Casual photo before medical faculty headshot',
            afterAlt: 'Professional medical faculty headshot',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Perfect for',
        items: [
          'Clinic websites and provider directories',
          'Telehealth profiles and intake portals',
          'LinkedIn and speaking bios',
          'Media and press materials',
        ],
      },
      {
        type: 'faq',
        title: 'FAQ',
        items: [
          {
            question: 'Should I wear scrubs or a white coat?',
            answer:
              'Either works. For a premium look, professional attire with a white coat reads “provider” instantly and photographs well.',
          },
          {
            question: 'Can we match headshots across multiple providers?',
            answer:
              'Yes. Pick one style preset and apply it across your team for a consistent, clinic-wide look.',
          },
          {
            question: 'Will this work for dentists, NPs, PAs, therapists too?',
            answer:
              'Yes. The same approach works for most medical and clinical roles; you just choose the tone you want (more formal vs more approachable).',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'Examples', href: '/examples' },
      { label: 'Reviews', href: '/reviews' },
      { label: 'Pricing', href: '/pricing' },
      { label: 'How it works', href: '/how-it-works' },
    ],
    cta: {
      title: 'Ready for a premium provider headshot?',
      description: 'Create a trustworthy, clinic-ready headshot in minutes.',
      label: 'Create my provider headshot',
      href: '/pricing',
    },
  },
  'compare-aragon-ai': {
    slug: 'compare/aragon-ai',
    ctaIntent: 'pricing',
    title: 'AI Profile Photo Maker vs Aragon AI',
    description:
      'Compare AI Profile Photo Maker and Aragon AI workflows, expectations, and positioning. Verify pricing and features before purchase.',
    keywords:
      'AI Profile Photo Maker vs Aragon AI, headshot comparison, AI headshot tools comparison',
    h1: 'AI Profile Photo Maker vs Aragon AI',
    hero: {
      eyebrow: 'Comparison',
      headline: 'AI Profile Photo Maker vs Aragon AI',
      subhead: 'Compare workflows, turnaround expectations, and positioning.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    sections: [
      {
        type: 'comparison',
        title: 'Quick comparison',
        columns: ['AI Profile Photo Maker', 'Aragon AI'],
        rows: [
          {
            label: 'Workflow',
            values: [
              'Upload one clear photo, pick a style, download fast',
              'Upload photos, select attire/backgrounds, download',
            ],
          },
          {
            label: 'Turnaround expectations',
            values: [
              'Minutes after upload for most users',
              'Instant single-headshot generation; advanced packs vary by package',
            ],
          },
          {
            label: 'Pricing & packages',
            values: [
              'Credits and packages with clear options',
              'Basic $35 (40 headshots), Standard $45 (60), Executive $75 (100)',
            ],
          },
          {
            label: 'Outfit variety (style-driven)',
            values: [
              'Preset styles define outfit/background combinations (no direct selection)',
              '1 attire (Basic), 2 attires (Standard), all attires (Executive)',
            ],
          },
          {
            label: 'Background variety (style-driven)',
            values: [
              'Preset styles define outfit/background combinations (no direct selection)',
              '1 background (Basic), 2 backgrounds (Standard), all backgrounds (Executive)',
            ],
          },
          {
            label: 'Resolution',
            values: [
              'High-resolution downloads',
              'Standard resolution (Basic/Standard), enhanced (Executive)',
            ],
          },
        ],
        note: 'Pricing and features change; verify before publishing or purchasing.',
      },
      {
        type: 'cards',
        title: 'Best for',
        items: [
          {
            title: 'Fast turnaround',
            description: 'Ideal if you want results in minutes, not days.',
          },
          {
            title: 'Flexible credits',
            description: 'Choose a package that matches your headshot needs.',
          },
          {
            title: 'LinkedIn-ready output',
            description: 'Optimized framing and lighting for professional profiles.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Why choose AI Profile Photo Maker',
        items: [
          'Fast, self-serve workflow with clear pricing options.',
          'Realistic headshots focused on professional use cases.',
          'Privacy-first approach with user controls.',
        ],
      },
    ],
    relatedLinks: [
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'Examples', href: '/examples' },
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'Corporate headshot', href: '/corporate-headshot' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Ready to compare results?',
      description: 'Generate your own headshots and see the difference.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  'compare-headshotpro': {
    slug: 'compare/headshotpro',
    ctaIntent: 'pricing',
    title: 'AI Profile Photo Maker vs HeadshotPro',
    description:
      'Compare AI Profile Photo Maker and HeadshotPro workflows, expectations, and positioning. Verify pricing and features before purchase.',
    keywords:
      'AI Profile Photo Maker vs HeadshotPro, headshot comparison, AI headshot tools comparison',
    h1: 'AI Profile Photo Maker vs HeadshotPro',
    hero: {
      eyebrow: 'Comparison',
      headline: 'AI Profile Photo Maker vs HeadshotPro',
      subhead: 'Compare setup steps, output style, and pricing expectations.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    sections: [
      {
        type: 'comparison',
        title: 'Quick comparison',
        columns: ['AI Profile Photo Maker', 'HeadshotPro'],
        rows: [
          {
            label: 'Setup steps',
            values: [
              'Upload one clear photo, select a style, download results',
              'Upload photos, choose style options, download favorites',
            ],
          },
          {
            label: 'Pricing & packages',
            values: [
              'Credits and packages with clear options',
              'Basic $29 (40 headshots), Professional $39 (80), Executive $59 (120)',
            ],
          },
          {
            label: 'Turnaround expectations',
            values: [
              'Minutes after upload for most users',
              '4 hours (Basic), 2 hours (Professional), 1 hour (Executive)',
            ],
          },
          {
            label: 'Style variations (backdrops & outfits)',
            values: [
              'Preset styles set backdrops/outfits (no direct selection)',
              '4 / 8 / 12 backdrop + outfit combos; Basic uses preselected combos, Pro/Exec add hundreds of styles',
            ],
          },
          {
            label: 'Resolution & edits',
            values: [
              'High-resolution downloads with consistent styling',
              'Standard (Basic); Premium + 10 edit credits (Professional); 4K + 40 edit credits + print-ready (Executive)',
            ],
          },
          {
            label: 'Data retention',
            values: [
              'User-managed deletions and retention policies',
              'Input photos deleted after 7 days; AI headshots deleted after 30 days; delete sooner in settings',
            ],
          },
        ],
        note: 'Pricing and features change; verify before publishing or purchasing.',
      },
      {
        type: 'cards',
        title: 'Best for',
        items: [
          {
            title: 'Fast professional refresh',
            description: 'Ideal if you need new headshots quickly for a profile update.',
          },
          {
            title: 'Flexible use',
            description: 'Great for both individuals and teams.',
          },
          {
            title: 'LinkedIn optimization',
            description: 'Framing and lighting tailored for professional profiles.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Why choose AI Profile Photo Maker',
        items: [
          'Self-serve workflow with fast results.',
          'Clear pricing and easy upgrades.',
          'Realistic headshots that look like you.',
        ],
      },
    ],
    relatedLinks: [
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'Examples', href: '/examples' },
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'Corporate headshot', href: '/corporate-headshot' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'See your own results in minutes',
      description: 'Generate a full headshot set and compare the quality yourself.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  features: {
    slug: 'features',
    ctaIntent: 'pricing',
    title: 'AI Headshot Features Built for Professional Profiles',
    description:
      'Explore AI Profile Photo Maker features: realistic headshots, fast turnaround, privacy-first processing, and flexible styles.',
    keywords:
      'AI headshot features, professional headshot tools, AI photo enhancement features, headshot generator features',
    h1: 'AI Headshot Features Built for Professional Profiles',
    hero: {
      eyebrow: 'Features',
      headline: 'AI Headshot Features Built for Professional Profiles',
      subhead:
        'Upload one clear photo and get a studio-quality headshot instantly — realistic detail, flexible styles, and privacy-first processing.',
      ctaLabel: 'Get your headshot instantly',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
      imageSrc: '/assets/marketing/before-after/executive-after.jpg',
      imageAlt: 'AI-generated professional headshot with studio lighting',
    },
    highlights: [
      { value: 'Instant', label: 'Typical generation' },
      { value: '20+', label: 'Style options' },
      { value: 'HD', label: 'Resolution output' },
    ],
    sections: [
      {
        type: 'showcase' as const,
        title: 'See the AI in action',
        intro:
          'Upload a casual photo and get back a polished, professional headshot — lighting, background, and framing all refined automatically.',
        items: [
          {
            title: 'Executive polish',
            description:
              'A relaxed home photo becomes a boardroom-ready portrait with balanced lighting and a clean city backdrop.',
            beforeImage: '/assets/marketing/before-after/executive-before.jpeg',
            afterImage: '/assets/marketing/before-after/executive-after.jpg',
            beforeAlt: 'Casual home photo before AI headshot processing',
            afterAlt: 'Executive headshot with suit and city skyline backdrop',
          },
          {
            title: 'LinkedIn-ready clarity',
            description:
              'From a coffee shop snapshot to a crisp corporate portrait with professional framing and natural skin tones.',
            beforeImage: '/assets/marketing/before-after/linkedin-before.jpeg',
            afterImage: '/assets/marketing/before-after/linkedin-after.jpg',
            beforeAlt: 'Coffee shop snapshot before LinkedIn headshot',
            afterAlt: 'LinkedIn-optimized professional headshot',
          },
        ],
      },
      {
        type: 'cards',
        title: 'Core capabilities',
        items: [
          {
            title: 'Custom AI model training',
            description:
              'Each headshot set is trained on your photos so results stay consistent with your facial features and identity.',
          },
          {
            title: '20+ professional styles',
            description:
              'Choose from LinkedIn classic, corporate modern, creative portrait, academic, medical, and more.',
          },
          {
            title: 'Natural detail preservation',
            description:
              'Balanced retouching keeps skin texture, facial features, and expressions realistic — no plastic AI look.',
          },
          {
            title: 'Fast delivery',
            description:
              'Most headshot sets are ready within minutes after upload, not hours or days.',
          },
        ],
      },
      {
        type: 'showcase' as const,
        title: 'Works across professions',
        intro:
          'The same upload flow produces headshots tailored to different industries and use cases.',
        items: [
          {
            title: 'Academic faculty portrait',
            description:
              'A casual selfie transformed into a distinguished faculty directory headshot with warm, scholarly backdrop.',
            beforeImage: '/assets/marketing/before-after/academic1-before.jpg',
            afterImage: '/assets/marketing/before-after/academic1-after.jpg',
            beforeAlt: 'Casual selfie before academic headshot',
            afterAlt: 'Professional academic headshot with library backdrop',
          },
          {
            title: 'Medical provider portrait',
            description:
              'From an outdoor photo to a clinic-ready portrait with white coat and professional medical setting.',
            beforeImage: '/assets/marketing/before-after/medical-before.jpg',
            afterImage: '/assets/marketing/before-after/medical.jpg',
            beforeAlt: 'Casual outdoor photo before medical headshot',
            afterAlt: 'Professional medical headshot with white coat',
          },
          {
            title: 'Lifestyle and dating',
            description:
              'A backyard snapshot becomes a sun-kissed, approachable portrait perfect for dating apps and social profiles.',
            beforeImage: '/assets/marketing/before-after/beach-vibes-before.jpg',
            afterImage: '/assets/marketing/before-after/beach-vibes-after.jpg',
            beforeAlt: 'Casual garden photo before lifestyle headshot',
            afterAlt: 'Beach vibes portrait with golden hour lighting',
          },
        ],
      },
      {
        type: 'steps',
        title: 'How it works',
        items: [
          {
            title: 'Upload one clear photo',
            description:
              'Front-facing shots with variety in angles and lighting help the AI model learn your features accurately.',
          },
          {
            title: 'Choose your styles',
            description:
              'Pick from LinkedIn, corporate, creative, medical, academic, and more — each optimized for its use case.',
          },
          {
            title: 'Download and publish',
            description:
              'Get high-resolution headshots in minutes. Use them across LinkedIn, resumes, websites, and directories.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Built for professionals',
        items: [
          'Privacy-first processing with encrypted uploads and user-controlled data deletion.',
          'High-resolution output that looks sharp across digital and print formats.',
          'Cross-platform ready — optimized framing for LinkedIn, resumes, team pages, and portfolios.',
          'Team-friendly workflow for consistent headshots across departments and locations.',
        ],
      },
      {
        type: 'faq',
        title: 'Features FAQ',
        items: [
          {
            question: 'How many photos should I upload?',
            answer:
              'We recommend one clear photo with varied angles and lighting for the best results.',
          },
          {
            question: 'Will the headshots look like me?',
            answer:
              'Yes. The AI is trained on your photos to preserve your facial features and identity.',
          },
          {
            question: 'Can I use these commercially?',
            answer:
              'Yes. You can use your headshots for business profiles, marketing, and professional use.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'Examples', href: '/examples' },
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'How it works', href: '/how-it-works' },
      { label: 'Corporate headshot', href: '/corporate-headshot' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Ready to see these features in action?',
      description: 'Upload one clear photo and get a professional headshot instantly.',
      label: 'Get your headshot instantly',
      href: '/pricing',
    },
  },
};
