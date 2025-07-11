import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
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
    // Always use current origin for OAuth
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
}