import { Component, OnInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HeaderNavigationComponent } from '../shared/header-navigation/header-navigation.component';
import { StatsCardComponent } from '../components/dashboard/stats-card/stats-card.component';
import { StyleSelectorComponent, StyleOption } from '../components/dashboard/style-selector/style-selector.component';
import { AuthService } from '../services/auth.service';
import { GalleryImage } from '../components/photo-gallery/photo-gallery.component';
import { FileUploadService } from '../services/file-upload.service';
import { StyleService, Style } from '../services/style.service';
import { NotificationService } from '../services/notification.service';
import { CreditService } from '../services/credit.service';
import { DashboardStateService } from '../services/dashboard-state.service';
import { FaceDetectionService, FaceValidationResult, QualityScore } from '../services/face-detection.service';
import { ConfigService } from '../services/config.service';
import { Observable } from 'rxjs';


interface GeneratedPhoto {
  id: string;
  url: string;
  style: string;
  createdAt: Date;
}

interface QualityCheckError {
  fileName: string;
  file: File;
  errors: string[];
  warnings?: string[];
  faceValidation?: FaceValidationResult;
  qualityScore?: QualityScore;
}

interface SelectedFileWithQuality {
  file: File;
  qualityScore?: QualityScore;
  faceValidation?: FaceValidationResult;
  errors: string[];
  warnings: string[];
  isValid: boolean;
  showDetails?: boolean; // For expandable details UI state
}

interface QualityCheckResult {
  validFiles: File[];
  errorFiles: QualityCheckError[];
}


@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, HeaderNavigationComponent, StatsCardComponent, StyleSelectorComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.sass']
})
export class DashboardComponent implements OnInit, OnDestroy {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  state$: Observable<any>;

  // Component-specific state
  currentStep: number = 1;
  selectedFiles: File[] = [];
  selectedFilesWithQuality: SelectedFileWithQuality[] = [];
  isUploading: boolean = false;
  uploadProgress: number = 0;
  isDragOver: boolean = false;
  isCheckingQuality: boolean = false;
  qualityCheckProgress: string = '';
  trainingProgress: number = 0;
  estimatedCompletion: string = '';
  trainingZipPath: string = '';
  isTrainingStarted: boolean = false;
  trainingId: string = '';
  imagesPerStyle: number = 2;
  availableStyles: StyleOption[] = [];
  isGenerating: boolean = false;
  isDownloadingZip: boolean = false;
  galleryImages: GalleryImage[] = [];
  generatedPhotos: GeneratedPhoto[] = [];
  selectedStyles: number = 0;
  qualityCheckErrors: QualityCheckError[] = [];
  
  private filePreviewCache = new Map<File, string>();

  // State-based getters for template
  get uploadedImages(): number {
    return this.stateService.getState().uploadedImages;
  }

  get modelStatus(): string {
    return this.stateService.getState().modelStatus;
  }

  get creditsInfo(): any {
    return this.stateService.getState().creditsInfo;
  }

  get userCreditStatus(): any {
    return this.stateService.getState().userCreditStatus;
  }

  get uploadedImageThumbnails(): Array<{id: number; url: string; fileName: string}> {
    return this.stateService.getState().uploadedImageThumbnails;
  }

  getTotalAvailableCredits(): number {
    const weeklyCredits = this.getWeeklyCredits();
    const purchasedCredits = this.getPurchasedCredits();
    
    // Always calculate total from individual components to ensure accuracy
    return weeklyCredits + purchasedCredits;
  }

  getPurchasedCredits(): number {
    return this.userCreditStatus?.purchasedCredits || 0;
  }

  getWeeklyCredits(): number {
    // Use weeklyCredits from userCreditStatus first, fallback to creditsInfo.availableCredits
    return this.userCreditStatus?.weeklyCredits || this.creditsInfo?.availableCredits || 0;
  }

  constructor(
    private authService: AuthService,
    private router: Router,
    private fileUploadService: FileUploadService,
    private styleService: StyleService,
    private notificationService: NotificationService,
    public creditService: CreditService,
    public stateService: DashboardStateService,
    private faceDetectionService: FaceDetectionService,
    private config: ConfigService
  ) {
    this.state$ = this.stateService.state$;
  }

  ngOnInit() {
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }

    // Subscribe to state changes to update UI
    this.state$.subscribe(_state => {
      // Force change detection when state updates
      this.selectedStyles = this.getSelectedStylesCount();
      
      // Update current step based on progress
      this.updateCurrentStep();
    });

