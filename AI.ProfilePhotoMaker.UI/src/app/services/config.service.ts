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

  get authLoginUrl(): string {
    return '/api/auth/login';
  }

  get authRegisterUrl(): string {
    return '/api/auth/register';
  }

  get authProfileCompletionUrl(): string {
    return '/api/auth/profile-completion-status';
  }

  get profileUrl(): string {
    return '/api/profile';
  }

  get uploadPhotoUrl(): string {
    return '/api/image/upload-photo';
  }

  get generateImageUrl(): string {
    return '/api/image/generate-profile-picture';
  }

  get generateSamplesUrl(): string {
    return '/api/image/generate-samples';
  }

  get userImagesUrl(): string {
    return '/api/image/user-images';
  }

  get activeStylesUrl(): string {
    return '/api/style';
  }

  get generateBasicUrl(): string {
    return '/api/replicate/generate/basic';
  }

  get replicateCreditsUrl(): string {
    return '/api/test/basic-tier-status';
  }

  get imageStylesUrl(): string {
    return '/api/image/styles';
  }

  get imageListUrl(): string {
    return '/api/image/images';
  }

  get profileTrainingStatusUrl(): string {
    return '/api/profile/training-status';
  }

  /**
   * Get the OAuth base URL for external login
   */
  getOAuthBaseUrl(): string {
    // For ngrok configuration, use the backend API URL
    if (environment.apiUrl && environment.apiUrl.startsWith('https://')) {
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
    return this.appBaseUrl;
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
}
