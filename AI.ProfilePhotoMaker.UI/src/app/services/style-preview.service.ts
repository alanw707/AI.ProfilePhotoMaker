import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, catchError, of, map } from 'rxjs';
import { ConfigService } from './config.service';
import { environment } from '../../environments/environment';

export interface StylePreviewResponse {
  success: boolean;
  styleName: string;
  url: string;
  fileName: string;
}

export interface StylePreviewListResponse {
  success: boolean;
  count: number;
  previews: {
    style: string;
    fileName: string;
    path: string;
    url: string;
    size: number;
  }[];
}

@Injectable({
  providedIn: 'root',
})
export class StylePreviewService {
  private _urlCache = new Map<string, string>();
  private _allPreviewsSubject = new BehaviorSubject<Map<string, string>>(new Map());
  // Cache version for busting browser cache - update this when style previews change
  private readonly _cacheVersion = '20260110';

  constructor(
    private _http: HttpClient,
    private _config: ConfigService
  ) {
    this.loadAllPreviews();
  }

  /**
   * Get the Azure Blob Storage URL for a style preview image
   * @param styleName - The style name
   * @returns Observable with the image URL
   */
  getStylePreviewUrl(styleName: string): Observable<string> {
    // Check cache first
    const cachedUrl = this._urlCache.get(styleName);
    if (cachedUrl) {
      return of(cachedUrl);
    }

    // Use direct storage URL if configured
    const directUrl = this.getDirectStorageUrl(styleName);
    if (directUrl) {
      this._urlCache.set(styleName, directUrl);
      return of(directUrl);
    }

    // Fetch from API as fallback (local development)
    const apiUrl = `${this._config.getApiUrl()}/style-preview/url/${encodeURIComponent(styleName)}`;

    return this._http.get<StylePreviewResponse | null>(apiUrl).pipe(
      map(res => {
        const response = res as any;
        if (response && response.success && response.url) {
          // Cache the URL
          this._urlCache.set(styleName, response.url);
          return response.url;
        }
        // Fallback to placeholder or default image
        return this.getFallbackUrl(styleName);
      }),
      catchError(error => {
        console.warn(`Failed to get style preview URL for ${styleName}:`, error);
        return of(this.getFallbackUrl(styleName));
      })
    );
  }

  /**
   * Load all available style previews and cache their URLs
   */
  private loadAllPreviews(): void {
    // First, pre-populate cache with known style preview URLs from Azure Blob Storage
    this.prePopulateKnownStyles();

    const apiUrl = `${this._config.getApiUrl()}/style-preview/list`;
    this._http
      .get<StylePreviewListResponse | null>(apiUrl)
      .pipe(
        map(response => {
          const urlMap = new Map<string, string>();
          if (!response || !response.success) {
            return urlMap;
          }

          response.previews.forEach(preview => {
            if (preview.style && preview.url) {
              urlMap.set(preview.style, preview.url);
            }
          });

          return urlMap;
        }),
        catchError(error => {
          console.warn('Failed to load style previews:', error);
          return of(new Map<string, string>());
        })
      )
      .subscribe(urlMap => {
        urlMap.forEach((url, style) => {
          this._urlCache.set(style, url);
        });
        this._allPreviewsSubject.next(new Map(this._urlCache));
      });
  }

  /**
   * Pre-populate cache with known style preview URLs
   */
  private prePopulateKnownStyles(): void {
    const knownStyles = [
      'academic',
      'artistic',
      'beach-vibes',
      'casual',
      'creative',
      'digital-native',
      'digital-nomad',
      'edgy-urban',
      'entrepreneur',
      'executive',
      'fitness',
      'fresh',
      'glamour',
      'influencer',
      'linkedin',
      'medical',
      'night-out',
      'retro-wave',
      'startup',
      'tech-professional',
    ];

    const urlMap = new Map<string, string>();

    knownStyles.forEach(styleName => {
      const directUrl = this.getDirectStorageUrl(styleName);
      if (directUrl) {
        this._urlCache.set(styleName, directUrl);
        urlMap.set(styleName, directUrl);
      }
    });

    // Emit the initial known URLs
    this._allPreviewsSubject.next(urlMap);
  }

