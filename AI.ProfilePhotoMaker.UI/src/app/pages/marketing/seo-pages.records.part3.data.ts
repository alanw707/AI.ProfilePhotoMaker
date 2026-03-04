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
      description: 'Upload your selfies and get directory-ready headshots in minutes.',
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
};
