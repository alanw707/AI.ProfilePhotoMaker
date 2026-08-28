import { ChangeDetectorRef, Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import {
  GalleryImage,
  PhotoGalleryComponent,
} from '../../components/photo-gallery/photo-gallery.component';
import { FileUploadService, ProcessedImage } from '../../services/file-upload.service';
import jsZip from 'jszip';
import { ConfigService } from '../../services/config.service';
import { LoggingService, LogLevel } from '../../services/logging.service';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';
import { HeadshotGenerationService } from '../../services/headshot-generation.service';

@Component({
  selector: 'app-gallery',
  standalone: true,
  imports: [CommonModule, RouterModule, PhotoGalleryComponent, HeaderNavigationComponent],
  templateUrl: './gallery.component.html',
  styleUrls: ['./gallery.component.sass'],
})
export class GalleryComponent implements OnInit {
  @ViewChild('photoGallery') photoGallery!: PhotoGalleryComponent;

  galleryImages: GalleryImage[] = [];
  isLoading = false;
  isDownloading = false;
  downloadProgress = 0;
  private _hasRunInitialRepair = false;

  constructor(
    private _authService: AuthService,
    private _router: Router,
    private _route: ActivatedRoute,
    private _fileUploadService: FileUploadService,
    private _cdr: ChangeDetectorRef,
    private _config: ConfigService,
    private _logger: LoggingService,
    private _headshotGenerationService: HeadshotGenerationService
  ) {}

  ngOnInit() {
    if (!this._authService.isAuthenticated()) {
      this._router.navigate(['/auth/login']);
      return;
    }

    // Check for refresh parameter
    this._route.queryParams.subscribe(params => {
      if (params['refresh']) {
        this._logger.conditionalLog(
          environment.features.logging?.enableGalleryDebug ?? false,
          LogLevel.DEBUG,
          'Gallery refresh requested via query parameter'
        );
        this.loadImages(true);
      } else {
        this.loadImages();
      }
    });
  }

  async loadImages(forceRefresh = false) {
    const enableDebug = environment.features.logging?.enableGalleryDebug ?? false;

    this._logger.conditionalLog(enableDebug, LogLevel.DEBUG, 'Gallery loadImages START', {
      isLoading: this.isLoading,
      forceRefresh,
      galleryImagesCount: this.galleryImages.length,
    });

    this.isLoading = true;
    try {
      // UI-triggered repair removed to prevent unintended deletions.

      const response = await firstValueFrom(this._fileUploadService.getUserImages(forceRefresh));

      this._logger.conditionalLog(enableDebug, LogLevel.DEBUG, 'API Response received', {
        success: response?.success,
        hasData: !!response?.data,
        totalImages: response?.data?.totalImages,
        imagesCount: response?.data?.images?.length,
      });

      if (response?.success && response.data) {
        // Deduplicate images by ID to prevent duplicates in zip downloads
        const uniqueImages = response.data.images.filter(
          (img, index, array) => array.findIndex(i => i.id === img.id) === index
        );

        this.galleryImages = uniqueImages
          .map((img: ProcessedImage) => {
            // Detect enhanced images by style markers
            const styleLower = (img.style || '').toLowerCase();
            const isEnhanced =
              styleLower.startsWith('enhanced') ||
              img.style === 'Background Remover' ||
              img.style === 'Social Media' ||
              img.style === 'Cartoon' ||
              styleLower.startsWith('enhancement:');

            // For generated/enhanced, prioritize processedImageUrl; for uploads, prefer original
            const preferredUrl =
              img.isGenerated || isEnhanced
                ? img.processedImageUrl || img.originalImageUrl
                : img.originalImageUrl || img.processedImageUrl;

            if (!preferredUrl) {
              this._logger.warn('Skipping image with no valid URL', {
                id: img.id,
                isGenerated: img.isGenerated,
                style: img.style,
              });
              return null;
            }

            const type = isEnhanced
              ? ('enhanced' as const)
              : img.isGenerated
                ? ('generated' as const)
                : ('original' as const);

            const styleDisplay = this._getDisplayStyleLabel(
              img.style,
              !!img.isGenerated && !isEnhanced
            );

            const title = isEnhanced
              ? 'Enhanced Photo'
              : img.isGenerated
                ? `${styleDisplay} Photo`
                : 'Uploaded Photo';

            const description = isEnhanced
              ? 'AI-enhanced photo'
              : img.isGenerated
                ? `Generated ${styleDisplay} style profile photo`
                : 'Original uploaded image';

            return {
              id: img.id,
              url: preferredUrl,
              thumbnailUrl: preferredUrl,
              title,
              description,
              style: img.style || 'original',
              createdAt: new Date(img.createdAt),
              status: 'completed' as const,
              type,
              downloadUrl: preferredUrl,
              canResumePreview: false,
            };
          })
          .filter(img => img !== null) as any[];

        const resumableIds = await Promise.all(
          response.data.images
            .filter(image => image.generationMode === 'instant_headshot')
            .slice(0, 3)
            .map(async image => {
              try {
                const preview = await firstValueFrom(
                  this._headshotGenerationService.getResumablePreview(image.id)
                );
                return preview.success && preview.data?.hasRawPreview ? image.id : null;
              } catch {
                return null;
              }
            })
        );
        const resumableIdSet = new Set(resumableIds.filter((id): id is number => id !== null));
        this.galleryImages = this.galleryImages.map(image => ({
          ...image,
          canResumePreview: resumableIdSet.has(image.id),
        }));

        this._logger.conditionalLog(enableDebug, LogLevel.DEBUG, 'Gallery images processed', {
          uniqueImages: uniqueImages.length,
          galleryImages: this.galleryImages.length,
          firstImage: this.galleryImages[0],
        });

        // Log if duplicates were found and removed
        if (uniqueImages.length < response.data.images.length) {
          this._logger.debug(
            `Removed ${response.data.images.length - uniqueImages.length} duplicate images from display`
          );
        }

        // Log if images were filtered out due to missing URLs
        if (this.galleryImages.length < uniqueImages.length) {
          this._logger.warn(
            `Filtered out ${uniqueImages.length - this.galleryImages.length} images with missing URLs`
          );
        }

        // Force change detection after processing images
        this._cdr.detectChanges();
      }
    } catch (error) {
      this._logger.error('Failed to load images', error);
    } finally {
      this.isLoading = false;
      this._cdr.detectChanges(); // Force Angular to update UI

      this._logger.conditionalLog(enableDebug, LogLevel.DEBUG, 'Gallery loadImages COMPLETE', {
        isLoading: this.isLoading,
        galleryImagesCount: this.galleryImages.length,
      });
    }
  }

  refreshGallery() {
    this.loadImages(true);
  }

  get resumablePreviewImages(): GalleryImage[] {
    return this.galleryImages.filter(
      image => (image as GalleryImage & { canResumePreview?: boolean }).canResumePreview
    );
  }

  continueInPhotoWorkspace(image: GalleryImage): void {
    this._router.navigate(['/app/enhance'], { queryParams: { resumePreviewId: image.id } });
  }

  async abandonPreview(image: GalleryImage): Promise<void> {
    if (!confirm('Abandon this unfinished preview session? Your saved photos stay available.')) {
      return;
    }

    try {
      await firstValueFrom(this._headshotGenerationService.abandonPreview(image.id));
      this.galleryImages = this.galleryImages.filter(candidate => candidate.id !== image.id);
      this._cdr.detectChanges();
    } catch {
      this._logger.warn('Unable to abandon preview session', { imageId: image.id });
    }
  }

  onImageClick(image: GalleryImage) {
    window.open(image.url, '_blank');
  }

  async testImageAccess(image: GalleryImage) {
    const imageUrl = image.downloadUrl || image.url;

    try {
      const response = await fetch(imageUrl, {
        method: 'HEAD',
        mode: 'cors',
        cache: 'no-cache',
      });

      return response.ok;
    } catch {
      // Try with img element to test basic accessibility
      return new Promise(resolve => {
        const img = new Image();
        img.onload = () => resolve(true);
        img.onerror = () => resolve(false);
        img.src = imageUrl;
      });
    }
  }

  async onImageDownload(image: GalleryImage) {
    // First test if the image is accessible
    const isAccessible = await this.testImageAccess(image);
    if (!isAccessible) {
      alert('This image appears to be inaccessible. It may have expired or been moved.');
      return;
    }

    try {
      const imageUrl = image.downloadUrl || image.url;

      // Try fetch with CORS mode (required for blob downloads)
      const env = this._config.getCurrentEnvironment();
      const isProdLike = env === 'production' || env === 'test';
      const headers = isProdLike
        ? {}
        : ({ 'ngrok-skip-browser-warning': 'true' } as Record<string, string>);

      const response = await fetch(imageUrl, {
        mode: 'cors',
        cache: 'no-cache',
        credentials: 'omit',
        headers,
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const contentType = response.headers.get('content-type') || '';
      const blob = await response.blob();

      // Verify blob is valid before creating download
      if (blob.size === 0) {
        throw new Error('Downloaded file is empty (0 bytes)');
      }

      // Check if we received HTML instead of an image
      if (contentType.includes('text/html')) {
        throw new Error(
          'Server returned HTML page instead of image. The image URL may be expired, moved, or require authentication.'
        );
      }

      // Additional validation for small files that might be error pages
      if (blob.size < 1000 && contentType.includes('text/')) {
        throw new Error(
          'Server returned text content instead of image - possible error page or expired URL'
        );
      }

      // Determine file extension - default to PNG for AI-generated images
      let extension = 'png';
      const finalContentType = contentType || blob.type;

      if (finalContentType) {
        if (finalContentType.includes('jpeg') || finalContentType.includes('jpg')) {
          extension = 'jpg';
        } else if (finalContentType.includes('png')) {
          extension = 'png';
        } else if (finalContentType.includes('webp')) {
          extension = 'webp';
        } else if (finalContentType.includes('gif')) {
          extension = 'gif';
        }
      } else {
        // Fallback: get extension from URL
        const urlExtension = imageUrl.split('.').pop()?.toLowerCase();
        if (urlExtension && ['jpg', 'jpeg', 'png', 'webp', 'gif'].includes(urlExtension)) {
          extension = urlExtension === 'jpeg' ? 'jpg' : urlExtension;
        }
      }

      const filename = `${image.title.toLowerCase().replace(/\s+/g, '-')}-${image.id}.${extension}`;

      // Create object URL for the blob
      const blobUrl = window.URL.createObjectURL(blob);

      // Create download link
      const link = document.createElement('a');
      link.href = blobUrl;
      link.download = filename;
      link.style.display = 'none';

      // Trigger download
      document.body.appendChild(link);
      link.click();

      // Clean up after download
      setTimeout(() => {
        if (document.body.contains(link)) {
          document.body.removeChild(link);
        }
        window.URL.revokeObjectURL(blobUrl);
      }, 100);
    } catch (error) {
      this._logger.error('Download failed', error);

      const errorMessage = error instanceof Error ? error.message : String(error);
      alert(
        `Download failed: ${errorMessage}\n\nPlease try the fallback method or right-click the image to save manually.`
      );
      this.fallbackDownload(image);
    }
  }

  private fallbackDownload(image: GalleryImage) {
    try {
      const imageUrl = image.downloadUrl || image.url;

      // Try to determine extension from URL for fallback
      let extension = 'png'; // Default to PNG
      const urlExtension = imageUrl.split('.').pop()?.toLowerCase();
      if (urlExtension && ['jpg', 'jpeg', 'png', 'webp', 'gif'].includes(urlExtension)) {
        extension = urlExtension === 'jpeg' ? 'jpg' : urlExtension;
      }

      const filename = `${image.title.toLowerCase().replace(/\s+/g, '-')}-${image.id}.${extension}`;

      // Create a more robust fallback download
      const link = document.createElement('a');
      link.href = imageUrl;
      link.download = filename;
      link.target = '_blank';
      link.rel = 'noopener noreferrer';

      // Try to force download with Content-Disposition
      link.style.display = 'none';
      link.setAttribute('download', filename);

      document.body.appendChild(link);
      link.click();

      setTimeout(() => {
        document.body.removeChild(link);
      }, 100);

      alert(
        `Download initiated via fallback method. If the image opens in a new tab instead of downloading, please right-click and select "Save image as..." to save as ${filename}`
      );
    } catch {
      alert(
        'Download failed completely. Please right-click the image and select "Save image as..." or check if the image URL is accessible.'
      );
    }
  }

  onImageShare(image: GalleryImage) {
    if (navigator.share) {
      navigator.share({
        title: image.title,
        text: image.description || 'Check out my AI-generated profile photo!',
        url: image.url,
      });
    } else {
      navigator.clipboard.writeText(image.url);
    }
  }

  onImageDelete(image: GalleryImage) {
    if (confirm(`Are you sure you want to delete "${image.title}"?`)) {
      this._logger.fileDebug('Deleting image', image.title, { id: image.id, title: image.title });

      this._fileUploadService.deleteImage(image.id).subscribe({
        next: response => {
          this._logger.debug('Delete response', response);
          if (response.success) {
            // Remove image from array - create completely new array to ensure change detection
            const originalLength = this.galleryImages.length;
            const newImages = this.galleryImages.filter(img => img.id !== image.id);

            this._logger.info('Image removed from gallery', {
              originalLength,
              newLength: newImages.length,
              imageId: image.id,
            });

            // Assign new array reference
            this.galleryImages = [...newImages];

            // Clear any selections in the photo gallery component
            if (this.photoGallery) {
              this.photoGallery.clearSelections();
            }

            // Force change detection to update UI
            this._cdr.detectChanges();

            // Log final state for debugging
            this._logger.debug('Gallery state after delete', {
              totalImages: this.galleryImages.length,
              isLoading: this.isLoading,
            });
          } else {
            this._logger.error('Delete failed - API returned success: false');
            alert('Failed to delete image. Please try again.');
          }
        },
        error: error => {
          this._logger.error('Delete request failed', error);
          alert(`Delete failed: ${error.message || 'Unknown error'}. Please try again.`);
        },
      });
    }
  }

  async onBulkDownload(images: GalleryImage[]) {
    if (images.length === 0) {
      this._logger.warn('No images selected for download');
      return;
    }

    const downloadableImages = images.filter(
      img => img.status === 'completed' && !!(img.downloadUrl || img.url)
    );

    const skippedCount = images.length - downloadableImages.length;

    if (skippedCount > 0) {
      alert(
        `Skipping ${skippedCount} image${skippedCount === 1 ? '' : 's'} that aren't ready for download yet.

Downloading ${downloadableImages.length} completed image${downloadableImages.length === 1 ? '' : 's'}.`
      );
    }

    if (downloadableImages.length === 0) {
      alert('No completed images ready for download yet. Please wait for processing to finish.');
      return;
    }

    if (downloadableImages.length === 1) {
      await this.onImageDownload(downloadableImages[0]);
      if (this.photoGallery) {
        this.photoGallery.clearSelections();
      }
    } else {
      await this.createZipDownload(downloadableImages);
    }
  }

  private async createZipDownload(images: GalleryImage[]) {
    this.isDownloading = true;
    this.downloadProgress = 0;

    const env = this._config.getCurrentEnvironment();
    const isProdLike = env === 'production' || env === 'test';
    const headers = isProdLike
      ? {}
      : ({ 'ngrok-skip-browser-warning': 'true' } as Record<string, string>);

    try {
      const zip = new jsZip();
      const imageFolder = zip.folder('profile-photos');
      let addedCount = 0;

      for (let i = 0; i < images.length; i++) {
        const image = images[i];
        this.downloadProgress = Math.round(((i + 1) / images.length) * 90); // Reserve 10% for zip generation

        try {
          const imageUrl = image.downloadUrl || image.url;

          // Fetch image with improved error handling
          const response = await fetch(imageUrl, {
            mode: 'cors',
            cache: 'no-cache',
            credentials: 'omit',
            headers,
          });

          if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
          }

          const blob = await response.blob();

          // Verify blob is valid
          if (blob.size === 0) {
            throw new Error('Downloaded blob is empty');
          }

          // Determine file extension - default to PNG for AI-generated images
          let extension = 'png'; // Default to PNG instead of JPG
          const contentType = response.headers.get('content-type') || blob.type;

          if (contentType) {
            if (contentType.includes('jpeg') || contentType.includes('jpg')) {
              extension = 'jpg';
            } else if (contentType.includes('png')) {
              extension = 'png';
            } else if (contentType.includes('webp')) {
              extension = 'webp';
            } else if (contentType.includes('gif')) {
              extension = 'gif';
            }
          } else {
            const urlExtension = imageUrl.split('.').pop()?.toLowerCase();
            if (urlExtension && ['jpg', 'jpeg', 'png', 'webp', 'gif'].includes(urlExtension)) {
              extension = urlExtension === 'jpeg' ? 'jpg' : urlExtension;
            }
          }

          const filename = `${image.title.toLowerCase().replace(/\s+/g, '-')}-${image.id}.${extension}`;

          // Add image to zip
          if (imageFolder) {
            imageFolder.file(filename, blob);
          } else {
            zip.file(filename, blob);
          }

          addedCount++;
        } catch {
          this._logger.warn('Skipping image download for zip (failed to fetch)', {
            imageId: image.id,
            style: image.title,
          });
          // Continue with other images instead of failing completely
        }
      }

      if (addedCount === 0) {
        throw new Error('No images could be downloaded for the zip file');
      }

      this.downloadProgress = 95;

      // Generate zip file
      const zipBlob = await zip.generateAsync({
        type: 'blob',
        compression: 'DEFLATE',
        compressionOptions: {
          level: 6,
        },
      });

      this.downloadProgress = 100;

      // Create download link for zip
      const zipUrl = window.URL.createObjectURL(zipBlob);
      const link = document.createElement('a');
      link.href = zipUrl;
      link.download = `profile-photos-${new Date().toISOString().split('T')[0]}.zip`;
      link.style.display = 'none';

      document.body.appendChild(link);
      link.click();

      setTimeout(() => {
        document.body.removeChild(link);
        window.URL.revokeObjectURL(zipUrl);
      }, 100);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      alert(
        `Failed to create zip file: ${errorMessage}\n\nPlease try downloading images individually.`
      );
    } finally {
      this.isDownloading = false;
      this.downloadProgress = 0;
      this._cdr.detectChanges(); // Force UI update to dismiss modal

      // Clear selections after download completes (success or failure)
      if (this.photoGallery) {
        this.photoGallery.clearSelections();
      }
    }
  }

  formatStyleName(style: string): string {
    if (!style) {
      return '';
    }
    return style
      .replace(/[-_/]/g, ' ') // Replace dashes, underscores, and slashes with spaces
      .split(' ')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(' ');
  }

  private _getDisplayStyleLabel(style: string | null | undefined, isGenerated: boolean): string {
    const normalized = (style || '').trim().toLowerCase();
    if (!isGenerated) {
      return this.formatStyleName(style || '');
    }

    if (!normalized) {
      return 'Unmapped Style';
    }

    return this.formatStyleName(normalized);
  }
}
