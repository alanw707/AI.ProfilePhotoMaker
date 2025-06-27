import { Component, OnInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HeaderNavigationComponent } from '../shared/header-navigation/header-navigation.component';
import { AuthService } from '../services/auth.service';
import { GalleryImage } from '../components/photo-gallery/photo-gallery.component';
import { FileUploadService } from '../services/file-upload.service';
import { StyleService, Style } from '../services/style.service';
import { NotificationService } from '../services/notification.service';
import { CreditService } from '../services/credit.service';
import { DashboardStateService } from '../services/dashboard-state.service';
import JSZip from 'jszip';
import * as faceapi from 'face-api.js';
import { Observable } from 'rxjs';

interface StyleOption {
  id: string;
  name: string;
  description: string;
  previewUrl: string;
  selected: boolean;
}

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
}

interface QualityCheckResult {
  validFiles: File[];
  errorFiles: QualityCheckError[];
}


@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, HeaderNavigationComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.sass']
})
export class DashboardComponent implements OnInit, OnDestroy {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  state$: Observable<any>;

  // Component-specific state
  currentStep: number = 1;
  selectedFiles: File[] = [];
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
    const weeklyCredits = this.creditsInfo?.availableCredits || 0;
    const purchasedCredits = this.userCreditStatus?.purchasedCredits || 0;
    const totalCredits = this.userCreditStatus?.totalCredits || 0;
    
    // Use totalCredits if available, otherwise sum weekly + purchased
    return totalCredits || (weeklyCredits + purchasedCredits);
  }

  constructor(
    private authService: AuthService,
    private router: Router,
    private fileUploadService: FileUploadService,
    private styleService: StyleService,
    private notificationService: NotificationService,
    public creditService: CreditService,
    public stateService: DashboardStateService
  ) {
    this.state$ = this.stateService.state$;
  }

  ngOnInit() {
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }

    // Subscribe to state changes to update UI
    this.state$.subscribe(state => {
      // Force change detection when state updates
      this.selectedStyles = this.getSelectedStylesCount();
    });

    this.stateService.loadInitialDashboardData();
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

  // UI Event Handlers
  triggerFileUpload() {
    this.fileInput.nativeElement.click();
  }

  removeFile(index: number) {
    this.selectedFiles.splice(index, 1);
  }

  deleteUploadedImage(thumb: any, index: number) {
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
          const newThumbnails = result.response.uploadedFiles.map((file, index) => ({
            id: result.response!.uploadedImageIds[index] || Date.now() + index,
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
  }

  deselectAllStyles() {
    this.availableStyles.forEach(style => style.selected = false);
  }

  toggleStyle(style: StyleOption) {
    style.selected = !style.selected;
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

  downloadAll() {
    if (this.generatedPhotos.length === 0) {
      this.notificationService.error('Download Error', 'No photos to download');
      return;
    }

    this.isDownloadingZip = true;
    
    const zip = new JSZip();
    const promises: Promise<void>[] = [];

    this.generatedPhotos.forEach((photo, index) => {
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

    Promise.all(promises).then(() => {
      zip.generateAsync({ type: 'blob' }).then(content => {
        const link = document.createElement('a');
        link.href = URL.createObjectURL(content);
        link.download = 'generated-photos.zip';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
        
        this.isDownloadingZip = false;
        this.notificationService.success('Download Success', 'All photos downloaded successfully');
      });
    }).catch(error => {
      console.error('Failed to create zip file:', error);
      this.isDownloadingZip = false;
      this.notificationService.error('Download Error', 'Failed to download photos');
    });
  }


  onImageError(event: any) {
    console.error('Image failed to load:', event);
    // Could replace with a placeholder image
    event.target.src = 'assets/placeholder-image.png';
  }

  // Workflow methods
  isPremiumWorkflow(): boolean {
    return true; // Always show premium workflow for now
  }

  getStepStatus(step: number): string {
    if (step < this.currentStep) return 'completed';
    if (step === this.currentStep) return 'active';
    return 'pending';
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
      
      // Add valid files to selected files
      this.selectedFiles.push(...qualityResult.validFiles);
      
      // Store quality check errors for display
      this.qualityCheckErrors = qualityResult.errorFiles;
      
      // Update state service with new selected image count
      this.stateService.setState({ uploadedImages: this.selectedFiles.length });
      
      // Show summary of validation results
      if (qualityResult.validFiles.length > 0) {
        this.notificationService.success('Validation Complete', 
          `${qualityResult.validFiles.length} image(s) ready for upload.`);
      }
      
      if (qualityResult.errorFiles.length > 0) {
        this.notificationService.warning('Validation Issues', 
          `${qualityResult.errorFiles.length} image(s) failed validation. See details below.`);
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
    const modelTrainingCost = 15; // Cost for model training
    const generationCostPerStyle = 5; // Cost per style generation
    const selectedStyleCount = this.availableStyles.filter(s => s.selected).length;
    
    return modelTrainingCost + (selectedStyleCount * generationCostPerStyle);
  }

  hasEnoughCredits(): boolean {
    const totalRequired = this.calculateTotalCredits();
    const availableCredits = (this.creditsInfo?.availableCredits || 0) + 
                           (this.userCreditStatus?.purchasedCredits || 0);
    return availableCredits >= totalRequired;
  }

  getRemainingCredits(): number {
    const totalRequired = this.calculateTotalCredits();
    const availableCredits = (this.creditsInfo?.availableCredits || 0) + 
                           (this.userCreditStatus?.purchasedCredits || 0);
    return availableCredits - totalRequired;
  }

  getSelectedStylesCount(): number {
    return this.availableStyles.filter(s => s.selected).length;
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
    const generationCostPerStyle = 5;
    const selectedStyleCount = this.availableStyles.filter(s => s.selected).length;
    return selectedStyleCount * generationCostPerStyle;
  }

  // Image Quality Validation Methods
  async validateImageQuality(files: File[]): Promise<QualityCheckResult> {
    const validFiles: File[] = [];
    const errorFiles: QualityCheckError[] = [];

    for (const file of files) {
      const errors: string[] = [];

      // Check file size (5MB max)
      if (file.size > 5 * 1024 * 1024) {
        errors.push('File size exceeds 5MB limit');
      }

      // Check file type
      const validTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];
      if (!validTypes.includes(file.type.toLowerCase())) {
        errors.push('Invalid file type. Only JPG, PNG, and WebP are allowed');
      }

      // Check image dimensions
      try {
        const dimensions = await this.getImageDimensions(file);
        if (dimensions.width < 1024 || dimensions.height < 1024) {
          errors.push('Image resolution too low. Minimum 1024x1024 pixels required');
        }
      } catch (error) {
        errors.push('Unable to read image file');
      }

      // Additional quality checks could be added here
      // (face detection, blur detection, etc.)

      if (errors.length > 0) {
        errorFiles.push({
          fileName: file.name,
          file: file,
          errors: errors
        });
      } else {
        validFiles.push(file);
      }
    }

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

  // Quality Check Error Management
  retryFile(errorFile: QualityCheckError) {
    // Remove this file from errors and attempt to reprocess it
    this.qualityCheckErrors = this.qualityCheckErrors.filter(ef => ef.fileName !== errorFile.fileName);
    
    // Re-validate just this file
    this.handleFileSelection([errorFile.file]);
  }

  clearErrors() {
    this.qualityCheckErrors = [];
  }
}