    this.stateService.loadInitialDashboardData();
    this.loadAvailableStyles();
  }

  ngOnDestroy() {
    this.cleanupFilePreviewCache();
    this.stateService.resetState();
  }

  private cleanupFilePreviewCache() {
    this.filePreviewCache.forEach((blobUrl) => {
      URL.revokeObjectURL(blobUrl);
    });
    this.filePreviewCache.clear();
  }

  private updateCurrentStep() {
    // Automatically progress to Step 2 when images are uploaded
    if ((this.uploadedImages > 0 || this.uploadedImageThumbnails.length > 0) && this.currentStep === 1) {
      this.currentStep = 2;
    }
    
    // Progress to Step 3 if photos are generated (future enhancement)
    if (this.generatedPhotos.length > 0 && this.currentStep === 2) {
      this.currentStep = 3;
    }
  }

  private loadAvailableStyles() {
    this.styleService.getActiveStyles().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.availableStyles = response.data.map(style => ({
            id: style.id.toString(),
            name: style.name,
            description: style.description,
            previewUrl: this.getStylePreviewUrl(style.name),
            selected: false
          }));
        } else {
          console.error('Failed to load styles:', response.error);
          this.notificationService.error('Style Load Failed', 'Could not load available styles. Please refresh the page.');
        }
      },
      error: (error) => {
        console.error('Error loading styles:', error);
        this.notificationService.error('Style Load Failed', 'Could not load available styles. Please refresh the page.');
      }
    });
  }

  private getStylePreviewUrl(styleName: string): string {
    // Use our API server's style preview images
    // Convert style name to filename format (lowercase, replace spaces and slashes with hyphens)
    const fileName = styleName.toLowerCase().replace(/[\s\/]+/g, '-');
    
    // Add cache busting parameter to prevent browser caching of updated images
    const cacheBuster = Date.now();
    
    return `${this.config.getApiUrl()}/style-previews/${fileName}.jpg?v=${cacheBuster}`;
  }

  // UI Event Handlers
  triggerFileUpload() {
    this.fileInput.nativeElement.click();
  }

  removeFile(idx: number) {
    this.selectedFiles.splice(idx, 1);
    this.selectedFilesWithQuality.splice(idx, 1);
  }

  deleteUploadedImage(thumb: any, _idx: number) {
    // Delete from server
    this.fileUploadService.deleteImage(thumb.id).subscribe({
      next: (response) => {
        if (response.success) {
          // Update state by removing the thumbnail
          const currentThumbnails = this.stateService.getState().uploadedImageThumbnails;
          const updatedThumbnails = currentThumbnails.filter(t => t.id !== thumb.id);
          this.stateService.setState({ 
            uploadedImageThumbnails: updatedThumbnails,
            uploadedImages: updatedThumbnails.length 
          });
          this.notificationService.success('Image Deleted', 'Image has been successfully deleted.');
        } else {
          this.notificationService.error('Delete Failed', 'Failed to delete image. Please try again.');
        }
      },
      error: (error) => {
        console.error('Delete image error:', error);
        this.notificationService.error('Delete Failed', 'Failed to delete image. Please try again.');
      }
    });
  }

  uploadImages() {
    if (this.selectedFiles.length === 0) {
      this.notificationService.error('Upload Error', 'Please select at least one image to upload');
      return;
    }
    
    this.isUploading = true;
    this.uploadProgress = 0;
    
    // Use real file upload service
    this.fileUploadService.uploadImages(this.selectedFiles, undefined, true).subscribe({
      next: (result) => {
        if (result.progress !== undefined) {
          this.uploadProgress = result.progress;
        }
        
        if (result.response) {
          // Upload completed successfully
          this.isUploading = false;
          this.uploadProgress = 100;
          
          // Add uploaded images to state
          const newThumbnails = result.response.uploadedFiles.map((file, idx) => ({
            id: result.response!.uploadedImageIds[idx] || Date.now() + idx,
            url: file.url,
            fileName: file.fileName
          }));
          
          const currentThumbnails = this.stateService.getState().uploadedImageThumbnails;
          const updatedThumbnails = [...currentThumbnails, ...newThumbnails];
          
          this.stateService.setState({ 
            uploadedImageThumbnails: updatedThumbnails,
            uploadedImages: updatedThumbnails.length 
          });
          
          // Clear selected files and reset
          this.selectedFiles = [];
          this.selectedFilesWithQuality = [];
          this.qualityCheckErrors = [];
          this.filePreviewCache.clear();
          
          this.currentStep = 2;
          this.notificationService.success('Upload Success', 
            `${result.response.uploadedFiles.length} image(s) uploaded successfully`);
        }
      },
      error: (error) => {
        console.error('Upload error:', error);
        this.isUploading = false;
        this.uploadProgress = 0;
        this.notificationService.error('Upload Failed', 'Failed to upload images. Please try again.');
      }
    });
  }

  selectAllStyles() {
    this.availableStyles.forEach(style => style.selected = true);
    // Update selected styles count immediately
    this.selectedStyles = this.getSelectedStylesCount();
  }

  deselectAllStyles() {
    this.availableStyles.forEach(style => style.selected = false);
    // Update selected styles count immediately
    this.selectedStyles = this.getSelectedStylesCount();
  }

  toggleStyle(style: StyleOption) {
    style.selected = !style.selected;
    // Update selected styles count immediately
    this.selectedStyles = this.getSelectedStylesCount();
  }

  onStyleToggled(style: StyleOption) {
    this.toggleStyle(style);
  }

  onImagesPerStyleChanged(count: number) {
    this.imagesPerStyle = count;
  }

  onSelectAllStyles() {
    this.selectAllStyles();
  }

  onDeselectAllStyles() {
    this.deselectAllStyles();
  }

  onStartTraining() {
    this.startTrainingWithStyles();
  }

  startTrainingWithStyles() {
    const selectedStyles = this.availableStyles.filter(s => s.selected);
    if (selectedStyles.length === 0) {
      this.notificationService.error('Training Error', 'Please select at least one style');
      return;
    }
    
    this.isTrainingStarted = true;
    this.currentStep = 3;
    // Start training logic here
  }

  downloadPhoto(photo: GeneratedPhoto) {
    // Create a download link for the photo
    const link = document.createElement('a');
    link.href = photo.url;
    link.download = `generated-photo-${photo.style}-${photo.id}.jpg`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  sharePhoto(photo: GeneratedPhoto) {
    if (navigator.share) {
      navigator.share({
        title: 'Generated Photo',
        text: `Check out this ${photo.style} style photo!`,
        url: photo.url
      });
    } else {
      // Fallback: copy URL to clipboard
      navigator.clipboard.writeText(photo.url).then(() => {
        this.notificationService.success('Share Success', 'Photo URL copied to clipboard');
      });
    }
  }

  async downloadAll() {
    if (this.generatedPhotos.length === 0) {
      this.notificationService.error('Download Error', 'No photos to download');
      return;
    }

    this.isDownloadingZip = true;
    
    try {
      // Lazy load JSZip only when needed
      const JSZip = (await import('jszip')).default;
      const zip = new JSZip();
      const promises: Promise<void>[] = [];

      this.generatedPhotos.forEach((photo) => {
        const promise = fetch(photo.url)
          .then(response => response.blob())
          .then(blob => {
            const filename = `generated-photo-${photo.style}-${photo.id}.jpg`;
            zip.file(filename, blob);
          })
          .catch(error => {
            console.error(`Failed to download photo ${photo.id}:`, error);
          });
        
        promises.push(promise);
      });

      await Promise.all(promises);
      
      const content = await zip.generateAsync({ type: 'blob' });
      const link = document.createElement('a');
      link.href = URL.createObjectURL(content);
      link.download = 'generated-photos.zip';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(link.href);
      
      this.isDownloadingZip = false;
      this.notificationService.success('Download Success', 'All photos downloaded successfully');
    } catch (error) {
      console.error('Failed to create zip file:', error);
      this.isDownloadingZip = false;
      this.notificationService.error('Download Error', 'Failed to download photos');
    }
  }


  onImageError(event: any) {
    // Fallback to a dynamically generated placeholder image from our API server
    event.target.src = `${this.config.getApiUrl()}/api/placeholder/style-preview`;
    
    // Remove the error event listener to prevent infinite loop
    event.target.onerror = null;
  }


  // Workflow methods
  isPremiumWorkflow(): boolean {
    return true; // Always show premium workflow for now
  }

  getStepStatus(step: number): string {
    const hasUploadedImages = this.uploadedImages > 0 || this.uploadedImageThumbnails.length > 0;
    
    switch (step) {
      case 1:
        // Step 1 is completed when user has uploaded images
        if (hasUploadedImages) return 'completed';
        if (this.currentStep === 1) return 'active';
        return 'pending';
      
      case 2:
        // Step 2 is active when Step 1 is completed (has uploaded images)
        if (hasUploadedImages && this.generatedPhotos.length === 0) return 'active';
        if (this.generatedPhotos.length > 0) return 'completed';
        return 'pending';
      
      case 3:
        // Step 3 is active when photos are generated
        if (this.generatedPhotos.length > 0) return 'active';
        return 'pending';
      
      default:
        if (step < this.currentStep) return 'completed';
        if (step === this.currentStep) return 'active';
        return 'pending';
    }
  }

  getStepStatusText(step: number): string {
    const status = this.getStepStatus(step);
    switch (status) {
      case 'completed': return 'Completed';
      case 'active': return 'In Progress';
      default: return 'Pending';
    }
  }

  // File handling methods
  onFileSelected(event: any) {
    const files = event.target.files;
    if (files) {
      this.handleFileSelection(Array.from(files));
    }
  }

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
    
    const files = event.dataTransfer?.files;
    if (files) {
      this.handleFileSelection(Array.from(files));
    }
  }

  async handleFileSelection(files: File[]) {
    // Reset previous quality check errors
    this.qualityCheckErrors = [];
    
    // Perform quality validation
    this.isCheckingQuality = true;
    this.qualityCheckProgress = 'Validating images...';
    
    try {
      const qualityResult = await this.validateImageQuality(files);
      
      // Create selected files with quality data for preview
      const newSelectedFilesWithQuality: SelectedFileWithQuality[] = [];
      
      // Add valid files
      for (const file of qualityResult.validFiles) {
        // Find quality data for this file from error files (which may contain warnings)
        const qualityData = qualityResult.errorFiles.find(ef => ef.file === file);
        newSelectedFilesWithQuality.push({
          file: file,
          qualityScore: qualityData?.qualityScore,
          faceValidation: qualityData?.faceValidation,
          errors: [],
          warnings: qualityData?.warnings || [],
          isValid: true
        });
      }
      
      // Add invalid files with their error information
      for (const errorFile of qualityResult.errorFiles) {
        if (!qualityResult.validFiles.includes(errorFile.file)) {
          newSelectedFilesWithQuality.push({
            file: errorFile.file,
            qualityScore: errorFile.qualityScore,
            faceValidation: errorFile.faceValidation,
            errors: errorFile.errors,
            warnings: errorFile.warnings || [],
            isValid: false
          });
        }
      }
      
      // Update both arrays
      this.selectedFilesWithQuality.push(...newSelectedFilesWithQuality);
      this.selectedFiles.push(...qualityResult.validFiles);
      
      // Store quality check errors for display (only invalid files with actual errors)
      this.qualityCheckErrors = qualityResult.errorFiles.filter(ef => 
        ef.errors.length > 0
      );
      
      // Update state service with new selected image count
      this.stateService.setState({ uploadedImages: this.selectedFiles.length });
      
      // Show summary of validation results
      if (qualityResult.validFiles.length > 0) {
        this.notificationService.success('Validation Complete', 
          `${qualityResult.validFiles.length} image(s) ready for upload.`);
      }
      
      if (qualityResult.errorFiles.length > 0) {
        const invalidCount = qualityResult.errorFiles.filter(ef => 
          ef.errors.length > 0
        ).length;
        if (invalidCount > 0) {
          this.notificationService.warning('Validation Issues', 
            `${invalidCount} image(s) failed validation. See details below.`);
        }
      }
      
    } catch (error) {
      console.error('Quality validation error:', error);
      this.notificationService.error('Validation Error', 'Failed to validate images. Please try again.');
    } finally {
      this.isCheckingQuality = false;
      this.qualityCheckProgress = '';
    }
  }

  // Credit calculation methods
  calculateTotalCredits(): number {
    return this.calculateTrainingCredits() + this.calculateGenerationCredits();
  }

  hasEnoughCredits(): boolean {
    const totalRequired = this.calculateTotalCredits();
    const availableCredits = this.getTotalAvailableCredits();
    return availableCredits >= totalRequired;
  }

  getRemainingCredits(): number {
    const totalRequired = this.calculateTotalCredits();
    const availableCredits = this.getTotalAvailableCredits();
    return availableCredits - totalRequired;
  }

  getSelectedStylesCount(): number {
    return this.availableStyles.filter(s => s.selected).length;
  }

  // Helper methods for selected files with quality
  getValidFilesCount(): number {
    return this.selectedFilesWithQuality.filter(f => f.isValid).length;
  }

  getInvalidFilesCount(): number {
    return this.selectedFilesWithQuality.filter(f => !f.isValid).length;
  }

  // Quality check methods
  checkImageQuality() {
    this.isCheckingQuality = true;
    this.qualityCheckProgress = 'Analyzing images...';
    
    // Simulate quality check
    setTimeout(() => {
      this.qualityCheckProgress = 'Quality check complete';
      this.isCheckingQuality = false;
      this.currentStep = 2;
    }, 2000);
  }

  checkAndCorrectImageQuality() {
    this.checkImageQuality();
  }

  // File preview method
  getFilePreview(file: File): string {
    if (this.filePreviewCache.has(file)) {
      return this.filePreviewCache.get(file)!;
    }
    
    const reader = new FileReader();
    reader.onload = (e) => {
      const url = e.target?.result as string;
      this.filePreviewCache.set(file, url);
    };
    reader.readAsDataURL(file);
    
    return ''; // Return empty until loaded
  }

  // Separate credit calculation methods
  calculateTrainingCredits(): number {
    return 15; // Fixed cost for model training
  }

  calculateGenerationCredits(): number {
    const generationCostPerImage = 5;
    const selectedStyleCount = this.availableStyles.filter(s => s.selected).length;
    return selectedStyleCount * this.imagesPerStyle * generationCostPerImage;
  }

  // Image Quality Validation Methods
  async validateImageQuality(files: File[]): Promise<QualityCheckResult> {
    const validFiles: File[] = [];
    const errorFiles: QualityCheckError[] = [];

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      const errors: string[] = [];
      const warnings: string[] = [];
      
      // Update progress
      this.qualityCheckProgress = `Analyzing image ${i + 1} of ${files.length}...`;

      // Basic file validation first
      if (file.size > 7 * 1024 * 1024) {
        errors.push('File size exceeds 7MB limit');
      }

      const validTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];
      if (!validTypes.includes(file.type.toLowerCase())) {
        errors.push('Invalid file type. Only JPG, PNG, and WebP are allowed');
      }

      // Basic dimension check
      try {
        const dimensions = await this.getImageDimensions(file);
        if (dimensions.width < 512 || dimensions.height < 512) {
          errors.push('Image resolution too low. Minimum 512x512 pixels required for processing');
        }
      } catch (error) {
        errors.push('Unable to read image file');
      }

      // If basic validation fails, skip advanced analysis
      if (errors.length > 0) {
        errorFiles.push({
          fileName: file.name,
          file: file,
          errors: errors,
          warnings: warnings
        });
        continue;
      }

      // Advanced face detection and quality analysis
      try {
        this.qualityCheckProgress = `Analyzing face and quality for ${file.name}...`;
        const faceValidation = await this.faceDetectionService.validateImage(file);
        
        // Add face validation errors
        if (!faceValidation.isValid) {
          errors.push(...faceValidation.errors);
        }
        
        // Add quality warnings
        warnings.push(...faceValidation.warnings);

        // Create error/warning entry
        const qualityCheckError: QualityCheckError = {
          fileName: file.name,
          file: file,
          errors: errors,
          warnings: warnings,
          faceValidation: faceValidation,
          qualityScore: faceValidation.qualityScore
        };

        if (errors.length > 0) {
          errorFiles.push(qualityCheckError);
        } else {
          validFiles.push(file);
          // Always add to errorFiles for quality score access, regardless of warnings
          errorFiles.push(qualityCheckError);
        }

      } catch (error) {
        console.error('Face detection error for file:', file.name, error);
        errorFiles.push({
          fileName: file.name,
          file: file,
          errors: ['Unable to analyze image. Please try a different photo.'],
          warnings: warnings
        });
      }
    }

    this.qualityCheckProgress = 'Analysis complete';
    return { validFiles, errorFiles };
  }

  private getImageDimensions(file: File): Promise<{width: number, height: number}> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      const url = URL.createObjectURL(file);
      
      img.onload = () => {
        URL.revokeObjectURL(url);
        resolve({ width: img.naturalWidth, height: img.naturalHeight });
      };
      
      img.onerror = () => {
        URL.revokeObjectURL(url);
        reject(new Error('Failed to load image'));
      };
      
      img.src = url;
    });
  }

}

