import type { SeoPageContent } from './seo-pages.types';

const STYLE_PREVIEW_BASE_URL = 'https://aipmstv16j74jubocuukg.blob.core.windows.net/style-previews';
const STYLE_PREVIEW_CACHE_VERSION = '20260110';

const buildRoleStylePreviewUrl = (styleName: 'medical' | 'executive'): string =>
  `${STYLE_PREVIEW_BASE_URL}/${styleName}.jpg?v=${STYLE_PREVIEW_CACHE_VERSION}`;

export const seoPagesPart1a: Record<string, SeoPageContent> = {
  'how-it-works': {
    slug: 'how-it-works',
    title: 'How AI Profile Photo Maker Works | Studio-quality headshots in minutes',
    description:
      'Studio-quality headshots in minutes. Upload clear selfies and get professional results fast.',
    keywords:
      'how AI headshots work, headshot process, AI headshot workflow, professional headshots in minutes',
    h1: 'How AI Profile Photo Maker Works',
    hero: {
      eyebrow: 'How it works',
      headline: 'How AI Profile Photo Maker Works',
      subhead: 'Upload clear selfies, pick a style, and get professional results fast.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    highlights: [
      { value: 'At least 10', label: 'Minimum selfies' },
      { value: 'Minutes', label: 'Typical delivery' },
      { value: '20+', label: 'Styles available' },
    ],
    sections: [
      {
        type: 'steps',
        title: 'Three simple steps',
        items: [
          {
            title: 'Upload at least 10 clear selfies',
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
      { label: 'Headshot enhancer', href: '/free-headshot-enhancer' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Ready to see your results?',
      description: 'Start with clear selfies and get a full headshot gallery in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  examples: {
    slug: 'examples',
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
            title: 'Neon city upgrade',
            description:
              'From a casual selfie to a crisp night-street portrait with cinematic neon glow.',
            beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
            afterImage: '/assets/marketing/before-after/set-1-after.jpg',
            beforeAlt: 'Casual selfie before headshot',
            afterAlt: 'Neon city headshot after',
          },
          {
            title: 'Event to studio casual',
            description:
              'From a formal event photo to a relaxed studio-style headshot with clean light.',
            beforeImage: '/assets/marketing/before-after/set-2-before.jpg',
            afterImage: '/assets/marketing/before-after/set-2-after.png',
            beforeAlt: 'Formal event photo before headshot',
            afterAlt: 'Studio casual headshot after',
          },
          {
            title: 'Outdoor casual to executive',
            description: 'From a bright outdoor snapshot to a polished suit-and-tie portrait.',
            beforeImage: '/assets/marketing/before-after/set-3-before.jpg',
            afterImage: '/assets/marketing/before-after/set-3-after.png',
            beforeAlt: 'Outdoor casual photo before headshot',
            afterAlt: 'Executive headshot after',
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
      { label: 'Headshot enhancer', href: '/free-headshot-enhancer' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Want a before and after set of your own?',
      description: 'Upload clear selfies and get a full gallery of headshots in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  reviews: {
    slug: 'reviews',
    title: 'AI Headshot Reviews | What customers say',
    description:
      'Read verified reviews for AI Profile Photo Maker. Professionals share how AI headshots improved LinkedIn, resumes, and profiles.',
    keywords: 'AI headshot reviews, customer testimonials, AI profile photo maker reviews',
    h1: 'AI Headshot Reviews from Professionals',
    hero: {
      eyebrow: 'Reviews',
      headline: 'AI Headshot Reviews from Professionals',
      subhead: 'Real feedback from customers who upgraded their profiles with AI headshots.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    highlights: [
      { value: '4.9/5', label: 'Average rating' },
      { value: 'Minutes', label: 'Typical delivery' },
      { value: '10k+', label: 'Headshots delivered' },
    ],
    sections: [
      {
        type: 'testimonials',
        title: 'What customers are saying',
        items: [
          {
            quote: 'My LinkedIn profile finally looks polished. The results felt realistic.',
            name: 'Amelia Walsh',
            role: 'Customer',
          },
          {
            quote: 'Super straightforward flow and the turnaround was fast.',
            name: 'Lachlan Reid',
            role: 'Customer',
          },
          {
            quote: 'The output still looks like me, just more professional.',
            name: 'Noah Bennett',
            role: 'Customer',
          },
        ],
      },
      {
        type: 'cards',
        title: 'Why professionals choose us',
        items: [
          {
            title: 'Looks authentic',
            description: 'Subtle retouching keeps your face consistent and realistic.',
          },
          {
            title: 'Fast delivery',
            description: 'Most customers receive their first set in minutes.',
          },
          {
            title: 'Flexible styles',
            description: 'LinkedIn, corporate, creative, and more in one package.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Most common feedback',
        items: [
          'Profiles look more professional within a day.',
          'Headshots feel consistent across teams.',
          'AI results still look like the real person.',
        ],
      },
      {
        type: 'faq',
        title: 'Reviews FAQ',
        items: [
          {
            question: 'Are these testimonials real?',
            answer: 'Yes. Feedback is collected from real customers after delivery.',
          },
          {
            question: 'How fast do people get results?',
            answer: 'Most customers receive their first headshots in minutes.',
          },
          {
            question: 'What if I need a different style?',
            answer: 'You can generate additional styles anytime from your account.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'Examples', href: '/examples' },
      { label: 'How it works', href: '/how-it-works' },
      { label: 'Pricing', href: '/pricing' },
      { label: 'Headshot enhancer', href: '/free-headshot-enhancer' },
    ],
    cta: {
      title: 'See why customers recommend us',
      description: 'Upload clear selfies and get a full headshot gallery in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  'free-headshot-enhancer': {
    slug: 'free-headshot-enhancer',
    title: 'Headshot Enhancer | Improve your profile photo fast',
    description:
      'Improve your existing profile photo with your credit balance. Weekly top-ups restore credits to 5 when below. Fix lighting, background, and color fast.',
    keywords:
      'headshot enhancer, profile photo enhancer, AI photo enhancement, headshot retouching',
    h1: 'Headshot Enhancer',
    hero: {
      eyebrow: 'Headshot enhancer',
      headline: 'Headshot Enhancer',
      subhead:
        'Improve your existing profile photo fast with your credit balance. Weekly top-ups restore credits to 5 when below.',
      ctaLabel: 'Get started',
      ctaHref: '/dashboard',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    highlights: [
      { value: 'Weekly', label: 'Top-up to 5' },
      { value: 'Minutes', label: 'Typical turnaround' },
      { value: 'No card', label: 'Required to start' },
    ],
    sections: [
      {
        type: 'showcase',
        title: 'Enhancement results',
        intro: 'See the difference AI enhancement makes to existing photos.',
        items: [
          {
            title: 'Lighting and clarity improvement',
            description: 'Balanced exposure with preserved natural detail.',
            beforeImage: '/assets/marketing/before-after/set-3-before.jpg',
            afterImage: '/assets/marketing/before-after/set-3-after.png',
            beforeAlt: 'Original photo before enhancement',
            afterAlt: 'Enhanced photo with better lighting',
          },
          {
            title: 'Background refinement',
            description: 'Cleaner background for professional presentation.',
            beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
            afterImage: '/assets/marketing/before-after/set-1-after.jpg',
            beforeAlt: 'Photo with distracting background',
            afterAlt: 'Photo with clean professional background',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'What the enhancer improves',
        items: [
          'Lighting cleanup to reduce harsh shadows or uneven exposure.',
          'Background cleanup for a cleaner, professional look.',
          'Color correction for natural skin tones.',
          'Detail preservation for realistic facial features.',
        ],
      },
      {
        type: 'cards',
        title: 'Best for',
        items: [
          {
            title: 'LinkedIn refresh',
            description: 'Turn a casual photo into a polished professional profile picture.',
          },
          {
            title: 'Team bios',
            description: 'Keep headshots consistent across team pages and directories.',
          },
          {
            title: 'Resume updates',
            description: 'Get a clean headshot for applications and portfolios.',
          },
        ],
      },
      {
        type: 'steps',
        title: 'How credits work',
        items: [
          {
            title: 'Create an account',
            description: 'Sign up once and get a credit balance with weekly top-ups when below 5.',
          },
          {
            title: 'Upload your photo',
            description: 'Choose an existing profile photo to improve.',
          },
          {
            title: 'Download your enhanced image',
            description: 'Use the improved version across your professional profiles.',
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
      title: 'Start with enhancement credits',
      description: 'Create an account and improve your profile photo today.',
      label: 'Get started',
      href: '/dashboard',
    },
  },
  'ai-headshot-generator': {
    slug: 'ai-headshot-generator',
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
            title: 'Upload at least 10 clear selfies',
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
            answer: 'We recommend at least 10 clear selfies with varied angles and lighting.',
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
      { label: 'Headshot enhancer', href: '/free-headshot-enhancer' },
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
      imageSrc: buildRoleStylePreviewUrl('executive'),
      imageAlt: 'LinkedIn-ready professional headshot',
      imageFallbackSrc: '/assets/marketing/before-after/set-3-after.png',
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
            title: 'Clean profile framing',
            description: 'Head-and-shoulders crop that reads clearly in feed and search.',
            beforeImage: '/assets/marketing/before-after/set-2-before.jpg',
            afterImage: '/assets/marketing/before-after/set-2-after.png',
            beforeAlt: 'Casual photo before LinkedIn optimization',
            afterAlt: 'LinkedIn-ready headshot after optimization',
          },
          {
            title: 'Professional lighting and tone',
            description: 'Balanced lighting and natural color for a credible first impression.',
            beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
            afterImage: '/assets/marketing/before-after/set-1-after.jpg',
            beforeAlt: 'Original selfie before styling',
            afterAlt: 'Professional LinkedIn headshot after styling',
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
      { label: 'Headshot enhancer', href: '/free-headshot-enhancer' },
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
