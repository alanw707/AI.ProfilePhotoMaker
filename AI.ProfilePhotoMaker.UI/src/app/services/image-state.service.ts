import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { StateBaseService } from './state-base.service';
import { FileUploadService } from './file-upload.service';
import { ImageValidationService } from './image-validation.service';
import { ConfigService } from './config.service';
import { CacheManagerService } from './cache-manager.service';
import { NotificationService } from './notification.service';
import { UploadedImageThumbnail } from '../interfaces/service.interfaces';

export interface ImageState {
  uploadedImages: number;
  uploadedImageThumbnails: UploadedImageThumbnail[];
  generatedPhotosCount: number;
  isLoading: boolean;
  imagesValidated: boolean;
  lastValidationTime: number;
}

export interface ImageValidationResult {
  validImages: UploadedImageThumbnail[];
  removedCount: number;
  repairTriggered?: boolean;
}

/**
 * Service responsible for managing image state, validation, and thumbnails
 * Extracted from DashboardStateService for better separation of concerns
 */
@Injectable({
  providedIn: 'root',
})
export class ImageStateService extends StateBaseService<ImageState> {
  private readonly CACHE_KEY = 'image_state_data';
  private readonly VALIDATION_TTL = 5 * 60 * 1000; // 5 minutes

  protected override initialState: ImageState = {
    uploadedImages: 0,
    uploadedImageThumbnails: [],
    generatedPhotosCount: 0,
    isLoading: false,
    imagesValidated: false,
    lastValidationTime: 0,
  };

  constructor(
    _cacheManager: CacheManagerService,
    _notificationService: NotificationService,
    private _fileUploadService: FileUploadService,
    private _imageValidation: ImageValidationService,
    private _configService: ConfigService
  ) {
    super(
      {
        uploadedImages: 0,
        uploadedImageThumbnails: [],
        generatedPhotosCount: 0,
        isLoading: false,
        imagesValidated: false,
        lastValidationTime: 0,
      },
      _cacheManager,
      _notificationService
    );
  }

  /**
   * Load user images and process them into thumbnails
   */
  async loadUserImages(forceRefresh = false): Promise<void> {
    const startTime = performance.now();

    // Check cache first
    if (!forceRefresh) {
      const cachedData = this.getCachedData<ImageState>(this.CACHE_KEY);
      if (cachedData?.uploadedImageThumbnails) {
        this.setState(cachedData);

        // Always validate cached images
        if (cachedData.uploadedImageThumbnails.length > 0) {
          await this.validateCachedImages(cachedData.uploadedImageThumbnails);
        }
        return;
      }
    }

    // Debounce rapid reloads
    if (this.shouldDebounceRequest('image_load')) {
      return;
    }

    this.setLoading(true);

    try {
      const userImages = await this._fileUploadService.getUserImages(forceRefresh).toPromise();

      if (userImages?.success && userImages.data) {
        const processed = this.processUserImagesData(userImages.data);

        this.setState({
          uploadedImages: processed.uploadedImages,
          uploadedImageThumbnails: processed.uploadedImageThumbnails,
          generatedPhotosCount: processed.generatedPhotosCount,
          isLoading: false,
        });

        // Cache the processed data
        this.setCachedData(this.CACHE_KEY, this.getState());

        // Validate images if we have any
        if (processed.uploadedImageThumbnails.length > 0) {
          const validation = await this.validateAndCleanupImages(
            processed.uploadedImageThumbnails,
            false
          );
          if (validation.removedCount > 0) {
            this.updateStateWithValidatedImages(validation);
          }
        }

        this.logPerformance('User images loaded', startTime);
      } else {
        throw new Error('Failed to load user images');
      }
    } catch (error) {
      this.handleApiError(error, 'Load Images');
      this.setLoading(false);
    }
  }

  /**
   * Process raw user images data into structured format
   */
  private processUserImagesData(userImagesData: any): {
    uploadedImages: number;
    uploadedImageThumbnails: UploadedImageThumbnail[];
    generatedPhotosCount: number;
  } {
    // Process uploaded images with robust filtering
    const rawImageThumbnails: UploadedImageThumbnail[] =
      userImagesData?.images
        ?.filter((img: any) => {
          // Skip generated images entirely
          if (img.isGenerated) {
            return false;
          }

          // Robust filtering for uploaded images
          const isOriginalByFlag = img.isOriginalUpload;
          const isOriginalByStyle = img.style === 'Original';
          const hasUrl = !!img.originalImageUrl;

          const isUploadedImage = (isOriginalByFlag || isOriginalByStyle) && hasUrl;

          // Flag/style mismatch indicates possible database corruption
          if (isOriginalByFlag !== isOriginalByStyle) {
            console.warn(
              `⚠️ Image ${img.id} has flag/style mismatch: isOriginalUpload=${isOriginalByFlag}, style=${img.style} - possible database corruption`
            );
          }

          return isUploadedImage;
        })
        ?.map((img: any) => ({
          id: img.id,
          url: this.cleanImageUrl(img.originalImageUrl),
          fileName: `Image ${img.id}`,
        })) || [];

    // Count generated photos
    const generatedPhotosCount =
      userImagesData?.generatedImages ||
      userImagesData?.images?.filter((img: any) => img.isGenerated)?.length ||
      0;

    return {
      uploadedImages: rawImageThumbnails.length,
      uploadedImageThumbnails: rawImageThumbnails,
      generatedPhotosCount,
    };
  }

  /**
   * Validate cached images and update if needed
   */
  private async validateCachedImages(images: UploadedImageThumbnail[]): Promise<void> {
    const currentState = this.getState();
    const validationAge = Date.now() - (currentState.lastValidationTime || 0);

    if (currentState.imagesValidated && validationAge < this.VALIDATION_TTL) {
      return;
    }
    const result = await this.validateAndCleanupImages(images, true);

    if (result.removedCount > 0) {
      this.updateStateWithValidatedImages(result);
    } else {
      this.setState({
        imagesValidated: true,
        lastValidationTime: Date.now(),
      });
    }
  }

