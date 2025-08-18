import { Injectable } from '@angular/core';
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

          // Additional safety check: Filter out enhanced images that might have slipped through
          // Enhanced images should NEVER be counted in dashboard uploads
          const isEnhancedImage = this.isEnhancedImage(
            img.originalImageUrl,
            img.processedImageUrl,
            img.fileName
          );

          const isUploadedImage =
            (isOriginalByFlag || isOriginalByStyle) && hasUrl && !isEnhancedImage;

          // Flag/style mismatch indicates possible database corruption
          if (isOriginalByFlag !== isOriginalByStyle) {
            console.warn(
              `⚠️ Image ${img.id} has flag/style mismatch: isOriginalUpload=${isOriginalByFlag}, style=${img.style} - possible database corruption`
            );
          }

          // Warn if enhanced images are found in upload list (indicates bug)
          if (isEnhancedImage) {
            console.warn(
              `🚨 Enhanced image ${img.id} found in upload list - this should not happen! URL: ${img.originalImageUrl}`
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
    _isFromCache: boolean
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

    if (validation.removedCount > 0 && this._shouldTriggerAutoRepair(validation)) {
      return await this._attemptAutoRepair(validation);
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

  /**
   * Utility method to check if an image is an enhanced image
   * Enhanced images are temporary files and should not be counted in dashboard uploads
   */
  private isEnhancedImage(originalUrl?: string, processedUrl?: string, fileName?: string): boolean {
    return (
      originalUrl?.includes('/enhanced/') ||
      processedUrl?.includes('/enhanced/') ||
      fileName?.includes('enhanced_') ||
      false
    );
  }

  // NEW: Enhanced Auto-Repair Helper Methods with Safety Guards

  /**
   * Check if auto-repair should be triggered based on safety conditions
   * @param validation - The validation result containing broken image counts
   * @returns true if auto-repair should proceed, false if safety conditions not met
   */
  private _shouldTriggerAutoRepair(validation: any): boolean {
    // Check master feature flag
    if (!this._configService.isAutoRepairEnabled) {
      console.log('🔧 Auto-repair disabled by feature flag');
      return false;
    }

    // Check threshold requirement
    if (validation.notFoundCount < this._configService.autoRepairThreshold) {
      console.log(
        `🔧 Auto-repair threshold not met: ${validation.notFoundCount} < ${this._configService.autoRepairThreshold}`
      );
      return false;
    }

    // Check cooldown period
    const lastRepairKey = 'lastAutoRepairTime';
    const lastRepair = localStorage.getItem(lastRepairKey);
    if (lastRepair) {
      const timeSinceLastRepair = Date.now() - parseInt(lastRepair);
      if (timeSinceLastRepair < this._configService.autoRepairCooldownMs) {
        const remainingCooldown = Math.ceil(
          (this._configService.autoRepairCooldownMs - timeSinceLastRepair) / (60 * 1000)
        );
        console.log(`🔧 Auto-repair in cooldown: ${remainingCooldown} minutes remaining`);
        return false;
      }
    }

    // Check session attempt limit
    const sessionAttemptsKey = 'sessionAutoRepairAttempts';
    const sessionAttempts = parseInt(localStorage.getItem(sessionAttemptsKey) || '0');
    if (sessionAttempts >= this._configService.autoRepairMaxAttempts) {
      console.log(
        `🔧 Auto-repair max attempts reached: ${sessionAttempts}/${this._configService.autoRepairMaxAttempts}`
      );
      return false;
    }

    // Check if repair is suggested by validation logic
    if (!validation.repairSuggested || validation.notFoundCount <= 0) {
      console.log('🔧 Auto-repair not suggested by validation logic');
      return false;
    }

    console.log(
      `🔧 Auto-repair conditions met: ${validation.notFoundCount} broken images detected`
    );
    return true;
  }

  /**
   * Attempt auto-repair with comprehensive safety measures and logging
   * @param validation - The validation result containing broken image information
   * @returns Enhanced validation result with repair status
   */
  private async _attemptAutoRepair(validation: any): Promise<any> {
    try {
      console.log(
        `🔧 Auto-repair triggered: ${validation.notFoundCount} broken references detected`
      );

      // Record repair attempt
      const now = Date.now();
      localStorage.setItem('lastAutoRepairTime', now.toString());

      // Increment session attempts counter
      const sessionAttemptsKey = 'sessionAutoRepairAttempts';
      const currentAttempts = parseInt(localStorage.getItem(sessionAttemptsKey) || '0') + 1;
      localStorage.setItem(sessionAttemptsKey, currentAttempts.toString());

      // Check dry-run mode
      if (this._configService.isAutoRepairDryRunOnly) {
        console.log('🔧 Auto-repair in DRY-RUN mode - no actual changes will be made');

        if (this._configService.isAutoRepairNotificationsEnabled) {
          this._notificationService.info(
            'Auto-Repair Simulation',
            `Would repair ${validation.notFoundCount} broken image references (DRY-RUN mode)`
          );
        }

        return {
          validImages: validation.validImages,
          removedCount: validation.removedCount,
          repairTriggered: false,
          dryRunMode: true,
        };
      }

      // Perform actual repair
      console.log('🔧 Executing auto-repair...');
      const repairResult = await this._fileUploadService.repairImageDatabase().toPromise();

      if (repairResult?.success) {
        console.log('✅ Auto-repair completed successfully');

        if (this._configService.isAutoRepairNotificationsEnabled) {
          this._notificationService.success(
            'Auto-Repair Complete',
            `Successfully repaired ${validation.notFoundCount} broken image references`
          );
        }

        // Force refresh to get accurate data after repair
        await this.forceRefreshAfterRepair();

        return {
          validImages: validation.validImages,
          removedCount: validation.removedCount,
          repairTriggered: true,
          repairSuccess: true,
        };
      } else {
        console.warn('⚠️ Auto-repair completed but returned no success indicator');
        return {
          validImages: validation.validImages,
          removedCount: validation.removedCount,
          repairTriggered: false,
          repairError: 'No success indicator returned',
        };
      }
    } catch (error) {
      console.error('🚨 Auto-repair failed:', error);

      // Send error telemetry if enabled
      if (this._configService.isAutoRepairTelemetryEnabled) {
        this._trackRepairError(error, validation);
      }

      if (this._configService.isAutoRepairNotificationsEnabled) {
        this._notificationService.error(
          'Auto-Repair Failed',
          'Image repair encountered an error. Please try manual refresh.'
        );
      }

      return {
        validImages: validation.validImages,
        removedCount: validation.removedCount,
        repairTriggered: false,
        repairError: error,
      };
    }
  }

  /**
   * Track auto-repair errors for telemetry and debugging
   * @param error - The error that occurred during repair
   * @param validation - The validation context when error occurred
   */
  private _trackRepairError(error: any, validation: any): void {
    const errorData = {
      timestamp: new Date().toISOString(),
      errorMessage: error?.message || 'Unknown error',
      validationContext: {
        notFoundCount: validation.notFoundCount,
        removedCount: validation.removedCount,
        repairSuggested: validation.repairSuggested,
      },
      environment: this._configService.getCurrentEnvironment(),
      validationLevel: this._configService.autoRepairValidationLevel,
    };

    console.warn('📊 Auto-repair error telemetry:', errorData);

    // Future: Send to analytics service
    // this._analyticsService.trackError('auto-repair-failed', errorData);
  }
}
