import type { SeoPageContent } from './seo-pages.types';

export const seoPagesPart3: Record<string, SeoPageContent> = {
  'nurse-headshots': {
    slug: 'nurse-headshots',
    ctaIntent: 'headshots',
    title: 'Nurse Headshots | Professional Photos for Nurses & Practitioners',
    description:
      'AI-powered nurse headshots for LinkedIn, hospital directories, and professional profiles. Create a trustworthy and approachable look in minutes.',
    keywords:
      'nurse headshots, nursing headshot, registered nurse photo, nurse practitioner headshot, healthcare professional photo',
    h1: 'Professional Headshots for Nurses',
    hero: {
      eyebrow: 'Nursing Headshots',
      headline: 'Nurse Headshots That Convey Trust & Compassion',
      subhead:
        'Generate polished, friendly, and professional headshots perfect for your nursing career.',
      ctaLabel: 'Get Your Headshots',
      ctaHref: '/pricing',
    },
    highlights: [
      { value: 'Compassionate', label: 'Professional Look' },
      { value: 'Directory-Ready', label: 'For All Platforms' },
      { value: 'Minutes', label: 'Fast Delivery' },
    ],
    sections: [
      {
        type: 'cards',
        title: 'Advance Your Nursing Career with a Professional Image',
        items: [
          {
            title: 'Build Patient Trust',
            description:
              'A warm, professional photo helps build immediate trust with patients and their families.',
          },
          {
            title: 'Enhance Your Professional Profile',
            description:
              'A polished headshot is essential for LinkedIn, certifications, and speaking engagements.',
          },
          {
            title: 'Create a Consistent Team Look',
            description:
              'Ensure a unified and professional look for your entire nursing unit or clinic staff.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Ideal For',
        items: [
          'Hospital and clinic provider directories.',
          'LinkedIn profiles and professional networking sites.',
          'ID badges and internal communications.',
          'Publications and conference speaker bios.',
        ],
      },
      {
        type: 'faq',
        title: 'Frequently Asked Questions',
        items: [
          {
            question: 'What should I wear for my nurse headshot?',
            answer:
              'Solid-colored scrubs or professional business casual attire work best. Aim for a clean and simple look.',
          },
          {
            question: 'Can I get a photo in my scrubs?',
            answer:
              'Yes, our AI can generate professional headshots of you in scrubs or other professional attire.',
          },
          {
            question: 'Is this suitable for NP or DNP profiles?',
            answer:
              'Absolutely. The styles are perfect for Nurse Practitioners, doctoral candidates, and other advanced practice nurses.',
          },
        ],
      },
    ],
    cta: {
      title: 'Ready for a professional nursing headshot?',
      description: 'Upload one clear photo and get a directory-ready headshot instantly.',
      label: 'Create My Nurse Headshot',
      href: '/pricing',
    },
  },
  'teacher-headshots': {
    slug: 'teacher-headshots',
    ctaIntent: 'headshots',
    title: 'Teacher Headshots | Friendly & Professional Photos for Educators',
    description:
      'Create warm, approachable, and professional teacher headshots for school websites, LinkedIn, and classroom materials. AI-powered and ready in minutes.',
    keywords:
      'teacher headshots, educator headshots, school profile photo, professional teacher photo, principal headshot',
    h1: 'Professional Headshots for Teachers & Educators',
    hero: {
      eyebrow: 'Educator Headshots',
      headline: 'Teacher Headshots That Look Approachable & Professional',
      subhead:
        'Generate a polished, friendly photo for your school profile, website, or professional portfolio.',
      ctaLabel: 'Get Your Headshots',
      ctaHref: '/pricing',
    },
    highlights: [
      { value: 'Approachable', label: 'Friendly Look' },
      { value: 'Website-Ready', label: 'For School Profiles' },
      { value: 'Ready in Minutes', label: 'Instant Delivery' },
    ],
    sections: [
      {
        type: 'cards',
        title: 'A Professional Image for Modern Educators',
        items: [
          {
            title: 'Connect with Parents & Students',
            description:
              'A warm, professional photo on the school website or your bio helps build immediate rapport.',
          },
          {
            title: 'Support Your Professional Development',
            description:
              'Perfect for conference bios, publications, and your professional learning network profiles.',
          },
          {
            title: 'Create a Cohesive Faculty Page',
            description:
              'Outfit your entire department or school with consistent, high-quality headshots easily.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Perfect For',
        items: [
          'School and district faculty web pages.',
          'LinkedIn profiles and educator networks.',
          '"Meet the Teacher" night presentations and classroom materials.',
          'Professional portfolios and resumes.',
        ],
      },
      {
        type: 'faq',
        title: 'Educator Headshot Questions',
        items: [
          {
            question: 'What is the best background for a teacher headshot?',
            answer:
              'We offer classic, simple backgrounds that look professional and are not distracting, perfect for any school website.',
          },
          {
            question: 'What should I wear for a teacher headshot?',
            answer:
              'Business casual is a great choice. A simple blouse, collared shirt, or blazer projects professionalism and warmth.',
          },
          {
            question: 'Can administrators and other staff use this?',
            answer:
              'Yes, our service is perfect for teachers, principals, counselors, and all school administrative staff.',
          },
        ],
      },
    ],
    cta: {
      title: 'Ready for a new school year photo?',
      description: 'Get a friendly, professional headshot for your educator profile today.',
      label: 'Create My Teacher Headshot',
      href: '/pricing',
    },
  },
  'linkedin-executive-profile-photo': {
    slug: 'linkedin-executive-profile-photo',
    ctaIntent: 'enhance',
    title: 'LinkedIn Executive Profile Photo Pack | AI Profile Photo Maker',
    description:
      'Create a polished LinkedIn and executive profile photo package with guided AI recipes, best-shot labels, and platform-ready exports.',
    keywords:
      'LinkedIn profile photo, executive headshot, AI LinkedIn photo, professional profile picture pack',
    h1: 'LinkedIn / Executive Profile Photo Pack',
    hero: {
      eyebrow: 'LinkedIn / Executive Pack',
      headline: 'A profile photo workflow for LinkedIn, resumes, and executive bios',
      subhead:
        'Start with a free preview, then use Starter or Pro to generate guided executive-ready candidates and export crops.',
      ctaLabel: 'Start Free Preview',
      ctaHref: '/app/enhance?useCase=linkedin_executive',
      ctaIntent: 'enhance',
    },
    highlights: [
      { value: 'Preview first', label: 'Try before upgrade' },
      { value: 'Starter / Pro', label: 'Existing checkout' },
      { value: 'Export kit', label: 'LinkedIn-ready crops' },
    ],
    sections: [
      {
        type: 'cards',
        title: 'Built for professional presence',
        items: [
          {
            title: 'Executive-ready recipes',
            description:
              'Guided generation emphasizes trustworthy lighting, polished wardrobe, and clean professional backgrounds.',
          },
          {
            title: 'Best-shot labels',
            description:
              'Candidates are labeled for LinkedIn profile, executive presence, and resume/avatar use.',
          },
          {
            title: 'Platform exports',
            description:
              'Download LinkedIn profile, square avatar, resume crop, and high-resolution assets.',
          },
        ],
      },
      {
        type: 'steps',
        title: 'How it works',
        items: [
          {
            title: 'Upload once',
            description: 'The quality gate checks whether your source photo is likely to work.',
          },
          {
            title: 'Generate a free preview',
            description: 'See a watermarked preview before buying Starter or Pro.',
          },
          {
            title: 'Upgrade for candidates and exports',
            description:
              'Starter and Pro keep the current checkout path and add guided candidate recipes.',
          },
        ],
      },
      {
        type: 'showcase',
        title: 'Example output slots',
        intro:
          'Placeholder examples for validating the offer before producing final vertical proof assets.',
        items: [
          {
            title: 'LinkedIn-ready crop',
            description: 'Square profile crop for LinkedIn and avatar use.',
            afterImage: 'assets/marketing/before-after/linkedin-after.jpg',
            afterAlt: 'Example LinkedIn profile photo output',
          },
          {
            title: 'Executive profile look',
            description: 'Premium office or studio look for professional pages.',
            afterImage: 'assets/marketing/before-after/executive-after.jpg',
            afterAlt: 'Example executive profile photo output',
          },
        ],
      },
    ],
    cta: {
      title: 'Create your LinkedIn-ready profile photo',
      description: 'Use the existing Free Preview -> Starter/Pro workflow with LinkedIn guidance.',
      label: 'Start Free Preview',
      href: '/app/enhance?useCase=linkedin_executive',
      ctaIntent: 'enhance',
    },
  },
  'realtor-profile-photo-pack': {
    slug: 'realtor-profile-photo-pack',
    ctaIntent: 'enhance',
    title: 'Realtor Profile Photo Pack | AI Profile Photo Maker',
    description:
      'Create a trust-building realtor profile photo pack for Zillow, Realtor.com, flyers, and social posts using guided AI recipes.',
    keywords:
      'realtor headshot, real estate agent profile photo, Zillow profile photo, realtor marketing photo',
    h1: 'Realtor Profile Photo Pack',
    hero: {
      eyebrow: 'Realtor Pack',
      headline: 'Trust-building profile photos for real estate marketing',
      subhead:
        'Generate warm, polished realtor portraits and export crops for profiles, flyers, and social posts.',
      ctaLabel: 'Start Realtor Preview',
      ctaHref: '/app/enhance?useCase=realtor',
      ctaIntent: 'enhance',
    },
    highlights: [
      { value: 'Zillow-ready', label: 'Profile crop' },
      { value: 'Flyer crop', label: 'Marketing asset' },
      { value: 'Trust labels', label: 'Candidate guidance' },
    ],
    sections: [
      {
        type: 'cards',
        title: 'For agent profiles and local marketing',
        items: [
          {
            title: 'Trust-first recipes',
            description: 'Prompts target approachable, confident, client-facing portraits.',
          },
          {
            title: 'Real estate contexts',
            description:
              'Recipes use modern office, upscale interior, and clean neutral backgrounds.',
          },
          {
            title: 'Realtor exports',
            description:
              'Export square profile crops and flyer-friendly crops from the selected best shot.',
          },
        ],
      },
      {
        type: 'bullets',
        title: 'Good fit for',
        items: [
          'Zillow and Realtor.com profiles.',
          'Brokerage team pages.',
          'Listing flyers and social covers.',
          'Local expert marketing.',
        ],
      },
      {
        type: 'showcase',
        title: 'Example output slots',
        intro: 'Placeholder examples for realtor proof assets while the offer is validated.',
        items: [
          {
            title: 'Realtor profile crop',
            description: 'Trust-building square portrait for Zillow/Realtor profiles.',
            afterImage: 'assets/marketing/before-after/linkedin-after.jpg',
            afterAlt: 'Example realtor profile photo output',
          },
          {
            title: 'Flyer-friendly portrait',
            description: 'Vertical crop suitable for flyers and social posts.',
            afterImage: 'assets/marketing/before-after/set-3-after.png',
            afterAlt: 'Example realtor flyer portrait output',
          },
        ],
      },
    ],
    cta: {
      title: 'Create a realtor-ready profile photo',
      description: 'Start with a free preview, then unlock Starter or Pro candidates.',
      label: 'Start Realtor Preview',
      href: '/app/enhance?useCase=realtor',
      ctaIntent: 'enhance',
    },
  },
  'founder-press-kit-photo-pack': {
    slug: 'founder-press-kit-photo-pack',
    ctaIntent: 'enhance',
    title: 'Founder Press Kit Photo Pack | AI Profile Photo Maker',
    description:
      'Create founder profile photos for press bios, podcasts, websites, and LinkedIn with guided AI recipes and export crops.',
    keywords:
      'founder headshot, founder press kit, entrepreneur profile photo, podcast guest headshot',
    h1: 'Founder / Press Kit Photo Pack',
    hero: {
      eyebrow: 'Founder / Press Kit',
      headline: 'Profile assets for founders, bios, podcasts, and launches',
      subhead:
        'Use guided founder recipes to create polished portraits and export crops for press, podcast, website, and social use.',
      ctaLabel: 'Start Founder Preview',
      ctaHref: '/app/enhance?useCase=founder_press_kit',
      ctaIntent: 'enhance',
    },
    highlights: [
      { value: 'Press bio', label: 'Founder-ready' },
      { value: 'Podcast avatar', label: 'Export preset' },
      { value: 'Banner crop', label: 'Social-ready' },
    ],
    sections: [
      {
        type: 'cards',
        title: 'For high-leverage founder surfaces',
        items: [
          {
            title: 'Press bio presence',
            description: 'Recipes aim for confident, credible, and authentic founder portraits.',
          },
          {
            title: 'Website and podcast assets',
            description: 'Export bio, podcast avatar, and banner crops from the selected shot.',
          },
          {
            title: 'Concierge-ready path',
            description:
              'The workflow can support a later manual review or concierge press kit upsell.',
          },
        ],
      },
      {
        type: 'steps',
        title: 'Founder workflow',
        items: [
          {
            title: 'Choose Founder / Press Kit',
            description: 'The use-case picker tunes copy, recipes, labels, and exports.',
          },
          { title: 'Generate preview', description: 'Confirm the look before upgrading.' },
          {
            title: 'Export assets',
            description: 'Download profile, podcast, website, and banner crops.',
          },
        ],
      },
      {
        type: 'showcase',
        title: 'Example output slots',
        intro:
          'Placeholder examples for founder/press proof assets before final examples are selected.',
        items: [
          {
            title: 'Press bio portrait',
            description: 'Founder-ready portrait for press pages and speaker bios.',
            afterImage: 'assets/marketing/before-after/executive-after.jpg',
            afterAlt: 'Example founder press bio photo output',
          },
          {
            title: 'Podcast avatar crop',
            description: 'Square crop for podcast guest pages and social profiles.',
            afterImage: 'assets/marketing/before-after/linkedin-after.jpg',
            afterAlt: 'Example founder podcast avatar output',
          },
        ],
      },
    ],
    cta: {
      title: 'Create founder-ready profile assets',
      description: 'Start with a free preview and upgrade only when ready.',
      label: 'Start Founder Preview',
      href: '/app/enhance?useCase=founder_press_kit',
      ctaIntent: 'enhance',
    },
  },
};
