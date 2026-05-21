import { SignupIntent } from './intent-tracking.service';

type IntentSourceSegment =
  | 'linkedin'
  | 'corporate'
  | 'job-search'
  | 'real-estate'
  | 'legal'
  | 'medical'
  | 'education'
  | 'dating'
  | 'generic';

const SOURCE_SEGMENT_MATCHERS: { segment: IntentSourceSegment; pattern: RegExp }[] = [
  { segment: 'linkedin', pattern: /linkedin/ },
  { segment: 'job-search', pattern: /job-search/ },
  { segment: 'real-estate', pattern: /realtor|real-estate/ },
  { segment: 'legal', pattern: /lawyer|attorney|legal/ },
  { segment: 'medical', pattern: /doctor|medical|nurse|health/ },
  { segment: 'education', pattern: /teacher|education|academic/ },
  { segment: 'dating', pattern: /dating/ },
  { segment: 'corporate', pattern: /corporate|professional|executive/ },
];

const HEADSHOT_WELCOME_COPY: Record<
  IntentSourceSegment,
  { title: string; description: string; urgency: string }
> = {
  linkedin: {
    title: 'Ready for your LinkedIn headshots?',
    description:
      'Start with one clear photo, then generate a polished, profile-ready LinkedIn headshot instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your LinkedIn-ready headshots.',
  },
  corporate: {
    title: 'Ready for your corporate headshots?',
    description:
      'Start with one clear photo, then generate a polished business portrait for company profiles and bios instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your corporate headshots.',
  },
  'job-search': {
    title: 'Ready for your job-search headshots?',
    description:
      'Start with one clear photo, then create a recruiter-ready headshot for resumes, applications, and networking instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your job-search headshots.',
  },
  'real-estate': {
    title: 'Ready for your realtor headshots?',
    description:
      'Start with one clear photo, then generate a confident, client-ready headshot for listings and agent pages instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your real-estate headshots.',
  },
  legal: {
    title: 'Ready for your legal profile headshots?',
    description:
      'Start with one clear photo, then create a polished portrait suited for law firm bios and legal directories instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your legal-profile headshots.',
  },
  medical: {
    title: 'Ready for your medical professional headshots?',
    description:
      'Start with one clear photo, then generate a trustworthy portrait for clinic profiles and healthcare directories instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your medical-professional headshots.',
  },
  education: {
    title: 'Ready for your educator headshots?',
    description:
      'Start with one clear photo, then create a warm, professional portrait for faculty pages and school profiles instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your educator headshots.',
  },
  dating: {
    title: 'Ready for your dating app photos?',
    description:
      'Start with one clear photo, then generate an authentic, standout profile photo tailored for dating platforms instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your dating-profile photos.',
  },
  generic: {
    title: 'Ready for your professional headshots?',
    description:
      'Start with one clear photo, then generate a polished professional headshot instantly.',
    urgency:
      'Your 25 free credits are waiting! Complete verification to start your professional headshots.',
  },
};

const HEADSHOT_PREVIEW_IMAGES: Record<IntentSourceSegment, { src: string; alt: string }[]> = {
  linkedin: [
    {
      src: '/assets/marketing/before-after/linkedin-before.jpeg',
      alt: 'Input selfie for AI LinkedIn headshots',
    },
    {
      src: '/assets/marketing/before-after/linkedin-after.jpg',
      alt: 'Professional AI LinkedIn headshot preview',
    },
  ],
  corporate: [
    {
      src: '/assets/marketing/before-after/executive-before.jpeg',
      alt: 'Casual photo before executive headshot styling',
    },
    {
      src: '/assets/marketing/before-after/executive-after.jpg',
      alt: 'Executive AI headshot preview',
    },
  ],
  'job-search': [
    {
      src: '/assets/marketing/before-after/academic2-before.jpg',
      alt: 'Input selfie before job-search headshot styling',
    },
    {
      src: '/assets/marketing/before-after/academic2-after.jpg',
      alt: 'Polished AI headshot for job-search profiles',
    },
  ],
  'real-estate': [
    {
      src: '/assets/marketing/before-after/set-1-before.jpg',
      alt: 'Input image before realtor profile enhancement',
    },
    {
      src: '/assets/marketing/before-after/set-1-after.jpg',
      alt: 'Realtor-style AI headshot preview',
    },
  ],
  legal: [
    {
      src: '/assets/marketing/before-after/set-2-before.jpg',
      alt: 'Input photo before legal profile styling',
    },
    {
      src: '/assets/marketing/before-after/set-2-after.png',
      alt: 'Attorney-style AI headshot preview',
    },
  ],
  medical: [
    {
      src: '/assets/marketing/before-after/medical-before.jpg',
      alt: 'Input selfie before medical profile styling',
    },
    {
      src: '/assets/marketing/before-after/medical.jpg',
      alt: 'Medical professional AI headshot preview',
    },
  ],
  education: [
    {
      src: '/assets/marketing/before-after/academic1-before.jpg',
      alt: 'Input selfie before educator profile styling',
    },
    {
      src: '/assets/marketing/before-after/academic1-after.jpg',
      alt: 'Educator-style AI headshot preview',
    },
  ],
  dating: [
    {
      src: '/assets/marketing/before-after/beach-vibes-before.jpg',
      alt: 'Input selfie before dating profile enhancement',
    },
    {
      src: '/assets/marketing/before-after/beach-vibes-after.jpg',
      alt: 'Dating-profile AI photo preview',
    },
  ],
  generic: [
    {
      src: '/assets/marketing/before-after/linkedin-before.jpeg',
      alt: 'Input selfie for AI headshots',
    },
    {
      src: '/assets/marketing/before-after/linkedin-after.jpg',
      alt: 'Professional AI headshot preview',
    },
  ],
};

const ENHANCE_PREVIEW_IMAGES = [
  {
    src: '/assets/marketing/before-after/academic1-before.jpg',
    alt: 'Original photo before enhancement',
  },
  {
    src: '/assets/marketing/before-after/academic1-after.jpg',
    alt: 'Enhanced photo preview',
  },
];

function resolveSourceSegment(sourcePage: string | undefined): IntentSourceSegment {
  if (!sourcePage) {
    return 'generic';
  }

  for (const matcher of SOURCE_SEGMENT_MATCHERS) {
    if (matcher.pattern.test(sourcePage)) {
      return matcher.segment;
    }
  }

  return 'generic';
}

export function getHeadshotsWelcomeCopy(intent: SignupIntent | null): {
  title: string;
  description: string;
  urgency: string;
} {
  const sourceSegment = resolveSourceSegment(intent?.sourcePage);
  return HEADSHOT_WELCOME_COPY[sourceSegment];
}

export function getIntentPreviewImages(
  intent: SignupIntent | null
): { src: string; alt: string }[] {
  if (intent?.ctaType === 'enhance') {
    return ENHANCE_PREVIEW_IMAGES;
  }

  if (intent?.ctaType === 'headshots') {
    const sourceSegment = resolveSourceSegment(intent.sourcePage);
    return HEADSHOT_PREVIEW_IMAGES[sourceSegment];
  }

  return HEADSHOT_PREVIEW_IMAGES.generic;
}
