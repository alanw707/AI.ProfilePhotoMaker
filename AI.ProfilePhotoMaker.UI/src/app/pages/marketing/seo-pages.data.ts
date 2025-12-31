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
export type SeoShowcaseSection = SeoSectionBase & { type: 'showcase'; items: SeoCard[] };
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

export const seoPages: Record<string, SeoPageContent> = {
  'how-it-works': {
    slug: 'how-it-works',
    title: 'How AI Profile Photo Maker Works | Studio-quality headshots in minutes',
    description:
      'Studio-quality headshots in minutes. Upload clear selfies, pick a style, and get professional results fast.',
    keywords:
      'how AI headshots work, headshot process, AI headshot workflow, professional headshots in minutes',
    h1: 'How AI Profile Photo Maker Works',
    hero: {
      eyebrow: 'How it works',
      headline: 'How AI Profile Photo Maker Works',
      subhead:
        'Studio-quality headshots in minutes. Upload clear selfies, pick a style, and get professional results fast.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    highlights: [
      { value: '8-12', label: 'Recommended selfies' },
      { value: 'Minutes', label: 'Typical delivery' },
      { value: '20+', label: 'Styles available' },
    ],
    sections: [
      {
        type: 'steps',
        title: 'Three simple steps',
        items: [
          {
            title: 'Upload 8-12 clear selfies',
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
    ],
    cta: {
      title: 'Ready to see your results?',
      description:
        'Start with your first set of selfies and get a full headshot gallery in minutes.',
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
      ctaLabel: 'See your own results in minutes',
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
            title: 'Natural office lighting',
            description: 'Balanced exposure with a clean background for LinkedIn-ready shots.',
          },
          {
            title: 'Creative studio look',
            description:
              'Sharper contrast with controlled lighting while staying true to your face.',
          },
          {
            title: 'Warm professional tone',
            description: 'Soft highlights that keep skin detail intact and flattering.',
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
            quote: 'The results look like me, just better lit and more confident.',
            name: 'Jordan K.',
            role: 'Product Manager',
          },
          {
            quote: 'I updated my LinkedIn and started getting more profile views the same week.',
            name: 'Maria L.',
            role: 'Marketing Lead',
          },
          {
            quote: 'The before and after difference is huge, but it still feels authentic.',
            name: 'Sam R.',
            role: 'Consultant',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'How it works', href: '/how-it-works' },
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'LinkedIn headshots', href: '/linkedin-headshots' },
    ],
    cta: {
      title: 'Want a before and after set of your own?',
      description: 'Upload a few selfies and get a full gallery of headshots in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  'free-headshot-enhancer': {
    slug: 'free-headshot-enhancer',
    title: 'Free Headshot Enhancer | Improve your profile photo fast',
    description:
      'Improve your existing profile photo with free weekly enhancement credits. Fix lighting, background, and color fast.',
    keywords:
      'free headshot enhancer, profile photo enhancer, AI photo enhancement, free headshot credits',
    h1: 'Free Headshot Enhancer',
    hero: {
      eyebrow: 'Free enhancer',
      headline: 'Free Headshot Enhancer',
      subhead: 'Improve your existing profile photo fast with free weekly enhancement credits.',
      ctaLabel: 'Try the free enhancer',
      ctaHref: '/auth/register',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    highlights: [
      { value: 'Free', label: 'Weekly credits' },
      { value: 'Minutes', label: 'Typical turnaround' },
      { value: 'No card', label: 'Required to start' },
    ],
    sections: [
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
        title: 'How free credits work',
        items: [
          {
            title: 'Create a free account',
            description: 'Sign up once and receive recurring enhancement credits.',
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
      title: 'Start with free enhancement credits',
      description: 'Create a free account and improve your profile photo today.',
      label: 'Try the free enhancer',
      href: '/auth/register',
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
    },
    highlights: [
      { value: 'Studio', label: 'Quality results' },
      { value: 'Minutes', label: 'To first set' },
      { value: 'Privacy', label: 'First processing' },
    ],
    sections: [
      {
        type: 'cards',
        title: 'Why AI headshots',
        items: [
          {
            title: 'Skip the studio',
            description: 'No scheduling or travel. Get professional results from home.',
          },
          {
            title: 'Consistent results',
            description: 'Multiple looks with one upload flow for a cohesive brand image.',
          },
          {
            title: 'Realistic output',
            description: 'Natural detail and lighting tuned for authenticity.',
          },
        ],
      },
      {
        type: 'steps',
        title: 'How it works',
        items: [
          {
            title: 'Upload a short selfie set',
            description: 'Provide 8-12 clear images to train a personalized model.',
          },
          {
            title: 'Select your style',
            description: 'LinkedIn, classic, or creative styles tailored to your goals.',
          },
          {
            title: 'Download your headshots',
            description: 'Use your best shots anywhere you need a professional photo.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Popular use cases',
        items: [
          'LinkedIn and professional networking profiles.',
          'Resumes, cover letters, and job applications.',
          'Portfolio sites and personal branding.',
          'Team bios and company directories.',
        ],
      },
      {
        type: 'faq',
        title: 'FAQ',
        items: [
          {
            question: 'How many photos should I upload?',
            answer: 'We recommend 8-12 clear selfies with varied angles and lighting.',
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
      ctaLabel: 'Upgrade your LinkedIn photo',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    sections: [
      {
        type: 'cards',
        title: 'Ideal framing and background',
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
            title: 'Soft professional lighting',
            description: 'Balanced light that keeps skin tones natural.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Outfit guidance',
        items: [
          'Choose solid colors that contrast with the background.',
          'Avoid loud patterns that distract from your face.',
          'Wear what you would use in a professional meeting.',
        ],
      },
      {
        type: 'cards',
        title: 'Common mistakes to avoid',
        items: [
          {
            title: 'Overly filtered images',
            description: 'Heavy filters reduce trust and look artificial.',
          },
          {
            title: 'Busy backgrounds',
            description: 'Cluttered scenes pull focus from your face.',
          },
          {
            title: 'Low resolution uploads',
            description: 'Clear, high quality selfies deliver better results.',
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
    ],
    cta: {
      title: 'Make a stronger LinkedIn first impression',
      description: 'Generate a set of LinkedIn-ready headshots in minutes.',
      label: 'Upgrade your LinkedIn photo',
      href: '/pricing',
    },
  },
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
      ctaLabel: 'Create professional headshots',
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
    ],
    cta: {
      title: 'Upgrade your professional headshot',
      description: 'Create studio-quality headshots without scheduling a session.',
      label: 'Create professional headshots',
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
      ctaLabel: 'Get job-ready headshots',
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
    ],
    cta: {
      title: 'Make your next application stand out',
      description: 'Create a job-ready headshot in minutes.',
      label: 'Get job-ready headshots',
      href: '/pricing',
    },
  },
  'compare-aragon-ai': {
    slug: 'compare/aragon-ai',
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
      ctaLabel: 'Try AI Profile Photo Maker',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'See examples',
      secondaryCtaHref: '/examples',
    },
    sections: [
      {
        type: 'comparison',
        title: 'Quick comparison',
        columns: ['AI Profile Photo Maker', 'Aragon AI (verify current details)'],
        rows: [
          {
            label: 'Workflow',
            values: ['Upload selfies, pick styles, download fast', 'Check current onboarding flow'],
          },
          {
            label: 'Turnaround expectations',
            values: ['Minutes after upload for most users', 'Check current delivery estimates'],
          },
          {
            label: 'Pricing model',
            values: [
              'Credits and packages with clear options',
              'Verify pricing tiers on provider site',
            ],
          },
          {
            label: 'Style control',
            values: ['Style presets with previews', 'Check available style options'],
          },
          {
            label: 'Privacy controls',
            values: [
              'User-managed deletions and retention policies',
              'Review data retention policies',
            ],
          },
        ],
        note: 'Pricing and features change. Verify on the Aragon AI site before publishing or purchasing.',
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
    ],
    cta: {
      title: 'Ready to compare results?',
      description: 'Generate your own headshots and see the difference.',
      label: 'Try AI Profile Photo Maker',
      href: '/pricing',
    },
  },
  'compare-headshotpro': {
    slug: 'compare/headshotpro',
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
        columns: ['AI Profile Photo Maker', 'HeadshotPro (verify current details)'],
        rows: [
          {
            label: 'Setup steps',
            values: [
              'Upload selfies, select styles, download results',
              'Check current onboarding steps',
            ],
          },
          {
            label: 'Output style',
            values: ['Realistic, LinkedIn-ready headshots', 'Review current style and positioning'],
          },
          {
            label: 'Pricing model',
            values: [
              'Credits and packages with clear options',
              'Verify pricing tiers on provider site',
            ],
          },
          {
            label: 'Turnaround expectations',
            values: ['Minutes after upload for most users', 'Check current delivery estimates'],
          },
          {
            label: 'Team use',
            values: [
              'Individuals and teams with consistent styling',
              'Review team offering details',
            ],
          },
        ],
        note: 'Pricing and features change. Verify on the HeadshotPro site before publishing or purchasing.',
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
    title: 'AI Headshot Features Built for Professional Profiles',
    description:
      'Explore AI Profile Photo Maker features: realistic headshots, fast turnaround, privacy-first processing, and flexible styles.',
    keywords:
      'AI headshot features, professional headshot tools, AI photo enhancement features, headshot generator features',
    h1: 'AI Headshot Features Built for Professional Profiles',
    hero: {
      eyebrow: 'Features',
      headline: 'AI Headshot Features Built for Professional Profiles',
      subhead: 'Realistic headshots, fast turnaround, and privacy-first processing.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'How it works',
      secondaryCtaHref: '/how-it-works',
    },
    sections: [
      {
        type: 'cards',
        title: 'Core capabilities',
        items: [
          {
            title: 'AI-powered enhancement',
            description: 'Transforms casual photos into polished headshots with natural detail.',
          },
          {
            title: 'Multiple style options',
            description: 'Choose from LinkedIn, classic, or creative looks.',
          },
          {
            title: 'Fast delivery',
            description: 'Most headshot sets are ready within minutes.',
          },
        ],
      },
      {
        type: 'cards',
        title: 'Built for professionals',
        items: [
          {
            title: 'Privacy-first processing',
            description: 'Encrypted uploads and user-controlled deletion options.',
          },
          {
            title: 'High-resolution outputs',
            description: 'Headshots that look sharp across digital and print use cases.',
          },
          {
            title: 'Cross-platform ready',
            description: 'Use your headshots on LinkedIn, resumes, and portfolios.',
          },
        ],
      },
    ],
    relatedLinks: [
      { label: 'Examples', href: '/examples' },
      { label: 'AI headshot generator', href: '/ai-headshot-generator' },
      { label: 'Pricing', href: '/pricing' },
    ],
    cta: {
      title: 'Unlock professional headshots with AI',
      description: 'Pick a style and get a full set of headshots in minutes.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
  help: {
    slug: 'help',
    title: 'Help and FAQ | AI Profile Photo Maker',
    description:
      'Get answers to common questions about AI Profile Photo Maker, including upload tips, privacy, and turnaround times.',
    keywords: 'AI headshot help, headshot FAQ, support, AI profile photo maker help',
    h1: 'Help and FAQ',
    hero: {
      eyebrow: 'Help',
      headline: 'Help and FAQ',
      subhead: 'Answers to common questions about uploads, privacy, and results.',
      ctaLabel: 'Get your headshot in minutes',
      ctaHref: '/pricing',
      secondaryCtaLabel: 'How it works',
      secondaryCtaHref: '/how-it-works',
    },
    sections: [
      {
        type: 'faq',
        title: 'Frequently asked questions',
        items: [
          {
            question: 'What photos work best?',
            answer:
              'Use clear selfies with good lighting and minimal filters. Variety in angles helps the model.',
          },
          {
            question: 'How long does it take?',
            answer: 'Most users receive results within minutes after upload.',
          },
          {
            question: 'Is my data safe?',
            answer: 'Uploads are encrypted and you can delete your data at any time.',
          },
          {
            question: 'Can I regenerate styles later?',
            answer: 'Yes. You can generate new styles whenever you want to refresh your headshot.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Need more help?',
        items: [
          'Visit the in-app support center after you sign in.',
          'Review privacy and retention details in our legal pages.',
        ],
      },
    ],
    relatedLinks: [
      { label: 'Privacy policy', href: '/legal/privacy' },
      { label: 'Data retention policy', href: '/legal/retention-policy' },
      { label: 'AI transparency', href: '/legal/ai-transparency' },
    ],
    cta: {
      title: 'Ready to get started?',
      description: 'Create your headshots in minutes with a simple upload flow.',
      label: 'Get your headshot in minutes',
      href: '/pricing',
    },
  },
};
