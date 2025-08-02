import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, catchError, of, map } from 'rxjs';
import { ConfigService } from './config.service';

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

    // Fetch from API
    const apiUrl = `${this._config.getApiUrl()}/style-preview/url/${encodeURIComponent(styleName)}`;

    return this._http.get<StylePreviewResponse>(apiUrl).pipe(
      map(response => {
        if (response.success && response.url) {
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
    const apiUrl = `${this._config.getApiUrl()}/style-preview/list`;

    this._http
      .get<StylePreviewListResponse>(apiUrl)
      .pipe(
        catchError(error => {
          console.warn('Failed to load style previews:', error);
          return of({ success: false, count: 0, previews: [] } as StylePreviewListResponse);
        })
      )
      .subscribe(response => {
        if (response.success && response.previews) {
          const urlMap = new Map<string, string>();

          response.previews.forEach(preview => {
            this._urlCache.set(preview.style, preview.url);
            urlMap.set(preview.style, preview.url);
          });

          this._allPreviewsSubject.next(urlMap);
        }
      });
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
    return this._urlCache.get(styleName) || this.getFallbackUrl(styleName);
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
    // Return placeholder image API endpoint
    return `/api/placeholder/style-preview`;
  }
}
