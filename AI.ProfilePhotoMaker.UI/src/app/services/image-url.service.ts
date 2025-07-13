import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ImageUrlService {
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
      return url;
    }

    // If it's an absolute URL, extract the path portion
    try {
      const urlObj = new URL(url);
      const path = urlObj.pathname;

      // Return the path which will be proxied correctly
      return path;
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
