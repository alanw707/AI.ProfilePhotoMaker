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
import { FileUploadManagerService } from '../../../services/file-upload-manager.service';
import { NotificationService } from '../../../services/notification.service';

// Lazy-loaded service interface
interface FaceDetectionService {
  validateImage(file: File): Promise<any>;
}

import {
  QualityCheckError,
  QualityCheckResult,
  SelectedFileWithQuality,
  UploadProgress,
} from '../../../models/dashboard.types';

export interface FileUploadState {
  selectedFiles: File[];
  selectedFilesWithQuality: SelectedFileWithQuality[];
  isUploading: boolean;
  uploadProgress: number;
  isDragOver: boolean;
  isCheckingQuality: boolean;
  qualityCheckProgress: string;
  qualityCheckErrors: QualityCheckError[];
}

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

  // File preview cache for memory management
  private filePreviewCache = new Map<File, string>();

  // Lazy-loaded service
  private faceDetectionService: FaceDetectionService | null = null;

  constructor(
    private fileUploadService: FileUploadService,
    private fileUploadManagerService: FileUploadManagerService,
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

    // Filter valid files
    const validFiles = files.filter(file => this.isValidFile(file));
    if (validFiles.length === 0) {
      this.notificationService.error(
        'Invalid Files',
        'No valid image files were selected. Please select JPEG, PNG, or WebP files under 7MB.'
      );
      return;
    }

    // Add valid files to selection
    this.selectedFiles.push(...validFiles);
    this.filesSelected.emit(this.selectedFiles);

    // Start quality validation
    await this.validateImageQuality(validFiles);
  }

  private isValidFile(file: File): boolean {
    // Check file type
    if (!this.allowedTypes.includes(file.type)) {
      this.notificationService.error(
        'Invalid File Type',
        `${file.name} is not a supported image format. Please use JPEG, PNG, or WebP files.`
      );
      return false;
    }

    // Check file size
    if (file.size > this.maxFileSize) {
      const sizeInMB = (this.maxFileSize / (1024 * 1024)).toFixed(1);
      this.notificationService.error(
        'File Too Large',
        `${file.name} is too large. Please use files smaller than ${sizeInMB}MB.`
      );
      return false;
    }

    return true;
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
          });
        }
      } catch (error) {
        console.error(`Quality check failed for ${file.name}:`, error);
        errors.push({
          fileName: file.name,
          file,
          errors: ['Failed to analyze image quality'],
          warnings: [],
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
    // console.log(`✅ Image loaded successfully: ${thumb.url}`);
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

  // Toggle popup visibility (positioning handled by CSS)
  toggleErrorDetails(error: QualityCheckError, event: Event) {
    event.stopPropagation();

    // Close other open popups
    this.qualityCheckErrors.forEach(e => {
      if (e !== error) {
        e.showErrorDetails = false;
      }
    });

    error.showErrorDetails = !error.showErrorDetails;
  }

  // Close all error detail popups
  private closeAllPopups(event?: Event) {
    if (event) {
      // Don't close if clicking on popup or its children
      const target = event.target as HTMLElement;
      if (target.closest('.error-details-popup') || target.closest('.error-info-btn')) {
        return;
      }
    }

    this.qualityCheckErrors.forEach(error => {
      error.showErrorDetails = false;
    });

    this.selectedFilesWithQuality.forEach(file => {
      file.showDetails = false;
    });
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
