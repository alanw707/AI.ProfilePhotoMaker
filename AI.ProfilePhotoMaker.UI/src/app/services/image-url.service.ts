import { Injectable } from '@angular/core';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root',
})
export class ImageUrlService {
  constructor(private config: ConfigService) {}

  private getApiOrigin(): string | null {
    const apiUrl = this.config.getApiUrl();
    if (apiUrl?.startsWith('http')) {
      try {
        return new URL(apiUrl).origin;
      } catch {
        // fall through
      }
    }

    try {
      const oauthBaseUrl = this.config.getOAuthBaseUrl();
      return oauthBaseUrl?.startsWith('http') ? new URL(oauthBaseUrl).origin : null;
    } catch {
      return null;
    }
  }
  /**
   * Convert absolute image URLs to relative proxy URLs
   * This fixes the issue where API returns absolute URLs that bypass the proxy
   */
  normalizeImageUrl(url: string): string {
    if (!url) {
      return url;
    }

    // If it's already a relative URL, return as-is
    if (url.startsWith('/')) {
      const apiOrigin = this.getApiOrigin();
      // In docker-local, there is no Angular proxy; route Azurite-style paths through the API so StorageProxyMiddleware can serve them.
      if (apiOrigin && apiOrigin !== window.location.origin && url.startsWith('/devstoreaccount')) {
        return `${apiOrigin}${url}`;
      }

      return url;
    }

    // If it's an absolute URL, extract the path portion
    try {
      const urlObj = new URL(url);

      // Decide normalization strategy based on environment
      const env = this.config.getCurrentEnvironment();
      const host = urlObj.host.toLowerCase();

      // In production/test, keep absolute Azure Blob URLs to avoid 404s
      if (env === 'production' || env === 'test') {
        return url; // do not rewrite
      }

      // In localhost/ngrok development, rewrite only local/Azurite URLs to proxy through the app
      const isLocalHost = host.includes('localhost') || host.startsWith('127.0.0.1');
      if (env === 'localhost' || env === 'ngrok') {
        const isAzuriteHost =
          host.startsWith('azurite') || host.includes('devstoreaccount1');
        const isAzuritePath = urlObj.pathname.startsWith('/devstoreaccount');

        // For Azurite/local blob URLs, return pathname so Angular proxy can handle `/devstoreaccount1/...`
        if (isAzuritePath && (isLocalHost || isAzuriteHost)) {
          const apiOrigin = this.getApiOrigin();
          // If we're running without a proxy (docker-local), route through the API origin.
          if (apiOrigin && apiOrigin !== window.location.origin && urlObj.pathname.startsWith('/devstoreaccount')) {
            return `${apiOrigin}${urlObj.pathname}`;
          }

          return urlObj.pathname;
        }
      }

      // Default: keep as-is
      return url;
    } catch (error) {
      console.warn('Failed to normalize image URL:', url, error);
      return url;
    }
  }

  /**
   * Apply URL normalization to an array of image objects
   */
  normalizeImageUrls<T extends { url?: string; thumbnailUrl?: string }>(images: T[]): T[] {
    return images.map(image => ({
      ...image,
      url: image.url ? this.normalizeImageUrl(image.url) : image.url,
      thumbnailUrl: image.thumbnailUrl
        ? this.normalizeImageUrl(image.thumbnailUrl)
        : image.thumbnailUrl,
    }));
  }
}
