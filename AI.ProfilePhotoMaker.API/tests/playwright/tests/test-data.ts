/**
 * Test data for style preview validation tests
 */

export interface StylePreviewTestCase {
  styleName: string;
  fileName: string;
  expectedUrl: string;
  description: string;
  category: 'professional' | 'creative' | 'lifestyle' | 'specialized';
}

export const AZURE_BLOB_BASE_URL = 'https://aipmstv16j74jubocuukg.blob.core.windows.net/style-previews';
export const FRONTEND_APP_URL = 'https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io';

/**
 * Complete list of style preview test cases
 */
export const STYLE_PREVIEW_TEST_CASES: StylePreviewTestCase[] = [
  // Professional Styles
  {
    styleName: 'corporate',
    fileName: 'corporate.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/corporate.jpg`,
    description: 'Corporate professional headshot style',
    category: 'professional'
  },
  {
    styleName: 'executive',
    fileName: 'executive.jpg', 
    expectedUrl: `${AZURE_BLOB_BASE_URL}/executive.jpg`,
    description: 'Executive leadership headshot style',
    category: 'professional'
  },
  {
    styleName: 'consultant',
    fileName: 'consultant.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/consultant.jpg`,
    description: 'Professional consultant headshot style',
    category: 'professional'
  },
  {
    styleName: 'linkedin',
    fileName: 'linkedin.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/linkedin.jpg`,
    description: 'LinkedIn-optimized professional headshot',
    category: 'professional'
  },
  {
    styleName: 'legal',
    fileName: 'legal.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/legal.jpg`,
    description: 'Legal professional headshot style',
    category: 'professional'
  },
  {
    styleName: 'medical',
    fileName: 'medical.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/medical.jpg`,
    description: 'Medical professional headshot style',
    category: 'specialized'
  },
  {
    styleName: 'academic',
    fileName: 'academic.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/academic.jpg`,
    description: 'Academic professional headshot style',
    category: 'specialized'
  },

  // Business & Entrepreneurial
  {
    styleName: 'entrepreneur',
    fileName: 'entrepreneur.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/entrepreneur.jpg`,
    description: 'Entrepreneur headshot style',
    category: 'professional'
  },
  {
    styleName: 'startup',
    fileName: 'startup.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/startup.jpg`,
    description: 'Startup professional headshot style',
    category: 'professional'
  },
  {
    styleName: 'tech-professional',
    fileName: 'tech-professional.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/tech-professional.jpg`,
    description: 'Technology professional headshot style',
    category: 'professional'
  },

  // Creative & Lifestyle
  {
    styleName: 'creative',
    fileName: 'creative.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/creative.jpg`,
    description: 'Creative professional headshot style',
    category: 'creative'
  },
  {
    styleName: 'artistic',
    fileName: 'artistic.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/artistic.jpg`,
    description: 'Artistic headshot style',
    category: 'creative'
  },
  {
    styleName: 'author',
    fileName: 'author.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/author.jpg`,
    description: 'Author headshot style',
    category: 'creative'
  },
  {
    styleName: 'influencer',
    fileName: 'influencer.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/influencer.jpg`,
    description: 'Social media influencer headshot style',
    category: 'lifestyle'
  },
  {
    styleName: 'casual',
    fileName: 'casual.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/casual.jpg`,
    description: 'Casual lifestyle headshot style',
    category: 'lifestyle'
  },
  {
    styleName: 'digital-nomad',
    fileName: 'digital-nomad.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/digital-nomad.jpg`,
    description: 'Digital nomad lifestyle headshot',
    category: 'lifestyle'
  },

{
    styleName: 'glamour',
    fileName: 'glamour.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/glamour.jpg`,
    description: 'Glamour headshot style',
    category: 'creative'
  },
  {
    styleName: 'edgy-urban',
    fileName: 'edgy-urban.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/edgy-urban.jpg`,
    description: 'Edgy urban headshot style',
    category: 'creative'
  },

  // Specialized Categories
  {
    styleName: 'fitness',
    fileName: 'fitness.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/fitness.jpg`,
    description: 'Fitness professional headshot style',
    category: 'specialized'
  },
  {
    styleName: 'spiritual',
    fileName: 'spiritual.jpg',
    expectedUrl: `${AZURE_BLOB_BASE_URL}/spiritual.jpg`,
    description: 'Spiritual/wellness headshot style',
    category: 'specialized'
  }
];

/**
 * Performance thresholds for testing
 */
export const PERFORMANCE_THRESHOLDS = {
  imageLoadTimeout: 10000, // 10 seconds max for image loading
  apiResponseTimeout: 5000, // 5 seconds max for API responses
  maxImageSize: 5 * 1024 * 1024, // 5MB max file size
  minImageWidth: 100, // Minimum image width in pixels
  minImageHeight: 100, // Minimum image height in pixels
  targetLoadTime: 3000, // Target load time under 3 seconds
};

/**
 * Expected HTTP status codes
 */
export const HTTP_STATUS = {
  OK: 200,
  NOT_FOUND: 404,
  FORBIDDEN: 403,
  SERVER_ERROR: 500,
} as const;

/**
 * Test scenarios configuration
 */
export const TEST_SCENARIOS = {
  preUpload: {
    name: 'Pre-Upload State Validation',
    expectedStatus: HTTP_STATUS.NOT_FOUND,
    description: 'Validates that images return 404 before upload'
  },
  postUpload: {
    name: 'Post-Upload Verification',
    expectedStatus: HTTP_STATUS.OK,
    description: 'Validates that images load correctly after upload'
  }
} as const;