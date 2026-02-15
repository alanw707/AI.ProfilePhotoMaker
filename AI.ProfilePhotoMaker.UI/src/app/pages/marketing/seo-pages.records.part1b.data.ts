import type { SeoPageContent } from './seo-pages.types';

export const seoPagesPart1b: Record<string, SeoPageContent> = {
  'professional-headshots': {
    slug: 'professional-headshots',
    title: 'Professional Headshots Without the Studio',
    description:
      'Consistent, high-quality professional headshots for teams and individuals. Skip studio scheduling and get results fast.',
    keywords:
      'professional headshots, business headshots, team headshots, studio-quality headshots',
    h1: 'Professional Headshots Without the Studio',
    hero: {
      eyebrow: 'Professional headshots',
      headline: 'Professional Headshots Without the Studio',
      subhead: 'Consistent, high-quality headshots for teams and individuals.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'How it works',
      secondaryCtaHref: '/how-it-works',
    },
    sections: [
      {
        type: 'cards',
        title: 'Business use cases',
        items: [
          {
            title: 'Team pages',
            description: 'Give your team a consistent, professional look across profiles.',
          },
          {
            title: 'Sales and leadership',
            description: 'Polished headshots that build trust with prospects.',
          },
          {
            title: 'Recruiting and HR',
            description: 'Modern headshots for leadership bios and hiring content.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Realism first',
        items: [
          'Natural detail preservation to keep your identity consistent.',
          'Balanced lighting to avoid harsh edits or plastic skin.',
          'Multiple styles to match different professional contexts.',
        ],
      },
      {
        type: 'cards',
        title: 'Flexible for teams and individuals',
        items: [
          {
            title: 'Single profile refresh',
            description: 'Update your headshot without a photoshoot.',
          },
          {
            title: 'Team-wide consistency',
            description: 'Coordinate styles across departments and locations.',
          },
          {
            title: 'Fast turnaround',
            description: 'Deliver headshots quickly for new hires and launches.',
          },
        ],
      },
      {
        type: 'faq',
        title: 'FAQ',
        items: [
          {
            question: 'Can teams standardize a look?',
            answer: 'Yes. Choose a shared style so every headshot feels cohesive.',
          },
          {
            question: 'Will images match my brand colors?',
            answer: 'We keep backgrounds clean and neutral for easy brand alignment.',
          },
          {
            question: 'Is this suitable for executives?',
            answer: 'Yes. The output is polished and professional for leadership profiles.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'LinkedIn headshots', href: '/linkedin-headshots' },
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'Examples', href: '/examples' },
      { label: 'Headshot enhancer', href: '/free-headshot-enhancer' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Upgrade your professional headshot',
      description: 'Create studio-quality headshots without scheduling a session.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  'headshots-for-job-search': {
    slug: 'headshots-for-job-search',
    title: 'Headshots for Job Seekers | Make a strong first impression',
    description:
      'Headshots for job seekers who want a polished, professional photo before applying. Fast, realistic results.',
    keywords:
      'headshots for job seekers, job search headshots, resume headshot, professional photo for applications',
    h1: 'Headshots for Job Seekers',
    hero: {
      eyebrow: 'Job search headshots',
      headline: 'Headshots for Job Seekers',
      subhead: 'First impressions matter. Get a polished headshot before your next application.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    sections: [
      {
        type: 'bullets',
        title: 'Why recruiters care',
        items: [
          'A professional photo builds trust and credibility quickly.',
          'Clean headshots stand out in crowded applicant pools.',
          'Consistent branding helps your profile feel polished.',
        ],
      },
      {
        type: 'cards',
        title: 'Recommended styles',
        items: [
          {
            title: 'LinkedIn classic',
            description: 'Clean, neutral backgrounds with professional framing.',
          },
          {
            title: 'Corporate modern',
            description: 'Sharper lighting and subtle contrast for tech roles.',
          },
          {
            title: 'Warm professional',
            description: 'Approachable tone for client-facing roles.',
          },
        ],
      },
      {
        type: 'faq',
        title: 'FAQ',
        items: [
          {
            question: 'Do I need a professional camera?',
            answer: 'No. Clear smartphone selfies are enough to get strong results.',
          },
          {
            question: 'Can I update my headshot later?',
            answer: 'Yes. You can generate new styles whenever you need a refresh.',
          },
          {
            question: 'Will this work for remote roles?',
            answer: 'Yes. A polished photo helps across remote and in-person roles.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'LinkedIn headshots', href: '/linkedin-headshots' },
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'Headshot enhancer', href: '/free-headshot-enhancer' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Make your next application stand out',
      description: 'Create a job-ready headshot in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  'realtor-headshots': {
    slug: 'realtor-headshots',
    title: 'Realtor Headshots That Look Real (AI) | AI Profile Photo Maker',
    description:
      'Premium realtor headshots from your photos—polished, realistic, and consistent. Perfect for Zillow, LinkedIn, and Google Business.',
    keywords:
      'realtor headshots, real estate agent headshots, broker headshot, zillow headshot, professional realtor photo',
    h1: 'Realtor Headshots That Build Trust in One Second',
    hero: {
      eyebrow: 'Real estate headshots',
      headline: 'Realtor Headshots That Build Trust in One Second',
      subhead:
        'Create studio-quality realtor headshots from your photos—clean lighting, premium styling, and a natural look that still feels like you.',
      ctaLabel: 'Create my headshot',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
      imageSrc: '/assets/marketing/before-after/set-1-after.jpg',
      imageAlt: 'Premium realtor headshot example',
    },
    highlights: [
      { value: 'Zillow-ready', label: 'Profile formats' },
      { value: 'Minutes', label: 'Typical delivery' },
      { value: 'Premium', label: 'Professional look' },
    ],
    sections: [
      {
        type: 'showcase',
        title: 'Before and after',
        intro:
          'Subtle improvements in lighting, background, and color add up to a more premium first impression.',
        items: [
          {
            title: 'Clean, professional lighting',
            description: 'Balanced exposure with natural detail for a studio-quality result.',
            beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
            afterImage: '/assets/marketing/before-after/set-1-after.jpg',
            beforeAlt: 'Original casual photo before enhancement',
            afterAlt: 'Professional headshot after enhancement',
          },
          {
            title: 'Background refinement',
            description: 'A cleaner setting keeps the focus on you and your brand.',
            beforeImage: '/assets/marketing/before-after/set-2-before.jpg',
            afterImage: '/assets/marketing/before-after/set-2-after.png',
          },
          {
            title: 'Polished color and contrast',
            description: 'Crisp but natural, ready for Zillow, LinkedIn, and your site.',
            beforeImage: '/assets/marketing/before-after/set-3-before.jpg',
            afterImage: '/assets/marketing/before-after/set-3-after.png',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'What top-producing agents get right',
        intro: 'Small details make you look established before anyone reads your bio.',
        items: [
          'Approachable confidence (friendly, not forced).',
          'Clean background and consistent lighting for instant credibility.',
          'Modern, neutral styling that stays timeless across markets.',
          'A headshot that matches your brand (luxury, modern, neighborhood expert).',
        ],
      },
      {
        type: 'cards',
        title: 'Recommended looks for real estate',
        items: [
          {
            title: 'Modern studio',
            description: 'Crisp, bright, minimal—built for profile conversion.',
          },
          {
            title: 'Luxury agent',
            description: 'Warmer tones and premium styling that signals high-end service.',
          },
          {
            title: 'Local expert',
            description: 'Approachable, clean, and confident for neighborhood trust.',
          },
          {
            title: 'Team consistency',
            description: 'Match headshots across agents for a unified brokerage page.',
          },
        ],
      },
      {
        type: 'steps',
        title: 'How it works',
        items: [
          {
            title: 'Upload a few photos',
            description: 'No studio session needed—clear selfies work great.',
          },
          {
            title: 'Choose a professional style',
            description: 'Pick a look that matches your brand and market.',
          },
          {
            title: 'Download your headshots',
            description: 'Get images ready for Zillow, LinkedIn, and your website.',
          },
        ],
      },
      {
        type: 'faq',
        title: 'FAQ',
        items: [
          {
            question: 'Do these headshots look like me?',
            answer:
              'Yes. We aim for a polished version of you—not a different person—so your headshot stays recognizable and natural.',
          },
          {
            question: 'Can I use these on Zillow, Realtor.com, and Google Business?',
            answer: 'Yes. The output is designed for major profile platforms and marketing pages.',
          },
          {
            question: 'What should I wear for a realtor headshot?',
            answer:
              'Stick to solid colors and simple patterns. A blazer or jacket typically reads more premium and timeless.',
          },
          {
            question: 'Can I standardize headshots for my team?',
            answer:
              'Yes. Use the same style preset across agents for a cohesive brokerage/team page.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'Examples', href: '/examples' },
      { label: 'Reviews', href: '/reviews' },
      { label: 'LinkedIn headshots', href: '/linkedin-headshots' },
      { label: 'Professional headshots', href: '/professional-headshots' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Ready for a premium realtor headshot?',
      description: 'Create a polished headshot that fits your brand in minutes.',
      label: 'Create my headshot',
      href: '/pricing',
    },
  },
};
