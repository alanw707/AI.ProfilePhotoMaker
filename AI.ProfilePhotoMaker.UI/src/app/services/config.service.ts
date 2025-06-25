import { Injectable } from '@angular/core';

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
      uploadImages: string;
      images: string;
      trainingStatus: string;
      trainingFiles: string;
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

@Injectable({
  providedIn: 'root'
})
export class ConfigService {
  private readonly config: ApiConfig = {
    baseUrl: 'http://localhost:5035/api',
    endpoints: {
      auth: {
        login: '/auth/login',
        register: '/auth/register',
        externalLogin: '/auth/external-login'
      },
      profile: {
        base: '/profile',
        uploadImages: '/profile/upload',
        images: '/profile/images',
        trainingStatus: '/profile/training-status',
        trainingFiles: '/profile/training-files'
      },
      replicate: {
        train: '/replicate/train',
        generate: '/replicate/generate',
        generateBasic: '/replicate/generate/basic',
        credits: '/replicate/credits'
      },
      styles: {
        base: '/style',
        active: '/style',
        userSelected: '/style/user-selected',
        select: '/style/select'
      }
    }
  };

  get apiConfig(): ApiConfig {
    return this.config;
  }

  get baseUrl(): string {
    return this.config.baseUrl;
  }

  getFullUrl(endpoint: string): string {
    return `${this.config.baseUrl}${endpoint}`;
  }

  // Convenience methods for common endpoints
  get authLoginUrl(): string {
    return this.getFullUrl(this.config.endpoints.auth.login);
  }

  get authRegisterUrl(): string {
    return this.getFullUrl(this.config.endpoints.auth.register);
  }

  get profileUrl(): string {
    return this.getFullUrl(this.config.endpoints.profile.base);
  }

  get uploadImagesUrl(): string {
    return this.getFullUrl(this.config.endpoints.profile.uploadImages);
  }

  get replicateCreditsUrl(): string {
    return this.getFullUrl(this.config.endpoints.replicate.credits);
  }

  get generateBasicUrl(): string {
    return this.getFullUrl(this.config.endpoints.replicate.generateBasic);
  }

  get activeStylesUrl(): string {
    return this.getFullUrl(this.config.endpoints.styles.active);
  }

  get appBaseUrl(): string {
    return this.config.baseUrl.replace('/api', '');
  }

  /**
   * Get the external URL that can be accessed by third-party services like Replicate
   * This should be the ngrok URL for local development or the production domain in production
   */
  get externalBaseUrl(): string {
    // Use ngrok URL for external access by third-party services
    return 'https://e195-71-38-148-86.ngrok-free.app';
  }

  getApiUrl(): string {
    return this.appBaseUrl;
  }
}