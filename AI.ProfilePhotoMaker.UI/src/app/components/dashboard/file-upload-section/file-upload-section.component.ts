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
import { FileSecurityService } from '../../../services/file-security.service';

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

  // Document-level modal elements
  private modalBackdrop: HTMLElement | null = null;
  private modalElement: HTMLElement | null = null;

  // File preview cache for memory management
  private filePreviewCache = new Map<File, string>();

  // Lazy-loaded service
  private faceDetectionService: FaceDetectionService | null = null;

  constructor(
    private fileUploadService: FileUploadService,
    private notificationService: NotificationService,
    private fileSecurityService: FileSecurityService,
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

    // Clean up any open modals
    this.removeDocumentLevelModal();

    // Restore body scroll in case modal was open when component destroyed
    document.body.style.overflow = '';
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

    console.log('🔒 Starting security validation for', files.length, 'files...');

    // Enhanced security validation using FileSecurityService
    const validationResults = {
      validFiles: [] as File[],
      invalidFiles: [] as File[],
      errors: {
        unsupportedType: [] as string[],
        tooLarge: [] as string[],
        securityIssues: [] as { fileName: string; issues: string[]; riskLevel: string }[],
      },
    };

    // Validate each file with comprehensive security checks
    for (const file of files) {
      try {
        const securityValidation = await this.fileSecurityService.validateFile(file).toPromise();

        if (securityValidation?.isValid) {
          validationResults.validFiles.push(file);
          console.log(`✅ File ${file.name} passed security validation`);
        } else {
          validationResults.invalidFiles.push(file);

          // Categorize security issues for user-friendly error messages
          const issues = securityValidation?.securityIssues || ['Security validation failed'];
          const riskLevel = securityValidation?.riskLevel || 'high';

          validationResults.errors.securityIssues.push({
            fileName: file.name,
            issues,
            riskLevel,
          });

          // Map security issues to existing error categories for UI compatibility
          if (issues.some(issue => issue.includes('type') || issue.includes('extension'))) {
            validationResults.errors.unsupportedType.push(file.name);
          }
          if (issues.some(issue => issue.includes('size') || issue.includes('large'))) {
            validationResults.errors.tooLarge.push(file.name);
          }

          console.warn(`❌ File ${file.name} failed security validation:`, {
            issues,
            riskLevel,
          });
        }
      } catch (error) {
        console.error(`Security validation error for ${file.name}:`, error);
        validationResults.invalidFiles.push(file);
        validationResults.errors.securityIssues.push({
          fileName: file.name,
          issues: ['Security validation failed due to internal error'],
          riskLevel: 'critical',
        });
      }
    }

    // Update inline feedback for invalid files with enhanced security messages
    this.invalidFilesFeedback = [];
    validationResults.errors.unsupportedType.forEach(fileName => {
      this.invalidFilesFeedback.push({
        fileName,
        reason: 'File type not allowed. Use JPEG, PNG, or WebP only',
      });
    });
    validationResults.errors.tooLarge.forEach(fileName => {
      const sizeInMB = (this.maxFileSize / (1024 * 1024)).toFixed(1);
      this.invalidFilesFeedback.push({
        fileName,
        reason: `File too large. Maximum: ${sizeInMB}MB`,
      });
    });

    // Add security-specific feedback
    validationResults.errors.securityIssues.forEach(({ fileName, issues, riskLevel }) => {
      // Use the most user-friendly issue message
      const primaryIssue = issues[0] || 'Security check failed';
      const reason = this.getSecurityErrorMessage(primaryIssue, riskLevel);

      // Only add if not already covered by type/size errors
      if (
        !validationResults.errors.unsupportedType.includes(fileName) &&
        !validationResults.errors.tooLarge.includes(fileName)
      ) {
        this.invalidFilesFeedback.push({
          fileName,
          reason,
        });
      }
    });

    // Show consolidated error notification if any files were invalid
    if (validationResults.invalidFiles.length > 0) {
      this.showEnhancedSecurityErrors(validationResults);
    }

    // If no valid files, return early
    if (validationResults.validFiles.length === 0) {
      return;
    }

    // Add valid files to selection
    this.selectedFiles.push(...validationResults.validFiles);
    this.filesSelected.emit(this.selectedFiles);

    console.log(
      `🔒 Security validation complete: ${validationResults.validFiles.length} valid, ${validationResults.invalidFiles.length} rejected`
    );

    // Start quality validation for security-validated files
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

  private showEnhancedSecurityErrors(results: any): void {
    const errors = [];

    if (results.errors.unsupportedType.length > 0) {
      const count = results.errors.unsupportedType.length;
      const fileList = results.errors.unsupportedType.slice(0, 3).join(', ');
      const more = count > 3 ? ` and ${count - 3} more` : '';
      errors.push(
        `${count} file${count > 1 ? 's have' : ' has'} unsupported format: ${fileList}${more}`
      );
    }

    if (results.errors.tooLarge.length > 0) {
      const count = results.errors.tooLarge.length;
      const sizeInMB = (this.maxFileSize / (1024 * 1024)).toFixed(1);
      const fileList = results.errors.tooLarge.slice(0, 3).join(', ');
      const more = count > 3 ? ` and ${count - 3} more` : '';
      errors.push(
        `${count} file${count > 1 ? 's exceed' : ' exceeds'} size limit (${sizeInMB}MB): ${fileList}${more}`
      );
    }

    // Add security-specific errors
    const criticalFiles = results.errors.securityIssues.filter(
      (s: any) => s.riskLevel === 'critical'
    );
    const highRiskFiles = results.errors.securityIssues.filter((s: any) => s.riskLevel === 'high');

    if (criticalFiles.length > 0) {
      const fileList = criticalFiles
        .slice(0, 2)
        .map((s: any) => s.fileName)
        .join(', ');
      const more = criticalFiles.length > 2 ? ` and ${criticalFiles.length - 2} more` : '';
      errors.push(
        `${criticalFiles.length} file${criticalFiles.length > 1 ? 's have' : ' has'} critical security issues: ${fileList}${more}`
      );
    }

    if (highRiskFiles.length > 0) {
      const fileList = highRiskFiles
        .slice(0, 2)
        .map((s: any) => s.fileName)
        .join(', ');
      const more = highRiskFiles.length > 2 ? ` and ${highRiskFiles.length - 2} more` : '';
      errors.push(
        `${highRiskFiles.length} file${highRiskFiles.length > 1 ? 's have' : ' has'} security concerns: ${fileList}${more}`
      );
    }

    const title = criticalFiles.length > 0 ? 'Security Issues Detected' : 'File Validation Failed';
    const message = errors.join('. ') + '. Only secure JPEG, PNG, and WebP files are allowed.';

    this.notificationService.error(title, message, 6000);
  }

  private getSecurityErrorMessage(issue: string, riskLevel: string): string {
    // Map technical security issues to user-friendly messages
    const errorMap: Record<string, string> = {
      'File size': 'File too large',
      'File type': 'Invalid file type',
      'File extension': 'Invalid file extension',
      'Empty files': 'Empty file not allowed',
      'path traversal': 'Unsafe filename detected',
      'dangerous pattern': 'Potentially harmful file',
      mismatch: 'File type mismatch detected',
      'validation failed': 'Security check failed',
    };

    // Find matching error message
    for (const [key, message] of Object.entries(errorMap)) {
      if (issue.toLowerCase().includes(key.toLowerCase())) {
        return riskLevel === 'critical' ? `🚨 ${message}` : message;
      }
    }

    // Fallback for unknown issues
    return riskLevel === 'critical' ? '🚨 Critical security issue' : 'Security validation failed';
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
        `${errors.length} image(s) failed quality validation. Please review and try again.`,
        8000 // 8 seconds - longer than success to account for user reading time
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

  // Show global tooltip with document.body level positioning
  showGlobalTooltip(error: QualityCheckError, event: Event) {
    event.stopPropagation();

    // Close any existing modal first
    this.closeGlobalTooltip();

    // Set active tooltip error
    this.activeTooltipError = error;

    // Create modal elements at document.body level
    this.createDocumentLevelModal(error);

    // Prevent body scroll
    document.body.style.overflow = 'hidden';

    // Force change detection for component state
    this.cdr.detectChanges();
  }

  // Close global tooltip
  closeGlobalTooltip() {
    this.activeTooltipError = null;

    // Remove document-level modal elements
    this.removeDocumentLevelModal();

    // Restore body scroll
    document.body.style.overflow = '';

    this.cdr.detectChanges();
  }

  // Simple center-modal tooltip positioning (now handled by CSS flexbox)
  private calculateTooltipPosition(buttonElement: HTMLElement) {
    // No positioning needed - CSS flexbox handles centering automatically
    // Keep this method for compatibility but remove positioning logic
    this.tooltipPosition = { x: 0, y: 0 };
  }

  // Create modal elements at document.body level with bulletproof inline positioning
  private createDocumentLevelModal(error: QualityCheckError): void {
    // Create backdrop element
    this.modalBackdrop = document.createElement('div');
    this.modalBackdrop.className = 'global-modal-backdrop';

    // Apply bulletproof backdrop positioning via inline styles (bypasses ViewEncapsulation)
    const backdropStyles = this.modalBackdrop.style;
    backdropStyles.position = 'fixed';
    backdropStyles.top = '0';
    backdropStyles.left = '0';
    backdropStyles.right = '0';
    backdropStyles.bottom = '0';
    backdropStyles.width = '100vw';
    backdropStyles.height = '100vh';
    backdropStyles.zIndex = '999998';
    backdropStyles.display = 'flex';
    backdropStyles.alignItems = 'center';
    backdropStyles.justifyContent = 'center';
    backdropStyles.flexDirection = 'column';
    backdropStyles.background = 'rgba(0, 0, 0, 0.4)';
    backdropStyles.backdropFilter = 'blur(4px)';
    backdropStyles.margin = '0';
    backdropStyles.padding = '0';
    backdropStyles.border = 'none';
    backdropStyles.outline = 'none';
    backdropStyles.boxSizing = 'border-box';

    // Create modal container
    this.modalElement = document.createElement('div');
    this.modalElement.className = 'global-error-tooltip';
    this.modalElement.setAttribute('role', 'dialog');
    this.modalElement.setAttribute('aria-modal', 'true');
    this.modalElement.setAttribute('aria-labelledby', 'tooltip-title');
    this.modalElement.setAttribute('tabindex', '-1');

    // Apply bulletproof modal positioning via inline styles
    const modalStyles = this.modalElement.style;
    modalStyles.position = 'relative';
    modalStyles.margin = '0';
    modalStyles.transform = 'none';
    modalStyles.top = 'auto';
    modalStyles.left = 'auto';
    modalStyles.right = 'auto';
    modalStyles.bottom = 'auto';
    modalStyles.width = 'min(400px, calc(100vw - 32px))';
    modalStyles.maxHeight = 'min(480px, calc(100vh - 80px))';
    modalStyles.maxWidth = '90vw';
    modalStyles.minHeight = '200px';
    modalStyles.zIndex = '999999';
    modalStyles.background =
      'linear-gradient(135deg, rgba(30, 41, 59, 0.98) 0%, rgba(51, 65, 85, 0.96) 100%)';
    modalStyles.border = '1px solid rgba(71, 85, 105, 0.4)';
    modalStyles.borderRadius = '12px';
    modalStyles.boxShadow =
      '0 25px 50px -12px rgba(0, 0, 0, 0.5), 0 10px 20px -5px rgba(0, 0, 0, 0.3)';
    modalStyles.backdropFilter = 'blur(20px) saturate(130%)';
    modalStyles.display = 'flex';
    modalStyles.flexDirection = 'column';
    modalStyles.overflow = 'hidden';

    // Build modal content
    this.modalElement.innerHTML = this.buildModalContent(error);

    // Apply comprehensive theme-aware inline styles to inner elements
    this.applyModalContentStyles();

    // Add event listeners
    this.modalBackdrop.addEventListener('click', () => this.closeGlobalTooltip());
    this.modalElement.addEventListener('click', e => e.stopPropagation());

    // Add close button listener
    const closeButton = this.modalElement.querySelector('.tooltip-close');
    if (closeButton) {
      closeButton.addEventListener('click', () => this.closeGlobalTooltip());
    }

    // Add ESC key listener
    document.addEventListener('keydown', this.handleModalKeydown);

    // Append to DOM
    this.modalBackdrop.appendChild(this.modalElement);
    document.body.appendChild(this.modalBackdrop);

    // Focus the modal for accessibility
    setTimeout(() => this.modalElement?.focus(), 100);
  }

  // Apply comprehensive theme-aware inline styles to modal content elements
  private applyModalContentStyles(): void {
    if (!this.modalElement) {
      return;
    }

    // Detect theme (default to dark if not found)
    const isDarkTheme =
      !document.body.hasAttribute('data-theme') ||
      document.body.getAttribute('data-theme') !== 'light';

    // Theme color schemes
    const colors = isDarkTheme
      ? {
          // Dark theme colors
          bg: 'rgba(30, 41, 59, 0.98)',
          bgSecondary: 'rgba(51, 65, 85, 0.96)',
          border: 'rgba(71, 85, 105, 0.4)',
          text: 'rgba(248, 250, 252, 0.95)',
          textSecondary: 'rgba(203, 213, 225, 0.9)',
          textTertiary: 'rgba(148, 163, 184, 0.8)',
          error: 'rgba(248, 113, 113, 0.95)',
          errorBg: 'rgba(239, 68, 68, 0.12)',
          errorBorder: 'rgba(248, 113, 113, 0.6)',
          warning: 'rgba(251, 191, 36, 0.95)',
          warningBg: 'rgba(245, 158, 11, 0.12)',
          success: 'rgba(34, 197, 94, 0.95)',
          successBg: 'rgba(34, 197, 94, 0.08)',
          itemBg: 'rgba(71, 85, 105, 0.15)',
          closeBtnHover: 'rgba(255, 255, 255, 0.1)',
        }
      : {
          // Light theme colors
          bg: 'rgba(255, 255, 255, 0.98)',
          bgSecondary: 'rgba(248, 250, 252, 0.96)',
          border: 'rgba(0, 0, 0, 0.1)',
          text: 'rgba(15, 23, 42, 0.95)',
          textSecondary: 'rgba(51, 65, 85, 0.9)',
          textTertiary: 'rgba(100, 116, 139, 0.8)',
          error: '#dc2626',
          errorBg: 'rgba(220, 38, 38, 0.1)',
          errorBorder: 'rgba(220, 38, 38, 0.3)',
          warning: '#d97706',
          warningBg: 'rgba(217, 119, 6, 0.1)',
          success: '#16a34a',
          successBg: 'rgba(22, 163, 74, 0.08)',
          itemBg: 'rgba(0, 0, 0, 0.03)',
          closeBtnHover: 'rgba(0, 0, 0, 0.05)',
        };

    // Update main modal background for theme
    this.modalElement.style.background = `linear-gradient(135deg, ${colors.bg} 0%, ${colors.bgSecondary} 100%)`;
    this.modalElement.style.border = `1px solid ${colors.border}`;

    // Style header
    const header = this.modalElement.querySelector('.tooltip-header') as HTMLElement;
    if (header) {
      header.style.background = `linear-gradient(135deg, ${colors.errorBg} 0%, rgba(185, 28, 28, 0.05) 100%)`;
      header.style.color = colors.error;
      header.style.borderBottom = `1px solid ${colors.border}`;
      header.style.padding = '12px 16px 10px 16px';
      header.style.display = 'flex';
      header.style.alignItems = 'center';
      header.style.justifyContent = 'space-between';
      header.style.fontFamily =
        "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
      header.style.fontWeight = '600';
      header.style.fontSize = '13px';
      header.style.letterSpacing = '-0.02em';

      // Style close button
      const closeBtn = header.querySelector('.tooltip-close') as HTMLElement;
      if (closeBtn) {
        closeBtn.style.background = 'none';
        closeBtn.style.border = 'none';
        closeBtn.style.color = colors.textSecondary;
        closeBtn.style.cursor = 'pointer';
        closeBtn.style.padding = '4px';
        closeBtn.style.borderRadius = '4px';
        closeBtn.style.display = 'flex';
        closeBtn.style.alignItems = 'center';
        closeBtn.style.justifyContent = 'center';
        closeBtn.style.transition = 'all 0.2s ease';

        closeBtn.addEventListener('mouseenter', () => {
          closeBtn.style.color = colors.text;
          closeBtn.style.background = colors.closeBtnHover;
        });
        closeBtn.addEventListener('mouseleave', () => {
          closeBtn.style.color = colors.textSecondary;
          closeBtn.style.background = 'none';
        });
      }
    }

    // Style filename
    const filename = this.modalElement.querySelector('.tooltip-filename') as HTMLElement;
    if (filename) {
      filename.style.fontFamily =
        "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
      filename.style.fontSize = '11px';
      filename.style.fontWeight = '500';
      filename.style.color = colors.textSecondary;
      filename.style.margin = '0 16px 12px 16px';
      filename.style.padding = '6px 10px';
      filename.style.background = colors.itemBg;
      filename.style.borderRadius = '4px';
      filename.style.wordBreak = 'break-all';
      filename.style.borderLeft = `2px solid ${colors.errorBorder}`;
    }

    // Style sections
    const sections = this.modalElement.querySelectorAll('.tooltip-section');
    sections.forEach((section: Element) => {
      const sectionEl = section as HTMLElement;
      sectionEl.style.margin = '0 16px 16px 16px';

      // Style section headers
      const sectionHeader = sectionEl.querySelector('.section-header') as HTMLElement;
      if (sectionHeader) {
        sectionHeader.style.fontFamily =
          "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
        sectionHeader.style.fontWeight = '600';
        sectionHeader.style.fontSize = '11px';
        sectionHeader.style.color = colors.textSecondary;
        sectionHeader.style.margin = '0 0 8px 0';
        sectionHeader.style.display = 'flex';
        sectionHeader.style.alignItems = 'center';
        sectionHeader.style.gap = '6px';
        sectionHeader.style.textTransform = 'uppercase';
        sectionHeader.style.letterSpacing = '0.3px';

        // Color section headers based on type
        if (sectionHeader.classList.contains('error-header')) {
          sectionHeader.style.color = colors.error;
        } else if (sectionHeader.classList.contains('warning-header')) {
          sectionHeader.style.color = colors.warning;
        } else if (sectionHeader.classList.contains('suggestions-header')) {
          sectionHeader.style.color = colors.success;
        }
      }

      // Style issue lists
      const issueLists = sectionEl.querySelectorAll('.issue-list, .suggestion-list');
      issueLists.forEach((list: Element) => {
        const listEl = list as HTMLElement;
        listEl.style.listStyle = 'none';
        listEl.style.padding = '0';
        listEl.style.margin = '0';

        const items = listEl.querySelectorAll('li');
        items.forEach((item: Element) => {
          const itemEl = item as HTMLElement;
          itemEl.style.fontFamily =
            "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
          itemEl.style.fontSize = '10px';
          itemEl.style.lineHeight = '1.4';
          itemEl.style.color = colors.textSecondary;
          itemEl.style.marginBottom = '6px';
          itemEl.style.padding = '6px 10px';
          itemEl.style.background = colors.itemBg;
          itemEl.style.borderRadius = '4px';
          itemEl.style.borderLeft = `2px solid ${colors.errorBorder}`;

          if (itemEl.classList.contains('warning')) {
            itemEl.style.color = colors.warning;
            itemEl.style.borderLeftColor = colors.warning;
          } else if (itemEl.classList.contains('suggestion-item')) {
            itemEl.style.color = colors.success;
            itemEl.style.background = colors.successBg;
            itemEl.style.borderLeftColor = colors.success;
          }
        });
      });

      // Style quality score display
      const scoreDisplay = sectionEl.querySelector(
        '.quality-score-display .score-value'
      ) as HTMLElement;
      if (scoreDisplay) {
        scoreDisplay.style.fontFamily =
          "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
        scoreDisplay.style.fontSize = '20px';
        scoreDisplay.style.fontWeight = '700';
        scoreDisplay.style.padding = '8px 12px';
        scoreDisplay.style.borderRadius = '6px';
        scoreDisplay.style.textAlign = 'center';

        if (scoreDisplay.classList.contains('score-red')) {
          scoreDisplay.style.background = colors.errorBg;
          scoreDisplay.style.color = colors.error;
          scoreDisplay.style.border = `1px solid ${colors.errorBorder}`;
        } else if (scoreDisplay.classList.contains('score-yellow')) {
          scoreDisplay.style.background = colors.warningBg;
          scoreDisplay.style.color = colors.warning;
          scoreDisplay.style.border = `1px solid ${colors.warning}`;
        } else if (scoreDisplay.classList.contains('score-green')) {
          scoreDisplay.style.background = colors.successBg;
          scoreDisplay.style.color = colors.success;
          scoreDisplay.style.border = `1px solid ${colors.success}`;
        }
      }
    });

    // Style scrollable content
    const content = this.modalElement.querySelector('.tooltip-content') as HTMLElement;
    if (content) {
      content.style.flex = '1';
      content.style.overflowY = 'auto';
      content.style.overflowX = 'hidden';
      content.style.minHeight = '0';

      // Custom scrollbar
      const scrollbarColor = isDarkTheme ? 'rgba(255, 255, 255, 0.2)' : 'rgba(0, 0, 0, 0.2)';
      const scrollbarTrack = isDarkTheme ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.05)';

      content.style.scrollbarWidth = 'thin';
      content.style.scrollbarColor = scrollbarColor + ' ' + scrollbarTrack;
    }
  }

  // Remove modal elements from document.body
  private removeDocumentLevelModal(): void {
    if (this.modalBackdrop) {
      // Remove event listeners
      document.removeEventListener('keydown', this.handleModalKeydown);

      // Remove from DOM
      document.body.removeChild(this.modalBackdrop);

      // Clear references
      this.modalBackdrop = null;
      this.modalElement = null;
    }
  }

  // Handle keydown for document-level modal
  private handleModalKeydown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      this.closeGlobalTooltip();
      event.preventDefault();
    }
  };

  // Build modal HTML content
  private buildModalContent(error: QualityCheckError): string {
    const errorsList =
      error.errors?.map(err => `<li class="issue-item error">${err}</li>`).join('') || '';
    const warningsList =
      error.warnings?.map(warn => `<li class="issue-item warning">${warn}</li>`).join('') || '';
    const suggestionsList =
      error.qualityScore?.suggestions
        ?.map(sug => `<li class="suggestion-item">${sug}</li>`)
        .join('') || '';

    const scoreClass = error.qualityScore
      ? error.qualityScore.overall < 50
        ? 'score-red'
        : error.qualityScore.overall < 75
          ? 'score-yellow'
          : 'score-green'
      : 'score-red';

    return `
      <!-- Tooltip Header -->
      <div class="tooltip-header">
        <div class="tooltip-title" id="tooltip-title">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"></circle>
            <path d="m9,9a3,3 0 0 1 6,0c0,2 -3,3 -3,3"></path>
            <path d="m12,17h.01"></path>
          </svg>
          Image Issues
        </div>
        <button class="tooltip-close" title="Close">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18"></line>
            <line x1="6" y1="6" x2="18" y2="18"></line>
          </svg>
        </button>
      </div>

      <!-- Scrollable Content -->
      <div class="tooltip-content">
        <!-- File Name -->
        <div class="tooltip-filename">${error.fileName}</div>

        ${
          error.errors?.length
            ? `
        <!-- Errors Section -->
        <div class="tooltip-section">
          <div class="section-header error-header">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="15" y1="9" x2="9" y2="15"></line>
              <line x1="9" y1="9" x2="15" y2="15"></line>
            </svg>
            Issues (${error.errors.length})
          </div>
          <ul class="issue-list">${errorsList}</ul>
        </div>`
            : ''
        }

        ${
          error.warnings?.length
            ? `
        <!-- Warnings Section -->
        <div class="tooltip-section">
          <div class="section-header warning-header">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="m21,16l-9,-15l-9,15l18,0z"></path>
              <line x1="12" y1="9" x2="12" y2="13"></line>
              <line x1="12" y1="17" x2="12.01" y2="17"></line>
            </svg>
            Warnings (${error.warnings.length})
          </div>
          <ul class="issue-list">${warningsList}</ul>
        </div>`
            : ''
        }

        ${
          error.qualityScore
            ? `
        <!-- Quality Score Section -->
        <div class="tooltip-section">
          <div class="section-header score-header">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M9 11H5a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h4a2 2 0 0 0 2-2v-7a2 2 0 0 0-2-2z"></path>
              <path d="M21 11H17a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h4a2 2 0 0 0 2-2v-7a2 2 0 0 0-2-2z"></path>
              <path d="M7 21V10a2 2 0 0 1 2-2h6a2 2 0 0 1 2 2v11"></path>
            </svg>
            Quality Score
          </div>
          <div class="quality-score-display">
            <div class="score-value ${scoreClass}">${error.qualityScore.overall}/100</div>
          </div>
        </div>`
            : ''
        }

        ${
          error.qualityScore?.suggestions?.length
            ? `
        <!-- Suggestions Section -->
        <div class="tooltip-section">
          <div class="section-header suggestions-header">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <path d="m9,12l2,2l4,-4"></path>
            </svg>
            Suggestions (${error.qualityScore.suggestions.length})
          </div>
          <ul class="suggestion-list">${suggestionsList}</ul>
        </div>`
            : ''
        }
      </div>
    `;
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

    // Close global tooltip (this will restore body scroll)
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
    if (!filename) {
      return '';
    }

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
