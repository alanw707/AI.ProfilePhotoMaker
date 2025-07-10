import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';

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
  private readonly CACHE_KEY = 'backend_config';
  private readonly CACHE_DURATION = 5 * 60 * 1000; // 5 minutes
  
  private backendConfig: BackendConfig | null = null;
  private configSubject = new BehaviorSubject<BackendConfig | null>(null);
  private statusSubject = new BehaviorSubject<ConfigurationStatus>({
    isLoaded: false,
    isFromBackend: false,
    isFromCache: false,
    lastUpdated: null,
    error: null
  });
  
  public config$ = this.configSubject.asObservable();
  public status$ = this.statusSubject.asObservable();
  
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

  constructor(private http: HttpClient) {
    // Try to load from cache first, then fetch from backend
    this.loadConfigurationFromCache();
    this.fetchBackendConfiguration().subscribe();
  }

  /**
   * Fetch configuration from backend API
   */
  fetchBackendConfiguration(): Observable<BackendConfig> {
    const configUrl = this.getConfigEndpointUrl();
    
    return this.http.get<{success: boolean, data: BackendConfig}>(configUrl).pipe(
      map(response => {
        if (!response.success || !response.data) {
          throw new Error('Invalid configuration response from backend');
        }
        return response.data;
      }),
      tap(config => {
        this.backendConfig = config;
        this.configSubject.next(config);
        this.updateStatus({
          isLoaded: true,
          isFromBackend: true,
          isFromCache: false,
          lastUpdated: new Date(),
          error: null
        });
        this.cacheConfiguration(config);
        
        console.log('✅ Backend configuration loaded:', {
          environment: config.environment,
          appBaseUrl: config.appBaseUrl,
          isExternal: !config.appBaseUrl.includes('localhost')
        });
      }),
      catchError(error => {
        const errorMessage = `Failed to fetch backend configuration: ${error.message}`;
        console.warn('⚠️ Backend configuration fetch failed, using fallback:', errorMessage);
        
        this.updateStatus({
          isLoaded: true,
          isFromBackend: false,
          isFromCache: false,
          lastUpdated: new Date(),
          error: errorMessage
        });
        
        // Return fallback configuration
        return of(this.getFallbackConfiguration());
      })
    );
  }

  /**
   * Load configuration from localStorage cache
   */
  private loadConfigurationFromCache(): void {
    try {
      const cached = localStorage.getItem(this.CACHE_KEY);
      if (cached) {
        const { config, timestamp } = JSON.parse(cached);
        const age = Date.now() - timestamp;
        
        if (age < this.CACHE_DURATION) {
          this.backendConfig = config;
          this.configSubject.next(config);
          this.updateStatus({
            isLoaded: true,
            isFromBackend: false,
            isFromCache: true,
            lastUpdated: new Date(timestamp),
            error: null
          });
          
          console.log('📋 Using cached configuration:', {
            age: Math.round(age / 1000) + 's',
            environment: config.environment
          });
        }
      }
    } catch (error) {
      console.warn('Failed to load cached configuration:', error);
    }
  }

  /**
   * Cache configuration to localStorage
   */
  private cacheConfiguration(config: BackendConfig): void {
    try {
      const cacheData = {
        config,
        timestamp: Date.now()
      };
      localStorage.setItem(this.CACHE_KEY, JSON.stringify(cacheData));
    } catch (error) {
      console.warn('Failed to cache configuration:', error);
    }
  }

  /**
   * Get fallback configuration based on environment
   */
  private getFallbackConfiguration(): BackendConfig {
    const currentOrigin = window.location.origin;
    const isExternal = !currentOrigin.includes('localhost');
    
    return {
      appBaseUrl: environment.appBaseUrl,
      apiBaseUrl: environment.apiBaseUrl,
      frontendBaseUrl: isExternal ? currentOrigin : environment.frontendBaseUrl,
      environment: environment.test ? 'test' : (environment.production ? 'production' : 'development'),
      isDevelopment: environment.development || false,
      isTest: environment.test || false,
      isProduction: environment.production || false,
      features: {
        enableAutoUrlDetection: environment.enableAutoUrlDetection || true,
        enableExternalAccess: environment.enableExternalAccess || false,
        enableConfigurationDebug: environment.development || false
      },
      oauth: {
        useExternalUrls: environment.oauth?.useExternalUrls || isExternal,
        redirectBaseUrl: environment.appBaseUrl
      },
      timestamp: new Date().toISOString()
    };
  }

  /**
   * Get the configuration endpoint URL
   */
  private getConfigEndpointUrl(): string {
    const currentOrigin = window.location.origin;
    
    // If accessing via external URL, try current origin first
    if (!currentOrigin.includes('localhost')) {
      return `${currentOrigin}/api/config/client`;
    }
    
    // Check for manually set backend URL
    const storedBackendUrl = localStorage.getItem('BACKEND_URL');
    if (storedBackendUrl) {
      return `${storedBackendUrl}/api/config/client`;
    }
    
    // Default to environment configuration
    return `${environment.appBaseUrl}/api/config/client`;
  }

  private updateStatus(status: Partial<ConfigurationStatus>): void {
    const currentStatus = this.statusSubject.value;
    this.statusSubject.next({ ...currentStatus, ...status });
  }

  // Public getters that use the loaded configuration
  get apiConfig(): ApiConfig {
    return {
      baseUrl: this.baseUrl,
      endpoints: this.endpoints
    };
  }

  get baseUrl(): string {
    return this.backendConfig?.apiBaseUrl || environment.apiBaseUrl;
  }

  get appBaseUrl(): string {
    return this.backendConfig?.appBaseUrl || environment.appBaseUrl;
  }

  get frontendBaseUrl(): string {
    if (this.backendConfig) {
      return this.backendConfig.frontendBaseUrl;
    }
    
    // Fallback to auto-detection
    const currentOrigin = window.location.origin;
    return !currentOrigin.includes('localhost') ? currentOrigin : environment.frontendBaseUrl;
  }

  getFullUrl(endpoint: string): string {
    return `${this.baseUrl}${endpoint}`;
  }

  // Convenience methods for common endpoints
  get authLoginUrl(): string {
    return this.getFullUrl(this.endpoints.auth.login);
  }

  get authRegisterUrl(): string {
    return this.getFullUrl(this.endpoints.auth.register);
  }

  get profileUrl(): string {
    return this.getFullUrl(this.endpoints.profile.base);
  }

  get imageUploadUrl(): string {
    return this.getFullUrl(this.endpoints.image.upload);
  }

  get imageListUrl(): string {
    return this.getFullUrl(this.endpoints.image.images);
  }

  get imageStylesUrl(): string {
    return this.getFullUrl(this.endpoints.image.styles);
  }

  get replicateCreditsUrl(): string {
    return this.getFullUrl(this.endpoints.replicate.credits);
  }

  get generateBasicUrl(): string {
    return this.getFullUrl(this.endpoints.replicate.generateBasic);
  }

  get activeStylesUrl(): string {
    return this.getFullUrl(this.endpoints.styles.active);
  }

  /**
   * Get the external URL that can be accessed by third-party services like Replicate
   */
  get externalBaseUrl(): string {
    return this.backendConfig?.appBaseUrl || environment.appBaseUrl;
  }

  getApiUrl(): string {
    return this.appBaseUrl;
  }

  /**
   * Check if the current access is external (not localhost)
   */
  isExternalAccess(): boolean {
    const currentOrigin = window.location.origin;
    return !currentOrigin.includes('localhost') && !currentOrigin.includes('127.0.0.1');
  }

  /**
   * Get the OAuth redirect URL - uses backend configuration or fallback
   */
  getOAuthRedirectUrl(): string {
    if (this.backendConfig) {
      return this.backendConfig.oauth.useExternalUrls ? this.frontendBaseUrl : this.appBaseUrl;
    }
    
    // Fallback logic
    const isExternal = this.isExternalAccess();
    return isExternal ? this.frontendBaseUrl : this.appBaseUrl;
  }

  /**
   * Manually set backend URL for debugging/override
   */
  setBackendUrl(url: string): void {
    localStorage.setItem('BACKEND_URL', url);
    // Force refresh configuration
    this.fetchBackendConfiguration().subscribe();
    console.log('Backend URL updated to:', url);
  }

  /**
   * Clear cached configuration and refresh from backend
   */
  refreshConfiguration(): Observable<BackendConfig> {
    localStorage.removeItem(this.CACHE_KEY);
    return this.fetchBackendConfiguration();
  }

  /**
   * Get current configuration status
   */
  getConfigurationStatus(): ConfigurationStatus {
    return this.statusSubject.value;
  }

  /**
   * Get current backend configuration
   */
  getCurrentConfig(): BackendConfig | null {
    return this.backendConfig;
  }

  /**
   * Wait for configuration to be loaded
   */
  waitForConfiguration(): Observable<BackendConfig> {
    if (this.backendConfig) {
      return of(this.backendConfig);
    }
    
    return this.config$.pipe(
      map(config => {
        if (!config) {
          throw new Error('Configuration not available');
        }
        return config;
      })
    );
  }
}