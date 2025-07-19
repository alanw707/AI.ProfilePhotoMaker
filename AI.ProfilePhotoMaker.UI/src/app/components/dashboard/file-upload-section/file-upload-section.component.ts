import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Injector,
  Input,
  NgZone,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { FileUploadService } from '../../../services/file-upload.service';
import { NotificationService } from '../../../services/notification.service';

// Lazy-loaded service interface
interface FaceDetectionService {
  validateImage(file: File): Promise<any>;
}

import {
  QualityCheckError,
  QualityCheckResult,
  SelectedFileWithQuality,
} from '../../../models/dashboard.types';

@Component({
  selector: 'app-file-upload-section',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './file-upload-section.component.html',
  styleUrls: ['./file-upload-section.component.sass'],
})
export class FileUploadSectionComponent implements OnInit, OnDestroy {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  // Input properties
  @Input() uploadedImageThumbnails: any[] = [];
  @Input() currentStep = 1;
  @Input() maxFiles = 20;
  @Input() maxFileSize: number = 7 * 1024 * 1024; // 7MB
  @Input() allowedTypes: string[] = ['image/jpeg', 'image/png', 'image/webp'];

  // Output events
  @Output() filesSelected = new EventEmitter<File[]>();
  @Output() uploadCompleted = new EventEmitter<any[]>();
  @Output() uploadProgress = new EventEmitter<number>();
  @Output() qualityCheckCompleted = new EventEmitter<QualityCheckResult>();
  @Output() fileRemoved = new EventEmitter<number>();
  @Output() uploadedImageDeleted = new EventEmitter<{
    thumb: any;
    index: number;
    refreshRequired?: boolean;
  }>();

  // Component state
  selectedFiles: File[] = [];
  selectedFilesWithQuality: SelectedFileWithQuality[] = [];
  isUploading = false;
  uploadProgressValue = 0;
  isDragOver = false;
  isCheckingQuality = false;
  qualityCheckProgress = '';
  qualityCheckErrors: QualityCheckError[] = [];
  invalidFilesFeedback: { fileName: string; reason: string }[] = [];

  // Global tooltip state
  activeTooltipError: QualityCheckError | null = null;
  tooltipPosition: { x: number; y: number } = { x: 0, y: 0 };

  // File preview cache for memory management
  private filePreviewCache = new Map<File, string>();

  // Lazy-loaded service
  private faceDetectionService: FaceDetectionService | null = null;

  constructor(
    private fileUploadService: FileUploadService,
    private notificationService: NotificationService,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef,
    private injector: Injector
  ) {}

  ngOnInit() {
    // Face detection models will be loaded automatically when validateImage is called

    // Close popups when clicking outside
    document.addEventListener('click', this.closeAllPopups.bind(this));
  }

  // Lazy loading method for face detection service
  private async loadFaceDetectionService(): Promise<FaceDetectionService> {
    if (!this.faceDetectionService) {
      const { FaceDetectionService } = await import('../../../services/face-detection.service');
      this.faceDetectionService = this.injector.get(FaceDetectionService);
    }
    return this.faceDetectionService;
  }

  ngOnDestroy() {
    this.cleanupFilePreviewCache();
    document.removeEventListener('click', this.closeAllPopups.bind(this));
  }

  // File Selection Methods
  triggerFileUpload() {
    this.fileInput.nativeElement.click();
  }

  onFileSelected(event: any) {
    const files = Array.from(event.target.files) as File[];
    this.handleFileSelection(files);
    // Reset the input value to allow selecting the same files again
    this.fileInput.nativeElement.value = '';
  }

  // Drag and Drop Methods
  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;

