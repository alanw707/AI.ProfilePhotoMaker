import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

interface RuntimeClientFeatures {
  openAIHeadshotMvp?: boolean;
  profilePhotoWorkflowOverhaul?: boolean;
  outcomePackagesVisible?: boolean;
  profilePhotoScoreVisible?: boolean;
  creativeStylePackVisible?: boolean;
  premiumAugmentationsVisible?: boolean;
  replicateTrainingFlowVisible?: boolean;
}

interface RuntimeClientConfig {
  features?: RuntimeClientFeatures;
}

@Injectable({
  providedIn: 'root',
})
export class ConfigService {
  private runtimeClientConfig: RuntimeClientConfig | null = null;

  async loadClientConfiguration(): Promise<void> {
    if (typeof window === 'undefined' || typeof fetch === 'undefined') {
      return;
    }

    try {
      const response = await fetch(this.buildEndpointUrl('/config/client'), {
        headers: { Accept: 'application/json' },
        credentials: 'same-origin',
      });

      if (!response.ok) {
        return;
      }

      const payload = (await response.json()) as { success?: boolean; data?: RuntimeClientConfig };
      if (payload.success && payload.data) {
        this.runtimeClientConfig = payload.data;
      }
    } catch (error) {
      console.warn('Runtime client configuration unavailable; using build-time defaults.', error);
    }
  }

  get baseUrl(): string {
    return environment.apiUrl || '/api';
  }

  get turnstileSiteKey(): string {
    return environment.turnstileSiteKey || '';
  }

  get appBaseUrl(): string {
    return environment.baseUrl || '';
  }

  get authLoginUrl(): string {
    // Use buildApiEndpoint so tests can mock getApiUrl() consistently
    return this.buildApiEndpoint('auth/login');
  }

  get authRegisterUrl(): string {
    return this.buildEndpointUrl('/auth/register');
  }

  get authProfileCompletionUrl(): string {
    return this.buildEndpointUrl('/auth/profile-completion-status');
  }

  get authValidateSessionUrl(): string {
    return this.buildEndpointUrl('/auth/validate-session');
  }

  get profileUrl(): string {
    return this.buildEndpointUrl('/profile');
  }

  get uploadPhotoUrl(): string {
    return this.buildEndpointUrl('/image/upload-photo');
  }

  get generateImageUrl(): string {
    return this.buildEndpointUrl('/image/generate-profile-picture');
  }

  get generateSamplesUrl(): string {
    return this.buildEndpointUrl('/image/generate-samples');
  }

  get userImagesUrl(): string {
    return this.buildEndpointUrl('/image/user-images');
  }

  get activeStylesUrl(): string {
    return this.buildEndpointUrl('/style');
  }

  get generateBasicUrl(): string {
    return this.buildEndpointUrl('/replicate/generate/basic');
  }

  get imageStylesUrl(): string {
    return this.buildEndpointUrl('/image/styles');
  }

  get imageListUrl(): string {
    return this.buildEndpointUrl('/image/images');
  }

  get profileTrainingStatusUrl(): string {
    return this.buildEndpointUrl('/model-creation/user/current');
  }

  get feedbackUrl(): string {
    return this.buildEndpointUrl('/feedback');
  }

  /**
   * Build a complete endpoint URL based on environment configuration
   * @param endpoint - API endpoint path (e.g., '/auth/login')
   * @returns Complete URL for the endpoint
   */
  private buildEndpointUrl(endpoint: string): string {
    const apiUrl = environment.apiUrl || '/api';

    // Remove leading slash from endpoint for consistent joining
    const cleanEndpoint = endpoint.startsWith('/') ? endpoint.slice(1) : endpoint;

    // If apiUrl is a full URL (starts with http), use it directly
    if (apiUrl.startsWith('http')) {
      return `${apiUrl}/${cleanEndpoint}`;
    }

    // Otherwise, use relative path (development with proxy)
    return `/api/${cleanEndpoint}`;
  }

  /**
   * Get the OAuth base URL for external login
   * CRITICAL FIX: Use baseUrl from environment for OAuth redirect URI generation
   */
  getOAuthBaseUrl(): string {
    // For production, use the explicit baseUrl from environment
    // This ensures OAuth redirects go to the correct backend domain
    if (environment.baseUrl) {
      return environment.baseUrl;
    }

    // For production/external configuration, use the backend API URL
    if (environment.apiUrl?.startsWith('https://') || environment.apiUrl?.startsWith('http://')) {
      try {
        // Use native URL constructor for bulletproof parsing
        // This prevents the string replacement bug that caused malformed URLs
        const url = new URL(environment.apiUrl);
        // Return protocol + hostname + port (removes path completely)
        return `${url.protocol}//${url.hostname}${url.port ? ':' + url.port : ''}`;
      } catch (error) {
        console.error('🔒 Invalid API URL configuration:', environment.apiUrl, error);
        // Graceful fallback to current origin
        return window.location.origin;
      }
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

    // Normalize by stripping optional leading /api prefix, then delegate to buildEndpointUrl
    const normalized = endpoint.startsWith('/api/') ? endpoint.slice(5) : endpoint.slice(1);
    return this.buildEndpointUrl(normalized);
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

  /**
   * Get OAuth redirect URL - consistent method name for OAuth operations
   * DEPRECATED: Use getOAuthBaseUrl() instead for consistency
   */
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

  get isOpenAIHeadshotMvpEnabled(): boolean {
    return this.getFeatureFlag('openAIHeadshotMvp', !environment.production);
  }

  get isProfilePhotoWorkflowOverhaulEnabled(): boolean {
    return this.getFeatureFlag('profilePhotoWorkflowOverhaul', this.isOpenAIHeadshotMvpEnabled);
  }

  get areOutcomePackagesVisible(): boolean {
    return this.getFeatureFlag(
      'outcomePackagesVisible',
      this.isProfilePhotoWorkflowOverhaulEnabled
    );
  }

  get isProfilePhotoScoreVisible(): boolean {
    return this.getFeatureFlag(
      'profilePhotoScoreVisible',
      this.isProfilePhotoWorkflowOverhaulEnabled
    );
  }

  get isCreativeStylePackVisible(): boolean {
    return this.getFeatureFlag('creativeStylePackVisible', true);
  }

  get arePremiumAugmentationsVisible(): boolean {
    return this.getFeatureFlag(
      'premiumAugmentationsVisible',
      this.isProfilePhotoWorkflowOverhaulEnabled
    );
  }

  get isReplicateTrainingFlowVisible(): boolean {
    return this.getFeatureFlag('replicateTrainingFlowVisible', true);
  }

  private getFeatureFlag(feature: keyof RuntimeClientFeatures, defaultValue: boolean): boolean {
    return (
      this.runtimeClientConfig?.features?.[feature] ??
      environment.features?.[feature] ??
      defaultValue
    );
  }
}
