import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class ConfigService {
  // Simple configuration service for ngrok/proxy setup
  // All API calls go through the same domain via proxy

  get baseUrl(): string {
    return environment.apiUrl || '/api';
  }

  get appBaseUrl(): string {
    return environment.baseUrl || '';
  }

  readonly authLoginUrl = '/api/auth/login';
  readonly authRegisterUrl = '/api/auth/register';
  readonly authProfileCompletionUrl = '/api/auth/profile-completion-status';
  readonly profileUrl = '/api/profile';
  readonly uploadPhotoUrl = '/api/image/upload-photo';
  readonly generateImageUrl = '/api/image/generate-profile-picture';
  readonly generateSamplesUrl = '/api/image/generate-samples';
  readonly userImagesUrl = '/api/image/user-images';
  readonly activeStylesUrl = '/api/style';
  readonly generateBasicUrl = '/api/replicate/generate/basic';
  readonly replicateCreditsUrl = '/api/test/basic-tier-status';
  readonly imageStylesUrl = '/api/image/styles';
  readonly imageListUrl = '/api/image/images';
  readonly profileTrainingStatusUrl = '/api/profile/training-status';

  /**
   * Get the OAuth base URL for external login
   */
  getOAuthBaseUrl(): string {
    // For ngrok configuration, use the backend API URL
    if (environment.apiUrl?.startsWith('https://')) {
      // Extract base URL from full API URL (remove /api suffix)
      return environment.apiUrl.replace('/api', '');
    }
    // Fallback to current origin for local development
    return window.location.origin;
  }

  /**
   * Get full URL for an endpoint
   */
  getFullUrl(endpoint: string): string {
    // Ensure endpoint starts with /
    if (!endpoint.startsWith('/')) {
      endpoint = '/' + endpoint;
    }

    // For API endpoints, prepend /api if not already present
    if (!endpoint.startsWith('/api/')) {
      endpoint = '/api' + endpoint;
    }

    return endpoint;
  }

  // Simplified properties for compatibility
  get frontendBaseUrl(): string {
    return window.location.origin;
  }

  get externalBaseUrl(): string {
    return window.location.origin;
  }

  getApiUrl(): string {
    return this.baseUrl;
  }

  isExternalAccess(): boolean {
    return !window.location.hostname.includes('localhost');
  }

  isNgrokAccess(): boolean {
    return (
      window.location.hostname.includes('ngrok.app') ||
      window.location.hostname.includes('ngrok.io')
    );
  }

  getCurrentEnvironment(): 'localhost' | 'ngrok' | 'test' | 'production' {
    const hostname = window.location.hostname;

    if (hostname.includes('localhost') || hostname.includes('127.0.0.1')) {
      return 'localhost';
    } else if (hostname.includes('ngrok.app') || hostname.includes('ngrok.io')) {
      return 'ngrok';
    } else if (hostname.includes('test.') || hostname.includes('-test.')) {
      return 'test';
    } else {
      return 'production';
    }
  }

  // Add getOAuthRedirectUrl for compatibility
  getOAuthRedirectUrl(): string {
    return this.getOAuthBaseUrl();
  }

  // Add apiConfig for compatibility with file-upload service
  get apiConfig() {
    return {
      endpoints: {
        image: {
          upload: '/image/upload',
          images: '/image/images',
          createTrainingZip: '/image/create-training-zip',
          trainingZips: '/image/training-zips',
          latestTrainingZip: '/image/latest-training-zip',
        },
      },
    };
  }

  /**
   * Build a complete API endpoint URL
   * @param endpoint - The endpoint path (with or without leading slash)
   * @returns Complete API URL
   */
  buildApiEndpoint(endpoint: string): string {
    const cleanEndpoint = endpoint.startsWith('/') ? endpoint.slice(1) : endpoint;
    return `${this.getApiUrl()}/${cleanEndpoint}`;
  }

  /**
   * Build a static file URL (for uploads, generated images, etc.)
   * @param path - The file path
   * @param baseEndpoint - The base endpoint (uploads, generated, style-previews)
   * @returns Complete static file URL
   */
  buildStaticFileUrl(path: string, baseEndpoint: string): string {
    const cleanPath = path.startsWith('/') ? path.slice(1) : path;
    const cleanBase = baseEndpoint.startsWith('/') ? baseEndpoint.slice(1) : baseEndpoint;
    return `${this.getApiUrl()}/${cleanBase}/${cleanPath}`;
  }

  /**
   * Build URL for uploaded user images
   * @param imagePath - The image path or filename
   * @param thumbnail - Whether to request thumbnail size (if supported)
   * @returns Complete upload URL
   */
  buildUploadUrl(imagePath: string, _thumbnail = false): string {
    const url = this.buildStaticFileUrl(imagePath, 'uploads');
    // Future: Add thumbnail parameter when backend supports it
    // if (thumbnail) url += '?size=thumbnail';
    return url;
  }

  /**
   * Build URL for generated images
   * @param imagePath - The image path or filename
   * @returns Complete generated image URL
   */
  buildGeneratedImageUrl(imagePath: string): string {
    return this.buildStaticFileUrl(imagePath, 'generated');
  }

  /**
   * Build URL for style preview images
   * @param styleName - The style name
   * @returns Complete style preview URL
   */
  buildStylePreviewUrl(styleName: string): string {
    // Map style names to unique filenames that exist on the backend
    // Each style should have its own unique preview image
    const styleFileMap: Record<string, string> = {
      // Database style names (with hyphens - exact matches)
      academic: 'academic',
      artistic: 'artistic',
      author: 'author',
      casual: 'casual',
      consultant: 'consultant',
      corporate: 'corporate',
      creative: 'creative',
      'digital-nomad': 'digital-nomad',
      'edgy-urban': 'edgy-urban',
      entrepreneur: 'entrepreneur',
      executive: 'executive',
      fitness: 'fitness',
      glamour: 'glamour',
      influencer: 'influencer',
      legal: 'legal',
      linkedin: 'linkedin',
      medical: 'medical',
      spiritual: 'spiritual',
      startup: 'startup',
      'tech-professional': 'tech-professional',

      // Legacy fallback mappings (with spaces - for backwards compatibility)
      'professional linkedin': 'linkedin',
      'creative professional': 'creative',
      'corporate executive': 'corporate',
      'casual professional': 'casual',
      'classic headshot': 'corporate',
      'modern professional': 'linkedin',
      'elegant portrait': 'executive',
      'friendly professional': 'consultant',
      'confident leader': 'executive',
      'artistic expression': 'artistic',
      'business casual': 'casual',
      'tech professional': 'tech-professional',
      'senior executive': 'executive',
      'professional consultant': 'consultant',
      'academic professional': 'academic',
      'sales professional': 'consultant',
      'marketing expert': 'creative',
      'finance professional': 'corporate',
      'healthcare professional': 'medical',
      'digital nomad': 'digital-nomad',
      'edgy urban': 'edgy-urban',
    };

    const lowerStyleName = styleName.toLowerCase();
    const fileName = styleFileMap[lowerStyleName] || this._generateUniqueFileName(styleName);

    return `/style-previews/${fileName}.jpg`;
  }

  /**
   * Generate a unique filename for styles not in the map
   * @param styleName - The original style name
   * @returns A unique filename based on the style name
   */
  private _generateUniqueFileName(styleName: string): string {
    return styleName
      .toLowerCase()
      .replace(/[^a-z0-9\s]/g, '') // Remove special characters
      .replace(/\s+/g, '-') // Replace spaces with hyphens
      .trim();
  }

  /**
   * Clean and normalize URL paths
   * @param url - URL to clean
   * @returns Cleaned URL
   */
  cleanUrl(url: string): string {
    return url.replace(/\/+/g, '/').replace(/\/$/, '');
  }

  /**
   * Check if image validation is enabled in the current environment
   * @returns true if image validation should be performed, false to skip
   */
  get isImageValidationEnabled(): boolean {
    return environment.features?.enableImageValidation ?? true;
  }

  /**
   * Check if Replicate credits API calls should be made
   * @returns true if Replicate API should be called, false to skip
   */
  get isReplicateCreditsEnabled(): boolean {
    return environment.features?.enableReplicateCredits ?? true;
  }
}
