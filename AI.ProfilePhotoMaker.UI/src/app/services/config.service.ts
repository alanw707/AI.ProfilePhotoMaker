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
      generateFree: string;
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
        uploadImages: '/profile/upload-images',
        images: '/profile/images',
        trainingStatus: '/profile/training-status',
        trainingFiles: '/profile/training-files'
      },
      replicate: {
        train: '/replicate/train',
        generate: '/replicate/generate',
        generateFree: '/replicate/generate/free',
        credits: '/replicate/credits'
      },
      styles: {
        base: '/style',
        active: '/style',
        userSelected: '/profile/style',
        select: '/profile/set-style'
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

  get generateFreeUrl(): string {
    return this.getFullUrl(this.config.endpoints.replicate.generateFree);
  }

  get activeStylesUrl(): string {
    return this.getFullUrl(this.config.endpoints.styles.active);
  }

  get appBaseUrl(): string {
    return this.config.baseUrl.replace('/api', '');
  }
}