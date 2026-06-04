export interface PhotoWorkspaceImageView {
  url: string;
  displayUrl: string;
  type?: string;
  processedImageId?: number;
  storagePath?: string;
  fallbackAttempted?: boolean;
  loadFailed?: boolean;
  previousDisplayUrl?: string;
  augmentationLabel?: string;
}

export interface PhotoWorkspaceImageDisplayAdapter {
  toApiImageUrl(path: string): string;
}

/**
 * Deep Image display/fallback module for the Photo workspace.
 *
 * Interface: callers provide raw image URL + optional storage path, then ask for a display URL
 * or next fallback state. Implementation owns data URL, profile-images proxy, and broken-image
 * fallback rules so Before/After callers share one seam.
 */
export class PhotoWorkspaceImageViewModule {
  constructor(private readonly adapter: PhotoWorkspaceImageDisplayAdapter) {}

  createImageView(
    url: string,
    type?: string,
    processedImageId?: number,
    storagePath?: string
  ): PhotoWorkspaceImageView {
    return {
      url,
      displayUrl: this.normalizeDisplayImageUrl(url, storagePath),
      type,
      processedImageId,
      storagePath,
    };
  }

  normalizeDisplayImageUrl(url: string, storagePath?: string): string {
    if (!url) {
      return this.getStorageProxyUrl(storagePath) ?? '';
    }

    if (url.startsWith('data:image/')) {
      return url;
    }

    const storageProxyUrl = this.getStorageProxyUrl(storagePath);
    try {
      const parsed = new URL(url, window.location.origin);
      if (parsed.pathname.startsWith('/profile-images/')) {
        return this.adapter.toApiImageUrl(`${parsed.pathname}${parsed.search}`);
      }
    } catch {
      // Use storage proxy fallback below.
    }

    return url.startsWith('/') ? this.adapter.toApiImageUrl(url) : (storageProxyUrl ?? url);
  }

  getStorageProxyUrl(storagePath?: string): string | null {
    if (!storagePath) {
      return null;
    }

    const normalizedPath = storagePath.replace(/^\/+/, '');
    return this.adapter.toApiImageUrl(`/profile-images/${normalizedPath}`);
  }

  nextFailedImageState(image: PhotoWorkspaceImageView): PhotoWorkspaceImageView {
    if (image.loadFailed) {
      return image;
    }

    const fallbackUrl = this.getStorageProxyUrl(image.storagePath);
    if (!image.fallbackAttempted && fallbackUrl && fallbackUrl !== image.displayUrl) {
      return {
        ...image,
        displayUrl: fallbackUrl,
        fallbackAttempted: true,
      };
    }

    return {
      ...image,
      loadFailed: true,
    };
  }
}
