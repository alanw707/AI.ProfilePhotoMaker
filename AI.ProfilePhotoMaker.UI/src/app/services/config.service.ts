import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Observable, of } from 'rxjs';

export interface ApiConfig {
  baseUrl: string;
  endpoints: {
    auth: {
      login: string;
      register: string;
      externalLogin: string;
    };
    profile: {
      base: string;
      trainingStatus: string;
    };
    image: {
      base: string;
      upload: string;
      images: string;
      styles: string;
      trainingZips: string;
      createTrainingZip: string;
      latestTrainingZip: string;
    };
    replicate: {
      train: string;
      generate: string;
      generateBasic: string;
      credits: string;
    };
    styles: {
      base: string;
      active: string;
      userSelected: string;
      select: string;
    };
  };
}

export interface BackendConfig {
  appBaseUrl: string;
  apiBaseUrl: string;
  frontendBaseUrl: string;
  environment: string;
  isDevelopment: boolean;
  isTest: boolean;
  isProduction: boolean;
  features: {
    enableAutoUrlDetection: boolean;
    enableExternalAccess: boolean;
    enableConfigurationDebug: boolean;
  };
  oauth: {
    useExternalUrls: boolean;
    redirectBaseUrl: string;
  };
  timestamp: string;
}

export interface ConfigurationStatus {
  isLoaded: boolean;
  isFromBackend: boolean;
  isFromCache: boolean;
  lastUpdated: Date | null;
  error: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class ConfigService {
  // Simple configuration service for ngrok setup
  // All API calls are proxied through the same domain
  
  private readonly endpoints = {
    auth: {
      login: '/auth/login',
      register: '/auth/register',
      externalLogin: '/auth/external-login'
    },
    profile: {
      base: '/profile',
      trainingStatus: '/profile/training-status'
    },
    image: {
      base: '/image',
      upload: '/image/upload',
      images: '/image/images',
      styles: '/image/styles',
      trainingZips: '/image/training-zips',
      createTrainingZip: '/image/create-training-zip',
      latestTrainingZip: '/image/latest-training-zip'
    },
    replicate: {
      train: '/replicate/train',
      generate: '/replicate/generate',
      generateBasic: '/replicate/generate/basic',
      credits: '/test/basic-tier-status'
    },
    styles: {
      base: '/style',
      active: '/style',
      userSelected: '/style/user-selected',
      select: '/style/select'
    }
  };

  get apiConfig(): ApiConfig {
    return {
      baseUrl: this.baseUrl,
      endpoints: this.endpoints
    };
  }

  get baseUrl(): string {
    return environment.apiUrl || '/api';
  }

  get appBaseUrl(): string {
    return environment.baseUrl || '';
  }

  get frontendBaseUrl(): string {
    return window.location.origin;
  }

  get authLoginUrl(): string {
    return environment.authLoginUrl || '/api/auth/login';
  }

  get authRegisterUrl(): string {
    return environment.authRegisterUrl || '/api/auth/register';
  }

  get authProfileCompletionUrl(): string {
    return environment.authProfileCompletionUrl || '/api/auth/profile-completion-status';
  }

  get profileUrl(): string {
    return environment.profileUrl || '/api/profile';
  }

  get uploadPhotoUrl(): string {
    return environment.uploadPhotoUrl || '/api/image/upload-photo';
  }

  get generateImageUrl(): string {
    return environment.generateImageUrl || '/api/image/generate-profile-picture';
  }

  get generateSamplesUrl(): string {
    return environment.generateSamplesUrl || '/api/image/generate-samples';
  }

  get userImagesUrl(): string {
    return environment.userImagesUrl || '/api/image/user-images';
  }

  get configEndpoint(): string {
    return environment.configEndpoint || '/api/config/client';
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

  getApiUrl(): string {
    return this.appBaseUrl;
  }

  get externalBaseUrl(): string {
    // For ngrok setup, external base URL is the same as current origin
    return window.location.origin;
  }

  /**
   * Get the OAuth base URL for external login
   * When using ngrok, this should be the full ngrok URL
   */
  getOAuthBaseUrl(): string {
    // For ngrok setup, we use the current origin since everything is proxied
    const currentOrigin = window.location.origin;
    
    // If we're on ngrok domain, use it directly
    if (currentOrigin.includes('ngrok.app')) {
      return currentOrigin;
    }
    
    // Otherwise use the environment configuration
    return this.appBaseUrl || currentOrigin;
  }

  /**
   * Get OAuth redirect URL (for backwards compatibility)
   */
  getOAuthRedirectUrl(): string {
    return this.getOAuthBaseUrl();
  }

  /**
   * Check if we're running through ngrok
   */
  isNgrokEnvironment(): boolean {
    return window.location.hostname.includes('ngrok.app');
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

  /**
   * Check if running in external access mode
   */
  isExternalAccess(): boolean {
    return this.isNgrokEnvironment() || !window.location.hostname.includes('localhost');
  }

  // Observable properties for compatibility with components expecting them
  config$ = of(this.getBackendConfig());
  status$ = of(this.getConfigurationStatus());

  /**
   * Get backend configuration (mock for ngrok setup)
   */
  private getBackendConfig(): BackendConfig {
    return {
      appBaseUrl: this.appBaseUrl,
      apiBaseUrl: this.baseUrl,
      frontendBaseUrl: this.frontendBaseUrl,
      environment: 'development',
      isDevelopment: true,
      isTest: false,
      isProduction: false,
      features: {
        enableAutoUrlDetection: true,
        enableExternalAccess: true,
        enableConfigurationDebug: false
      },
      oauth: {
        useExternalUrls: this.isExternalAccess(),
        redirectBaseUrl: this.getOAuthBaseUrl()
      },
      timestamp: new Date().toISOString()
    };
  }

  /**
   * Get configuration status (mock for ngrok setup)
   */
  private getConfigurationStatus(): ConfigurationStatus {
    return {
      isLoaded: true,
      isFromBackend: false,
      isFromCache: false,
      lastUpdated: new Date(),
      error: null
    };
  }

  /**
   * Refresh configuration (no-op for ngrok setup)
   */
  refreshConfiguration(): Observable<BackendConfig> {
    return of(this.getBackendConfig());
  }
}