  /**
   * Validate and cleanup broken images
   */
  async validateAndCleanupImages(
    images: UploadedImageThumbnail[],
    isFromCache: boolean
  ): Promise<ImageValidationResult> {
    // Check if image validation is disabled via environment configuration
    if (!this._configService.isImageValidationEnabled) {
      return {
        validImages: images,
        removedCount: 0,
        repairTriggered: false,
      };
    }

    const validation = await this._imageValidation.filterValidImages(images);

    if (validation.removedCount > 0) {
      // Trigger repair if 404s were found
      if (validation.repairSuggested && validation.notFoundCount > 0) {
        try {
          const repairResult = await this._fileUploadService.repairImageDatabase().toPromise();
          if (repairResult?.success) {
            // Force refresh from server after repair
            await this.forceRefreshAfterRepair();

            return {
              validImages: validation.validImages,
              removedCount: validation.removedCount,
              repairTriggered: true,
            };
          }
        } catch (repairError) {
          console.error('🔧 Database repair failed:', repairError);
        }
      }
    }

    return {
      validImages: validation.validImages,
      removedCount: validation.removedCount,
      repairTriggered: false,
    };
  }

  /**
   * Update state with validated images
   */
  private updateStateWithValidatedImages(result: ImageValidationResult): void {
    this.setState({
      uploadedImageThumbnails: result.validImages,
      uploadedImages: result.validImages.length,
      imagesValidated: true,
      lastValidationTime: Date.now(),
    });

    // Invalidate cache since we have new data
    this.invalidateCache(this.CACHE_KEY);

    // Show notification if images were removed
    if (result.removedCount > 0) {
      const message = result.repairTriggered
        ? `Removed ${result.removedCount} broken image reference(s) and repaired database inconsistencies.`
        : `Removed ${result.removedCount} broken image reference(s) that could no longer be loaded.`;

      this.showInfo(
        result.repairTriggered ? 'Cleaned up & Repaired' : 'Cleaned up broken images',
        message
      );
    }
  }

  /**
   * Force refresh after repair to sync counts
   */
  private async forceRefreshAfterRepair(): Promise<void> {
    try {
      // Clear all caches
      this.forceRefreshCache(this.CACHE_KEY);
      this._fileUploadService.invalidateUserImagesCache();
      this._imageValidation.clearCache();

      // Get fresh data from server
      const userImages = await this._fileUploadService.getUserImages(true).toPromise();

      if (userImages?.success && userImages.data) {
        const processed = this.processUserImagesData(userImages.data);

        this.setState({
          uploadedImageThumbnails: processed.uploadedImageThumbnails,
          uploadedImages: processed.uploadedImages,
          generatedPhotosCount: processed.generatedPhotosCount,
          imagesValidated: false, // Force validation on fresh data
          lastValidationTime: 0,
        });
      }
    } catch (error) {
      console.error('❌ Failed to refresh after repair:', error);
    }
  }

  /**
   * Refresh only the generated photos count
   */
  async refreshGeneratedPhotosCount(): Promise<void> {
    try {
      const userImages = await this._fileUploadService.getUserImages().toPromise();

      if (userImages?.success && userImages.data) {
        const userImagesData = userImages.data;
        const generatedPhotosCount =
          userImagesData?.generatedImages ||
          userImagesData?.images?.filter((img: any) => img.isGenerated)?.length ||
          0;

        this.setState({ generatedPhotosCount });
      }
    } catch (error) {
      console.error('Failed to refresh generated photos count:', error);
    }
  }

  /**
   * Clean image URL to remove fragments and cache-busting artifacts
   */
  private cleanImageUrl(url: string): string {
    try {
      const cleanUrl = url.split('#')[0].split('?')[0];

      if (!cleanUrl.startsWith('http') && !cleanUrl.startsWith('/')) {
        return '/' + cleanUrl;
      }

      return cleanUrl;
    } catch (error) {
      console.warn('Failed to clean image URL:', url, error);
      return url;
    }
  }

  /**
   * Public method for manual validation (debug purposes)
   */
  async validateCurrentImages(): Promise<void> {
    const currentState = this.getState();
    const images = currentState.uploadedImageThumbnails;

    if (images.length === 0) {
      return;
    }
    const result = await this.validateAndCleanupImages(images, false);

    if (result.removedCount > 0) {
      this.updateStateWithValidatedImages(result);

      const message = result.repairTriggered
        ? `Validated ${images.length} images. Removed ${result.removedCount} broken references and repaired database.`
        : `Validated ${images.length} images. Removed ${result.removedCount} broken references.`;

      this.showInfo(
        result.repairTriggered ? 'Validation & Repair Complete' : 'Image Validation Complete',
        message
      );
    } else {
      this.setState({
        imagesValidated: true,
        lastValidationTime: Date.now(),
      });

      this.showSuccess('All Images Valid', 'All uploaded images are accessible and valid.');
    }
  }

  /**
   * Invalidate images cache and refresh
   */
  invalidateAndRefresh(): void {
    this._fileUploadService.invalidateUserImagesCache();
    this.invalidateCache(this.CACHE_KEY);
    this.loadUserImages(true);
  }

  /**
   * Force refresh implementation
   */
  forceRefresh(): void {
    this.forceRefreshCache(this.CACHE_KEY);
    this._imageValidation.clearCache();

    this.setState({
      imagesValidated: false,
      lastValidationTime: 0,
    });

    this.loadUserImages(true);
  }
}