  /**
   * Get all cached preview URLs
   * @returns Observable with a map of style names to URLs
   */
  getAllPreviewUrls(): Observable<Map<string, string>> {
    return this._allPreviewsSubject.asObservable();
  }

  /**
   * Get a cached URL or fallback
   * @param styleName - The style name
   * @returns The cached URL or fallback URL
   */
  getCachedUrl(styleName: string): string {
    const cachedUrl = this._urlCache.get(styleName);
    if (cachedUrl) {
      return cachedUrl;
    }

    // Try direct Azure Blob Storage URL first
    const directUrl = this.getDirectStorageUrl(styleName);
    if (directUrl) {
      // Cache the direct URL for future use
      this._urlCache.set(styleName, directUrl);
      return directUrl;
    }

    return this.getFallbackUrl(styleName);
  }

  /**
   * Clear the URL cache and reload
   */
  refreshCache(): void {
    this._urlCache.clear();
    this.loadAllPreviews();
  }

  /**
   * Get a fallback URL for when the style preview isn't available
   * @param styleName - The style name
   * @returns Fallback URL
   */
  private getFallbackUrl(styleName: string): string {
    // Try direct Azure Blob Storage URL first
    const directUrl = this.getDirectStorageUrl(styleName);
    if (directUrl) {
      return directUrl;
    }
    // Return placeholder image API endpoint as last resort
    return this._config.getFullUrl('/placeholder/style-preview');
  }

  /**
   * Generate direct Azure Blob Storage URL for style preview
   * @param styleName - The style name
   * @returns Direct Azure Blob Storage URL or null if invalid
   */
  private getDirectStorageUrl(styleName: string): string | null {
    if (!styleName) {
      return null;
    }

    // Convert style name to filename format (matches actual Azure Blob Storage files)
    // Examples: "edgy/urban" -> "edgy-urban.jpg", "tech professional" -> "tech-professional.jpg"
    const fileName = `${styleName.toLowerCase().replace(/[/\s]/g, '-')}.jpg`;

    const configuredBase = (
      environment.azure?.stylePreviewUrl ?? environment.azure?.storageUrl
    )?.trim();
    const normalizedBase = configuredBase?.toLowerCase() ?? '';
    const isInvalidBase =
      !configuredBase ||
      normalizedBase.includes('devstoreaccount1') ||
      normalizedBase.includes('/profile-images') ||
      normalizedBase.startsWith('/devstoreaccount1');

    const proxyBase = this.getStylePreviewProxyBase();
    if (proxyBase) {
      return `${proxyBase}/profile-images/style-previews/${fileName}?v=${this._cacheVersion}`;
    }

    const storageUrl = isInvalidBase
      ? 'https://aipmstv16j74jubocuukg.blob.core.windows.net'
      : configuredBase;

    const cleanBase = storageUrl.endsWith('/') ? storageUrl.slice(0, -1) : storageUrl;
    const baseWithContainer = cleanBase.includes('/style-previews')
      ? cleanBase
      : `${cleanBase}/style-previews`;

    return `${baseWithContainer}/${fileName}?v=${this._cacheVersion}`;
  }

  private getStylePreviewProxyBase(): string | null {
    const configuredBase = (
      ((environment.azure as { backendUrl?: string } | undefined)?.backendUrl ||
        environment.baseUrl ||
        '') as string
    ).trim();
    if (!configuredBase || !configuredBase.startsWith('http')) {
      return null;
    }

    const cleanBase = configuredBase.endsWith('/') ? configuredBase.slice(0, -1) : configuredBase;
    return cleanBase.endsWith('/api') ? cleanBase.slice(0, -4) : cleanBase;
  }
}
