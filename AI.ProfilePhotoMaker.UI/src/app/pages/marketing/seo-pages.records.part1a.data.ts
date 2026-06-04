import type { SeoPageContent } from './seo-pages.types';

const STYLE_PREVIEW_BASE_URL = 'https://aipmstv16j74jubocuukg.blob.core.windows.net/style-previews';
const STYLE_PREVIEW_CACHE_VERSION = '20260110';

const buildRoleStylePreviewUrl = (styleName: 'medical' | 'executive'): string =>
  `${STYLE_PREVIEW_BASE_URL}/${styleName}.jpg?v=${STYLE_PREVIEW_CACHE_VERSION}`;

export const seoPagesPart1a: Record<string, SeoPageContent> = {
  'how-it-works': {
    slug: 'how-it-works',
    ctaIntent: 'pricing',
    title: 'How AI Profile Photo Maker Works | Studio-quality headshots in minutes',
    description:
      'Studio-quality headshots in minutes. Upload one clear photo and get professional results fast.',
    keywords:
      'how AI headshots work, headshot process, AI headshot workflow, professional headshots in minutes',
    h1: 'How AI Profile Photo Maker Works',
    hero: {
      eyebrow: 'How it works',
      headline: 'How AI Profile Photo Maker Works',
      subhead: 'Upload one clear photo, pick a style, and get professional results fast.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    highlights: [
      { value: 'One photo', label: 'Minimum input' },
      { value: 'Minutes', label: 'Typical delivery' },
      { value: '20+', label: 'Styles available' },
    ],
    sections: [
      {
        type: 'steps',
        title: 'Three simple steps',
        items: [
          {
            title: 'Upload one clear photo',
            description:
              'Front-facing shots with variety in angles and lighting help the model learn your features accurately.',
          },
          {
            title: 'Choose a style',
            description:
              'Pick LinkedIn, classic, or creative looks and let the AI build a consistent headshot set.',
          },
          {
            title: 'Receive your headshots',
            description:
              'Download polished results in minutes and use them across profiles, resumes, and portfolios.',
          },
        ],
      },
      {
        type: 'showcase' as const,
        title: 'See the transformation',
        intro: 'Upload a casual photo and get back a polished, professional headshot.',
        items: [
          {
            title: 'Academic transformation',
            description: 'A relaxed home photo becomes a distinguished faculty portrait.',
            beforeImage: '/assets/marketing/before-after/academic1-before.jpg',
            afterImage: '/assets/marketing/before-after/academic1-after.jpg',
            beforeAlt: 'Casual home photo before transformation',
            afterAlt: 'Professional academic headshot after transformation',
          },
          {
            title: 'Corporate upgrade',
            description: 'From coffee mug in hand to corner office ready.',
            beforeImage: '/assets/marketing/before-after/executive-before.jpeg',
            afterImage: '/assets/marketing/before-after/executive-after.jpg',
            beforeAlt: 'Casual photo before corporate headshot',
            afterAlt: 'Executive corporate headshot',
          },
        ],
      },
      {
        type: 'cards',
        title: 'Why the results feel natural',
        intro: 'Our pipeline is tuned for realism, not plastic faces or harsh filters.',
        items: [
          {
            title: 'Lighting refinement',
            description: 'Balances exposure while preserving skin texture and natural detail.',
          },
          {
            title: 'Background control',
            description: 'Clean, professional settings without distracting artifacts.',
          },
          {
            title: 'Identity preservation',
            description: 'Keeps your facial features consistent so the image still looks like you.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Privacy and retention',
        items: [
          'Privacy-first processing with encrypted uploads.',
          'Delete your data at any time from account settings.',
          'Clear retention policies and no resale of personal data.',
        ],
      },
    ],
    relatedLinks: [
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'LinkedIn headshots', href: '/linkedin-headshots' },
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'Corporate headshot', href: '/corporate-headshot' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Ready to see your results?',
      description: 'Start with clear photos and get a full headshot gallery in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  examples: {
    slug: 'examples',
    ctaIntent: 'headshots',
    title: 'AI Headshot Examples | Before and After Results',
    description:
      'See before and after AI headshot examples. Realistic, professional results tailored to your input photos.',
    keywords:
      'AI headshot examples, before and after headshots, professional headshot results, AI photo examples',
    h1: 'AI Headshot Examples (Before and After)',
    hero: {
      eyebrow: 'Examples',
      headline: 'AI Headshot Examples (Before and After)',
      subhead:
        'Realistic, professional results tailored to you. Results vary based on lighting and input quality.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'How it works',
      secondaryCtaHref: '/how-it-works',
    },
    sections: [
      {
        type: 'showcase',
        title: 'Before and after highlights',
        intro: 'Each set is trained on your photos for consistency and realism.',
        items: [
          {
            title: 'Executive boardroom ready',
            description:
              'From a casual home photo to a polished executive portrait with city skyline backdrop.',
            beforeImage: '/assets/marketing/before-after/executive-before.jpeg',
            afterImage: '/assets/marketing/before-after/executive-after.jpg',
            beforeAlt: 'Casual home photo before executive headshot',
            afterAlt: 'Executive headshot with suit and city backdrop',
          },
          {
            title: 'Academic faculty portrait',
            description:
              'A relaxed selfie transformed into a distinguished faculty directory headshot.',
            beforeImage: '/assets/marketing/before-after/academic1-before.jpg',
            afterImage: '/assets/marketing/before-after/academic1-after.jpg',
            beforeAlt: 'Casual selfie before academic headshot',
            afterAlt: 'Professional academic headshot with library backdrop',
          },
          {
            title: 'LinkedIn career ready',
            description: 'Coffee shop snapshot to corporate hallway portrait in minutes.',
            beforeImage: '/assets/marketing/before-after/linkedin-before.jpeg',
            afterImage: '/assets/marketing/before-after/linkedin-after.jpg',
            beforeAlt: 'Coffee shop photo before LinkedIn headshot',
            afterAlt: 'Professional LinkedIn headshot in corporate setting',
          },
        ],
      },
      {
        type: 'cards',
        title: 'Style showcase',
        items: [
          {
            title: 'LinkedIn classic',
            description: 'Neutral backgrounds and clean framing for a professional profile.',
          },
          {
            title: 'Corporate modern',
            description: 'Slightly brighter tones with sharper clarity for business teams.',
          },
          {
            title: 'Creative portrait',
            description: 'Stylish depth and softer light for portfolios and personal brands.',
          },
        ],
      },
      {
        type: 'testimonials',
        title: 'What customers notice first',
        items: [
          {
            quote: 'My LinkedIn profile finally looks polished. The results felt realistic.',
            name: 'Amelia Walsh',
            role: 'Customer',
          },
          {
            quote: 'The output still looks like me, just more professional.',
            name: 'Noah Bennett',
            role: 'Customer',
          },
          {
            quote: 'The final set gave me consistent photos across platforms.',
            name: 'Grace Nolan',
            role: 'Customer',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'How it works', href: '/how-it-works' },
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'LinkedIn headshots', href: '/linkedin-headshots' },
      { label: 'Corporate headshot', href: '/corporate-headshot' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Want a before and after set of your own?',
      description: 'Upload one clear photo and get a full gallery of headshots in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  'corporate-headshot': {
    slug: 'corporate-headshot',
    ctaIntent: 'headshots',
    title: 'Corporate Headshots | Polished photos for teams and leaders',
    description:
      'Create polished corporate headshots for leadership pages, team profiles, and company directories. Fast turnaround with realistic results.',
    keywords: 'corporate headshots, business headshots, executive headshots, team profile photos',
    h1: 'Corporate Headshots',
    hero: {
      eyebrow: 'Corporate headshots',
      headline: 'Corporate Headshots',
      subhead:
        'Create consistent, polished headshots for executives and teams without booking a studio.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
      imageSrc: '/assets/marketing/before-after/academic2-after.jpg',
      imageAlt: 'Corporate-style professional headshot result',
      imageFallbackSrc: '/assets/marketing/before-after/set-2-after.png',
    },
    highlights: [
      { value: 'Consistent', label: 'Team look and feel' },
      { value: 'Minutes', label: 'To first results' },
      { value: 'Professional', label: 'Brand-ready output' },
    ],
    sections: [
      {
        type: 'showcase',
        title: 'Corporate headshot results',
        intro: 'See how everyday uploads become polished, business-ready portraits.',
        items: [
          {
            title: 'Executive polish',
            description: 'Balanced lighting and clean framing for leadership profiles.',
            beforeImage: '/assets/marketing/before-after/academic2-before.jpg',
            afterImage: '/assets/marketing/before-after/academic2-after.jpg',
            beforeAlt: 'Casual photo before corporate headshot processing',
            afterAlt: 'Polished corporate headshot after processing',
          },
          {
            title: 'Brand-safe presentation',
            description: 'Professional background cleanup for company websites and directories.',
            beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
            afterImage: '/assets/marketing/before-after/set-1-after.jpg',
            beforeAlt: 'Photo with distracting background',
            afterAlt: 'Photo with clean professional background',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'What corporate headshots should deliver',
        items: [
          'Consistent style across executives and team members.',
          'Professional wardrobe and background styling.',
          'Natural skin tones and realistic facial detail.',
          'Profile-ready framing for websites and LinkedIn.',
        ],
      },
      {
        type: 'cards',
        title: 'Best for',
        items: [
          {
            title: 'Leadership pages',
            description: 'Create consistent executive portraits for company leadership profiles.',
          },
          {
            title: 'Team directories',
            description: 'Keep every team profile aligned with your brand presentation.',
          },
          {
            title: 'Sales and recruiting',
            description: 'Use polished photos across proposals, bios, and recruiting materials.',
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
              'Share varied, well-lit photos so the model captures your features accurately.',
          },
          {
            title: 'Choose corporate-friendly styles',
            description: 'Pick looks that match your industry, role, and brand tone.',
          },
          {
            title: 'Download and publish',
            description: 'Use your favorites for websites, LinkedIn, decks, and team directories.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'How it works', href: '/how-it-works' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Need corporate-ready headshots fast?',
      description: 'Generate polished portraits for your team and brand in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  'ai-headshot-generator': {
    slug: 'ai-headshot-generator',
    ctaIntent: 'headshots',
    title: 'AI Headshot Generator for Professional Profiles',
    description:
      'Create studio-quality AI headshots for LinkedIn, resumes, and professional profiles. Fast, realistic, and private.',
    keywords:
      'AI headshot generator, professional headshots, LinkedIn headshots, AI profile photo maker',
    h1: 'AI Headshot Generator for Professional Profiles',
    hero: {
      eyebrow: 'AI headshot generator',
      headline: 'AI Headshot Generator for Professional Profiles',
      subhead: 'Create studio-quality headshots for LinkedIn, resumes, and portfolios.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
      imageSrc: buildRoleStylePreviewUrl('executive'),
      imageAlt: 'Professional AI headshot result',
      imageFallbackSrc: '/assets/marketing/before-after/set-2-after.png',
    },
    highlights: [
      { value: 'Studio', label: 'Quality results' },
      { value: 'Minutes', label: 'To first set' },
      { value: 'Privacy', label: 'First processing' },
    ],
    sections: [
      {
        type: 'showcase',
        title: 'Before and after results',
        intro: 'See how everyday photos become polished, profile-ready headshots.',
        items: [
          {
            title: 'Sharper lighting and framing',
            description: 'Balanced light and cleaner framing for a stronger first impression.',
            beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
            afterImage: '/assets/marketing/before-after/set-1-after.jpg',
            beforeAlt: 'Casual selfie before AI headshot generation',
            afterAlt: 'Professional AI-generated headshot after processing',
          },
          {
            title: 'Polished profile-ready finish',
            description: 'Natural detail with a professional look that still feels like you.',
            beforeImage: '/assets/marketing/before-after/set-3-before.jpg',
            afterImage: '/assets/marketing/before-after/set-3-after.png',
            beforeAlt: 'Original photo before enhancement',
            afterAlt: 'Refined AI headshot for LinkedIn and resumes',
          },
        ],
      },
      {
        type: 'cards',
        title: 'Why AI headshots',
        items: [
          {
            title: 'Skip the studio',
            description: 'No scheduling or travel, just polished results from home.',
          },
          {
            title: 'Consistent results',
            description: 'Generate multiple looks that stay consistent across profiles.',
          },
          {
            title: 'Realistic output',
            description: 'Natural detail and lighting tuned for believable results.',
          },
        ],
      },
      {
        type: 'steps',
        title: 'How it works',
        items: [
          {
            title: 'Upload one clear photo',
            description: 'Use varied angles and lighting for stronger face consistency.',
          },
          {
            title: 'Select your style',
            description: 'Choose LinkedIn, classic, or creative looks for your goals.',
          },
          {
            title: 'Download your headshots',
            description: 'Download your favorites for LinkedIn, resumes, and websites.',
          },
        ],
      },
      {
        type: 'faq',
        title: 'FAQ',
        items: [
          {
            question: 'How many photos should I upload?',
            answer: 'We recommend one clear photo with varied angles and lighting.',
          },
          {
            question: 'How fast are results?',
            answer: 'Most users receive their headshots within minutes after upload.',
          },
          {
            question: 'Can I use the images commercially?',
            answer: 'Yes. You can use your headshots for business and professional use.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'LinkedIn headshots', href: '/linkedin-headshots' },
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'Headshots for job search', href: '/headshots-for-job-search' },
      { label: 'Corporate headshot', href: '/corporate-headshot' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Ready for studio-quality headshots?',
      description: 'Start your AI headshot generation and download results in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  'linkedin-headshots': {
    slug: 'linkedin-headshots',
    ctaIntent: 'headshots',
    title: 'LinkedIn Headshots That Look Like You',
    description:
      'Professional LinkedIn headshots that look like you. Realistic, polished, and optimized for profile visibility.',
    keywords:
      'LinkedIn headshots, professional LinkedIn photo, AI LinkedIn headshot, LinkedIn profile photo',
    h1: 'LinkedIn Headshots That Look Like You',
    hero: {
      eyebrow: 'LinkedIn headshots',
      headline: 'LinkedIn Headshots That Look Like You',
      subhead: 'Professional, realistic headshots optimized for LinkedIn profiles.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
      imageSrc: '/assets/marketing/before-after/linkedin-after.jpg',
      imageAlt: 'LinkedIn-ready professional headshot',
    },
    highlights: [
      { value: 'Profile-ready', label: 'Framing and crop' },
      { value: 'Natural', label: 'Realistic skin tones' },
      { value: 'Minutes', label: 'To publish-ready shots' },
    ],
    sections: [
      {
        type: 'showcase',
        title: 'LinkedIn before and after',
        intro: 'Compare casual uploads with polished results designed for profile visibility.',
        items: [
          {
            title: 'Professional profile upgrade',
            description: 'From a coffee shop snapshot to a polished corporate portrait.',
            beforeImage: '/assets/marketing/before-after/linkedin-before.jpeg',
            afterImage: '/assets/marketing/before-after/linkedin-after.jpg',
            beforeAlt: 'Casual photo before LinkedIn headshot',
            afterAlt: 'LinkedIn-ready professional headshot',
          },
          {
            title: 'Clean profile framing',
            description: 'Casual selfie transformed into a crisp, well-lit LinkedIn photo.',
            beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
            afterImage: '/assets/marketing/before-after/set-1-after.jpg',
            beforeAlt: 'Casual selfie before LinkedIn optimization',
            afterAlt: 'LinkedIn-optimized professional headshot',
          },
        ],
      },
      {
        type: 'cards',
        title: 'What works best on LinkedIn',
        items: [
          {
            title: 'Head and shoulders focus',
            description: 'Centered framing that keeps your face visible even at small sizes.',
          },
          {
            title: 'Clean backgrounds',
            description: 'Neutral backdrops that keep attention on you.',
          },
          {
            title: 'Natural professional lighting',
            description: 'Balanced light and tone that reads trustworthy and real.',
          },
          {
            title: 'Simple outfit styling',
            description: 'Solid colors and minimal patterns keep focus on your face.',
          },
        ],
      },
      {
        type: 'faq',
        title: 'FAQ',
        items: [
          {
            question: 'Will this work for international profiles?',
            answer: 'Yes. LinkedIn headshots should be clear and professional in any market.',
          },
          {
            question: 'Can I create multiple LinkedIn looks?',
            answer: 'Yes. Choose different styles to match roles or industries.',
          },
          {
            question: 'Do I need a suit or blazer?',
            answer: 'Wear what aligns with your industry. A clean, professional outfit is best.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'Headshots for job search', href: '/headshots-for-job-search' },
      { label: 'Corporate headshot', href: '/corporate-headshot' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Make a stronger LinkedIn first impression',
      description: 'Generate a set of LinkedIn-ready headshots in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
};