    const files = Array.from(event.dataTransfer?.files || []) as File[];
    this.handleFileSelection(files);
  }

  // Core File Handling
  async handleFileSelection(files: File[]) {
    if (!files || files.length === 0) {
      return;
    }

    // Check total file count limit
    const totalFiles = this.selectedFiles.length + files.length;
    if (totalFiles > this.maxFiles) {
      this.notificationService.error(
        'Too Many Files',
        `You can only upload a maximum of ${this.maxFiles} files. You've selected ${totalFiles} files.`
      );
      return;
    }

    // Track validation results
    const validationResults = {
      validFiles: [] as File[],
      invalidFiles: [] as File[],
      errors: {
        unsupportedType: [] as string[],
        tooLarge: [] as string[],
      },
    };

    // Validate each file
    files.forEach(file => {
      const validation = this.validateFile(file);
      if (validation.isValid) {
        validationResults.validFiles.push(file);
      } else {
        validationResults.invalidFiles.push(file);
        if (validation.error === 'type') {
          validationResults.errors.unsupportedType.push(file.name);
        } else if (validation.error === 'size') {
          validationResults.errors.tooLarge.push(file.name);
        }
      }
    });

    // Update inline feedback for invalid files
    this.invalidFilesFeedback = [];
    validationResults.errors.unsupportedType.forEach(fileName => {
      this.invalidFilesFeedback.push({
        fileName,
        reason: 'Different format needed. Use JPEG, PNG, or WebP',
      });
    });
    validationResults.errors.tooLarge.forEach(fileName => {
      const sizeInMB = (this.maxFileSize / (1024 * 1024)).toFixed(1);
      this.invalidFilesFeedback.push({
        fileName,
        reason: `Size too large. Max: ${sizeInMB}MB`,
      });
    });

    // Show consolidated error notification if any files were invalid
    if (validationResults.invalidFiles.length > 0) {
      this.showConsolidatedErrors(validationResults);
    }

    // If no valid files, return early
    if (validationResults.validFiles.length === 0) {
      return;
    }

    // Add valid files to selection
    this.selectedFiles.push(...validationResults.validFiles);
    this.filesSelected.emit(this.selectedFiles);

    // Start quality validation
    await this.validateImageQuality(validationResults.validFiles);
  }

  private validateFile(file: File): { isValid: boolean; error?: 'type' | 'size' } {
    // Check file type
    if (!this.allowedTypes.includes(file.type)) {
      return { isValid: false, error: 'type' };
    }

    // Check file size
    if (file.size > this.maxFileSize) {
      return { isValid: false, error: 'size' };
    }

    return { isValid: true };
  }

  private showConsolidatedErrors(results: any): void {
    const errors = [];

    if (results.errors.unsupportedType.length > 0) {
      const count = results.errors.unsupportedType.length;
      const fileList = results.errors.unsupportedType.slice(0, 3).join(', ');
      const more = count > 3 ? ` and ${count - 3} more` : '';
      errors.push(
        `${count} file${count > 1 ? 's need' : ' needs'} a different format: ${fileList}${more}`
      );
    }

    if (results.errors.tooLarge.length > 0) {
      const count = results.errors.tooLarge.length;
      const sizeInMB = (this.maxFileSize / (1024 * 1024)).toFixed(1);
      const fileList = results.errors.tooLarge.slice(0, 3).join(', ');
      const more = count > 3 ? ` and ${count - 3} more` : '';
      errors.push(
        `${count} file${count > 1 ? 's are' : ' is'} too large (max ${sizeInMB}MB): ${fileList}${more}`
      );
    }

    const totalInvalid = results.invalidFiles.length;
    const totalFiles = results.validFiles.length + totalInvalid;

    this.notificationService.error(
      'Please Check File Format',
      errors.join('. ') + '. Supported formats: JPEG, PNG, WebP (max 7MB).',
      5000 // Auto-close after 5 seconds
    );
  }

  // Quality Validation
  private async validateImageQuality(files: File[]) {
    this.isCheckingQuality = true;
    this.qualityCheckProgress = 'Starting quality analysis...';
    this.qualityCheckErrors = [];
    this.cdr.detectChanges();

    const validFiles: File[] = [];
    const errors: QualityCheckError[] = [];

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      const progress = Math.round(((i + 1) / files.length) * 100);
      this.qualityCheckProgress = `Analyzing ${file.name} (${i + 1}/${files.length})...`;
      this.cdr.detectChanges();

      try {
        // Check image dimensions
        const dimensions = await this.getImageDimensions(file);
        if (dimensions.width < 512 || dimensions.height < 512) {
          errors.push({
            fileName: file.name,
            file,
            errors: [
              `Image resolution ${dimensions.width}x${dimensions.height} is too small. Minimum 512x512 required.`,
            ],
            warnings: [],
            showErrorDetails: false,
          });
          continue;
        }

        // Perform face detection and quality analysis
        const faceDetectionService = await this.loadFaceDetectionService();
        const qualityResult = await faceDetectionService.validateImage(file);

        if (qualityResult.isValid) {
          validFiles.push(file);
          this.selectedFilesWithQuality.push({
            file,
            qualityScore: qualityResult.qualityScore,
            faceValidation: qualityResult,
            errors: [],
            warnings: qualityResult.warnings || [],
            isValid: true,
            showDetails: false,
          });
        } else {
          errors.push({
            fileName: file.name,
            file,
            errors: qualityResult.errors || ['Quality check failed'],
            warnings: qualityResult.warnings || [],
            faceValidation: qualityResult,
            qualityScore: qualityResult.qualityScore,
            showErrorDetails: false,
          });
        }
      } catch (error) {
        console.error(`Quality check failed for ${file.name}:`, error);
        errors.push({
          fileName: file.name,
          file,
          errors: ['Failed to analyze image quality'],
          warnings: [],
          showErrorDetails: false,
        });
      }

      // Update progress
      await this.ngZone.run(async () => {
        this.qualityCheckProgress = `Analyzed ${i + 1} of ${files.length} images...`;
        this.cdr.detectChanges();
      });
    }

    this.isCheckingQuality = false;
    this.qualityCheckProgress = '';
    this.qualityCheckErrors = errors;
    this.cdr.detectChanges();

    // Emit quality check results
    this.qualityCheckCompleted.emit({
      validFiles,
      invalidFiles: errors.map(e => e.file),
      errors,
      totalProcessed: files.length,
    });

    // Show summary notification
    if (validFiles.length > 0) {
      this.notificationService.success(
        'Quality Check Complete',
        `${validFiles.length} of ${files.length} images passed quality checks.`
      );
    }

    if (errors.length > 0) {
      this.notificationService.error(
        'Quality Issues Found',
        `${errors.length} image(s) failed quality validation. Please review and try again.`
      );
    }
  }

  private getImageDimensions(file: File): Promise<{ width: number; height: number }> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      const url = URL.createObjectURL(file);

      img.onload = () => {
        URL.revokeObjectURL(url);
        resolve({ width: img.width, height: img.height });
      };

      img.onerror = () => {
        URL.revokeObjectURL(url);
        reject(new Error('Failed to load image'));
      };

      img.src = url;
    });
  }

  // File Management
  removeFile(index: number) {
    if (index >= 0 && index < this.selectedFiles.length) {
      const removedFile = this.selectedFiles[index];

      // Clean up preview cache
      if (this.filePreviewCache.has(removedFile)) {
        URL.revokeObjectURL(this.filePreviewCache.get(removedFile)!);
        this.filePreviewCache.delete(removedFile);
      }

      // Remove from arrays
      this.selectedFiles.splice(index, 1);
      this.selectedFilesWithQuality.splice(index, 1);

      // Remove associated quality errors
      this.qualityCheckErrors = this.qualityCheckErrors.filter(error => error.file !== removedFile);

      this.fileRemoved.emit(index);
      this.filesSelected.emit(this.selectedFiles);
    }
  }

  deleteUploadedImage(thumb: any, index: number) {
    console.log('Attempting to delete image:', { thumb, index });

    if (!thumb?.id) {
      console.error('Thumbnail missing or invalid:', thumb);
      this.notificationService.error(
        'Error',
        'Cannot delete image: missing or invalid thumbnail data'
      );
      return;
    }

    // Validate ID is a number
    const imageId = parseInt(thumb.id.toString(), 10);
    if (isNaN(imageId)) {
      console.error('Invalid image ID format:', thumb.id);
      this.notificationService.error('Error', 'Cannot delete image: invalid ID format');
      return;
    }

    console.log('Parsed image ID:', imageId);

    this.fileUploadService.deleteImage(imageId).subscribe({
      next: response => {
        if (response.success) {
          this.uploadedImageDeleted.emit({ thumb, index });

          // Show different messages based on whether repair was triggered
          if (response.repairTriggered) {
            this.notificationService.success(
              'Repaired & Synchronized',
              'Detected data inconsistency and automatically repaired. Database synchronized with filesystem.'
            );
            // Trigger refresh to sync with repaired server state
            this.uploadedImageDeleted.emit({
              thumb,
              index,
              refreshRequired: true,
            });
          } else {
            this.notificationService.success('Deleted', 'Image deleted successfully');
          }
        } else {
          console.error('Delete failed on server:', response);
          this.notificationService.error('Error', response.message || 'Failed to delete image');
        }
      },
      error: error => {
        console.error('Error deleting image:', error);

        let errorMessage = 'Failed to delete image';
        let shouldRefreshData = false;

        if (error.status === 400) {
          errorMessage = 'Image already deleted or invalid request';
          shouldRefreshData = true;
        } else if (error.status === 404) {
          errorMessage = 'Image not found - it may have already been deleted';
          shouldRefreshData = true;
        } else if (error.status === 401) {
          errorMessage = 'Not authorized to delete this image';
        } else if (error.status === 500) {
          errorMessage = 'Server error while deleting image';
        }

        this.notificationService.error(
          'Error',
          `${errorMessage}: ${error.message || error.statusText || 'Unknown error'}`
        );

        // If image wasn't found, refresh the uploaded images to sync with server
        if (shouldRefreshData) {
          this.refreshUploadedImages();
        }
      },
    });
  }

  // Method to refresh uploaded images from server
  private refreshUploadedImages() {
    // Emit an event to parent component to refresh the uploaded images
    // This will be handled by the dashboard component
    this.uploadedImageDeleted.emit({
      thumb: null,
      index: -1,
      refreshRequired: true,
    });
  }

  // Handle image load errors (404s, network failures, etc.)
  onImageLoadError(thumb: any, index: number) {
    console.warn(`🖼️ Image failed to load: ${thumb.url}`, {
      thumb,
      index,
      errorType: 'Image load failure',
      possibleCauses: ['Network issue', 'CORS', 'Authentication', '404', 'Malformed URL'],
    });

    // Don't automatically remove - this could be a temporary network issue
    // Instead, log the error for debugging and let users manually handle it
    // Only remove if we can confirm it's actually a 404 (which HTML img error event can't tell us)
  }

  // Handle successful image loads (for debugging)
  onImageLoadSuccess(thumb: any, index: number) {
    // Optional: Log successful loads for debugging
  }

  // Upload Process
  uploadImages() {
    if (this.selectedFiles.length === 0) {
      this.notificationService.error('No Files Selected', 'Please select files to upload');
      return;
    }

    // Get only valid files that passed quality checks
    const validFiles = this.selectedFilesWithQuality.filter(f => f.isValid).map(f => f.file);

    if (validFiles.length === 0) {
      this.notificationService.error(
        'No Valid Files',
        'Please fix quality issues or select different files before uploading'
      );
      return;
    }

    // Show info about excluded files
    const invalidCount = this.selectedFiles.length - validFiles.length;
    if (invalidCount > 0) {
      this.notificationService.info(
        'Files Excluded',
        `${invalidCount} file(s) with quality issues were excluded. Uploading ${validFiles.length} valid file(s).`
      );
    }

    this.isUploading = true;
    this.uploadProgressValue = 0;
    this.cdr.detectChanges();

    // Upload only valid files, set forTraining=false to avoid premature ZIP creation
    this.fileUploadService.uploadImages(validFiles, undefined, false).subscribe({
      next: result => {
        if (result.progress !== undefined) {
          this.uploadProgressValue = result.progress;
          this.uploadProgress.emit(result.progress);
          this.cdr.detectChanges();
        }

        if (result.response) {
          // Upload completed successfully
          console.log('🎉 Upload completed successfully:', result.response);

          // Force UI state reset BEFORE emitting events
          this.isUploading = false;
          this.uploadProgressValue = 0;
          this.cdr.detectChanges();

          // Clear selected files
          this.clearSelectedFiles();

          // Emit completion event to trigger dashboard refresh
          const uploadedFiles = result.response?.uploadedFiles || [];
          this.uploadCompleted.emit(uploadedFiles);

          // Show success notification with null safety
          const fileCount = uploadedFiles.length;
          this.notificationService.success(
            'Upload Complete',
            `${fileCount} image(s) uploaded successfully!`
          );

          // Force final change detection
          this.cdr.detectChanges();
        }
      },
      error: error => {
        console.error('Upload error:', error);
        this.notificationService.error('Upload Error', 'An error occurred during upload');
        this.isUploading = false;
        this.uploadProgressValue = 0;
        this.cdr.detectChanges();
      },
    });
  }

  private clearSelectedFiles() {
    this.cleanupFilePreviewCache();
    this.selectedFiles = [];
    this.selectedFilesWithQuality = [];
    this.qualityCheckErrors = [];
    this.invalidFilesFeedback = [];
    this.filesSelected.emit(this.selectedFiles);
  }

  // File Preview Management
  getFilePreview(file: File): string {
    if (!this.filePreviewCache.has(file)) {
      const url = URL.createObjectURL(file);
      this.filePreviewCache.set(file, url);
    }
    return this.filePreviewCache.get(file)!;
  }

  private cleanupFilePreviewCache() {
    this.filePreviewCache.forEach(url => URL.revokeObjectURL(url));
    this.filePreviewCache.clear();
  }

  removeFileFromErrors(error: QualityCheckError) {
    // Remove from selectedFiles
    const fileIndex = this.selectedFiles.findIndex(f => f === error.file);
    if (fileIndex !== -1) {
      this.selectedFiles.splice(fileIndex, 1);
    }

    // Remove from selectedFilesWithQuality
    const qualityIndex = this.selectedFilesWithQuality.findIndex(f => f.file === error.file);
    if (qualityIndex !== -1) {
      this.selectedFilesWithQuality.splice(qualityIndex, 1);
    }

    // Remove from qualityCheckErrors
    const errorIndex = this.qualityCheckErrors.findIndex(e => e.file === error.file);
    if (errorIndex !== -1) {
      this.qualityCheckErrors.splice(errorIndex, 1);
    }

    // Clean up preview cache
    if (this.filePreviewCache.has(error.file)) {
      URL.revokeObjectURL(this.filePreviewCache.get(error.file)!);
      this.filePreviewCache.delete(error.file);
    }

    this.filesSelected.emit(this.selectedFiles);
  }

  // Show global tooltip with smart viewport positioning
  showGlobalTooltip(error: QualityCheckError, event: Event) {
    event.stopPropagation();

    // Set active tooltip error
    this.activeTooltipError = error;

    // Calculate optimal position
    this.calculateTooltipPosition(event.target as HTMLElement);

    // Force change detection
    this.cdr.detectChanges();

    // Enhanced safety check: Validate positioning after DOM updates
    setTimeout(() => this.validateTooltipPositioning(), 50);
  }

  // Close global tooltip
  closeGlobalTooltip() {
    this.activeTooltipError = null;
    this.cdr.detectChanges();
  }

  // Calculate optimal tooltip position for global tooltip with robust boundary checking
  private calculateTooltipPosition(buttonElement: HTMLElement) {
    // Get viewport dimensions with safety margin
    const viewport = {
      width: window.innerWidth,
      height: window.innerHeight,
    };

    // Get button position relative to viewport
    const buttonRect = buttonElement.getBoundingClientRect();

    // Tooltip dimensions matching CSS constraints exactly
    const tooltipWidth = Math.min(340, viewport.width - 32); // Match CSS: min(340px, calc(100vw - 32px))
    const tooltipHeight = Math.min(360, viewport.height - 32); // Match CSS: calc(100vh - 32px)
    const safetyPadding = 16; // Safety margin
    const offset = 8; // Distance from button

    // Calculate available space in each direction
    const spaceRight = viewport.width - buttonRect.right - safetyPadding;
    const spaceLeft = buttonRect.left - safetyPadding;
    const spaceBelow = viewport.height - buttonRect.bottom - safetyPadding;
    const spaceAbove = buttonRect.top - safetyPadding;

    let x = 0;
    let y = 0;

    // Enhanced horizontal positioning with priority-based fallbacks
    if (spaceRight >= tooltipWidth) {
      // Position to the right of button
      x = buttonRect.right + offset;
    } else if (spaceLeft >= tooltipWidth) {
      // Position to the left of button
      x = buttonRect.left - tooltipWidth - offset;
    } else {
      // Force center positioning with viewport constraints
      x = safetyPadding;
    }

    // Enhanced vertical positioning with priority-based fallbacks
    if (spaceBelow >= tooltipHeight) {
      // Position below button, aligned to button top
      y = buttonRect.top;
    } else if (spaceAbove >= tooltipHeight) {
      // Position above button, aligned to button bottom
      y = buttonRect.bottom - tooltipHeight;
    } else {
      // Force center positioning with viewport constraints
      y = safetyPadding;
    }

    // ENHANCED: Multiple layers of boundary protection
    const safetyMargin = 8; // Additional margin for extra safety
    const minX = safetyMargin;
    const maxX = viewport.width - tooltipWidth - safetyMargin;
    const minY = safetyMargin;
    const maxY = viewport.height - tooltipHeight - safetyMargin;

    // Apply stricter constraints
    x = Math.max(minX, Math.min(x, maxX));
    y = Math.max(minY, Math.min(y, maxY));

    // Store position
    this.tooltipPosition = { x, y };

    // Enhanced error handling: If still clipping, force safe positioning
    const withinBounds = {
      left: x >= 0,
      right: x + tooltipWidth <= viewport.width,
      top: y >= 0,
      bottom: y + tooltipHeight <= viewport.height,
    };

    if (!withinBounds.right || !withinBounds.bottom || !withinBounds.left || !withinBounds.top) {
      console.warn('Tooltip positioning: applying emergency viewport constraints');

      // Emergency fallback: Force tooltip to safe area
      x = Math.max(safetyMargin, Math.min(x, viewport.width - tooltipWidth - safetyMargin));
      y = Math.max(safetyMargin, Math.min(y, viewport.height - tooltipHeight - safetyMargin));

      this.tooltipPosition = { x, y };
    }
  }

  // Enhanced validation method to check actual DOM positioning after render
  private validateTooltipPositioning() {
    if (!this.activeTooltipError) return;

    const tooltipElement = document.querySelector('.global-error-tooltip') as HTMLElement;
    if (!tooltipElement) return;

    // Get actual rendered dimensions and position
    const tooltipRect = tooltipElement.getBoundingClientRect();
    const viewport = {
      width: window.innerWidth,
      height: window.innerHeight,
    };

    const isClipping =
      tooltipRect.right > viewport.width ||
      tooltipRect.bottom > viewport.height ||
      tooltipRect.left < 0 ||
      tooltipRect.top < 0;

    // If tooltip is still clipping, apply emergency repositioning
    if (isClipping) {
      const safetyMargin = 8;
      let correctedX = this.tooltipPosition.x;
      let correctedY = this.tooltipPosition.y;

      const clipping = {
        rightClip: Math.max(0, tooltipRect.right - viewport.width),
        bottomClip: Math.max(0, tooltipRect.bottom - viewport.height),
        leftClip: Math.max(0, -tooltipRect.left),
        topClip: Math.max(0, -tooltipRect.top),
      };

      // Correct horizontal clipping
      if (clipping.rightClip > 0) {
        correctedX = viewport.width - tooltipRect.width - safetyMargin;
      }
      if (clipping.leftClip > 0) {
        correctedX = safetyMargin;
      }

      // Correct vertical clipping
      if (clipping.bottomClip > 0) {
        correctedY = viewport.height - tooltipRect.height - safetyMargin;
      }
      if (clipping.topClip > 0) {
        correctedY = safetyMargin;
      }

      // Apply corrected position
      this.tooltipPosition = { x: correctedX, y: correctedY };
      this.cdr.detectChanges();
    }
  }

  // Close global tooltip when clicking outside
  private closeAllPopups(event?: Event) {
    if (event) {
      // Don't close if clicking on global tooltip or info icon buttons
      const target = event.target as HTMLElement;
      if (target.closest('.global-error-tooltip') || target.closest('.info-icon-btn')) {
        return;
      }
    }

    // Close global tooltip
    this.closeGlobalTooltip();

    // Legacy: Close any remaining old-style popups (for compatibility)
    this.selectedFilesWithQuality.forEach(file => {
      file.showDetails = false;
    });
  }

  // Inline Feedback Management
  dismissInlineFeedback(): void {
    this.invalidFilesFeedback = [];
    this.cdr.detectChanges();
  }

  // Helper Methods
  getValidFilesCount(): number {
    return this.selectedFilesWithQuality.filter(f => f.isValid).length;
  }

  getInvalidFilesCount(): number {
    return this.qualityCheckErrors.length;
  }

  hasSelectedFiles(): boolean {
    return this.selectedFiles.length > 0;
  }

  canUpload(): boolean {
    return (
      this.hasSelectedFiles() &&
      !this.isUploading &&
      !this.isCheckingQuality &&
      this.getValidFilesCount() > 0
    );
  }

  // Compact error message utility
  getCompactErrorMessage(message: string): string {
    const messageMap: Record<string, string> = {
      'No face detected in image. Please upload a clear photo with your face visible.':
        'No face detected',
      'Unable to determine photo composition. Please upload a clear headshot or upper body photo.':
        'Unclear composition',
      'Image quality is below recommended standards. Consider uploading a higher quality photo.':
        'Low image quality',
      'Full body photo detected. Please upload headshot or upper body photos only.':
        'Full body photo detected',
      'Multiple faces detected in image. Please upload a photo with only one person.':
        'Multiple faces detected',
      'Face is too small in the image. Please upload a closer headshot.': 'Face too small',
      'Image is too blurry. Please upload a sharper photo.': 'Image too blurry',
      'Poor lighting detected. Please upload a well-lit photo.': 'Poor lighting',
    };

    // Return mapped message if found, otherwise truncate long messages
    if (messageMap[message]) {
      return messageMap[message];
    }

    // Truncate messages longer than 40 characters
    if (message.length > 40) {
      return message.substring(0, 37) + '...';
    }

    return message;
  }

  // Compact suggestion utility for UX-optimized tooltips
  getCompactSuggestion(suggestion: string): string {
    const suggestionMap: Record<string, string> = {
      'Try uploading a clearer photo with better lighting.': 'Use better lighting',
      'Upload a closer headshot photo.': 'Take closer photo',
      'Ensure only one person is in the photo.': 'Remove other people',
      'Use a higher resolution image.': 'Higher resolution',
      'Take photo with better focus.': 'Better focus needed',
      'Improve lighting conditions.': 'Better lighting',
      'Remove sunglasses or accessories covering face.': 'Remove accessories',
    };

    // Return mapped suggestion if found, otherwise truncate
    if (suggestionMap[suggestion]) {
      return suggestionMap[suggestion];
    }

    // Truncate suggestions longer than 25 characters for tooltip
    if (suggestion.length > 25) {
      return suggestion.substring(0, 22) + '...';
    }

    return suggestion;
  }

  // Truncate filename for better card layout consistency (kept for compatibility)
  truncateFilename(filename: string): string {
    if (!filename) return '';

    // Extract name and extension
    const lastDotIndex = filename.lastIndexOf('.');
    const name = lastDotIndex > -1 ? filename.substring(0, lastDotIndex) : filename;
    const extension = lastDotIndex > -1 ? filename.substring(lastDotIndex) : '';

    // Truncate to max 12 characters for the name part
    const maxNameLength = 12;
    if (name.length > maxNameLength) {
      return name.substring(0, maxNameLength) + '...' + extension;
    }

    return filename;
  }

  // UI State Getters
  get showUploadGuidelines(): boolean {
    return (
      this.currentStep === 1 &&
      this.selectedFiles.length === 0 &&
      this.uploadedImageThumbnails.length === 0
    );
  }

  get showSelectedFiles(): boolean {
    return this.selectedFiles.length > 0;
  }

  get showUploadedImages(): boolean {
    return this.uploadedImageThumbnails.length > 0;
  }
}
