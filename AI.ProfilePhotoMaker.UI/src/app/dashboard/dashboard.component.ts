import { Component, OnInit, OnDestroy, ViewChild, ElementRef, NgZone } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';

import { HeaderNavigationComponent } from '../shared/header-navigation/header-navigation.component';
import { StatsCardComponent } from '../components/dashboard/stats-card/stats-card.component';
import { StyleSelectorComponent, StyleOption } from '../components/dashboard/style-selector/style-selector.component';

import { AuthService } from '../services/auth.service';
import { FileUploadService } from '../services/file-upload.service';
import { StyleService, Style } from '../services/style.service';
import { NotificationService } from '../services/notification.service';
import { CreditService } from '../services/credit.service';
import { DashboardStateService } from '../services/dashboard-state.service';
import { FaceDetectionService } from '../services/face-detection.service';
import { ConfigService } from '../services/config.service';
import { ReplicateService, TrainModelRequest, GenerateImagesRequest, GenerateBatchImagesRequest } from '../services/replicate.service';
import { FileUploadManagerService } from '../services/file-upload-manager.service';

import { GalleryImage } from '../components/photo-gallery/photo-gallery.component';
import { 
  GeneratedPhoto, 
  QualityCheckError, 
  SelectedFileWithQuality, 
  QualityCheckResult,
  UploadProgress,
  TrainingStatus,
  GenerationStatus
} from '../models/dashboard.types';




@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    FormsModule, 
    HeaderNavigationComponent, 
    StatsCardComponent, 
    StyleSelectorComponent
  ],
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
  photoCompletionPollingInterval?: any;
  
  // Progress tracking properties
  isTraining: boolean = false;
  progressPercentage: number = 0;
  progressMessage: string = '';
  generationStartTime: number = 0;
  expectedGenerationTime: number = 0;
  timeBasedProgressInterval?: any;
  lastGenerationCount: number = 0;
  showLastGenerationMessage: boolean = false;
  
  private filePreviewCache = new Map<File, string>();
  private pollingInterval?: any;

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

  get generatedPhotosCount(): number {
    return this.stateService.getState().generatedPhotosCount;
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
    private config: ConfigService,
    private replicateService: ReplicateService,
    private fileUploadManager: FileUploadManagerService,
    private ngZone: NgZone
  ) {
    this.state$ = this.stateService.state$;
    
    // Enable debug methods for troubleshooting
    this.stateService.enableGlobalDebug();
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
    // Clear any active polling intervals
    if (this.pollingInterval) {
      clearInterval(this.pollingInterval);
    }
    if (this.photoCompletionPollingInterval) {
      clearInterval(this.photoCompletionPollingInterval);
    }
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
    if (this.generatedPhotosCount > 0 && this.currentStep === 2) {
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

  onDismissSuccessMessage() {
    this.showLastGenerationMessage = false;
    this.lastGenerationCount = 0;
  }

  async startTrainingWithStyles() {
    const selectedStyles = this.availableStyles.filter(s => s.selected);
    if (selectedStyles.length === 0) {
      this.notificationService.error('Training Error', 'Please select at least one style');
      return;
    }

    // Check if user has enough credits
    if (!this.hasEnoughCredits()) {
      this.notificationService.error('Insufficient Credits', 
        `You need ${this.calculateTotalCredits()} credits but only have ${this.getTotalAvailableCredits()}. Please purchase more credits.`);
      return;
    }

    try {
      this.isTrainingStarted = true;
      this.currentStep = 3;

      // UNIFIED LOGIC: Use the same model status that determines the UI display
      const currentState = this.stateService.getState();
      const userProfile = currentState.userProfile;
      const modelStatus = currentState.modelStatus;
      const latestTrainedModel = currentState.latestTrainedModel;
      
      console.log('🎯 GENERATION LOGIC: Current model status:', modelStatus);
      console.log('🎯 GENERATION LOGIC: Latest trained model:', latestTrainedModel);
      
      if (modelStatus === 'Model Ready') {
        // If dashboard says Model Ready, we should generate, not train
        console.log('✅ Model Ready detected - proceeding with generation');
        
        // Get the model version from ModelCreationRequest data
        const modelVersion = latestTrainedModel?.trainedModelVersion || latestTrainedModel?.versionId;
        const modelId = latestTrainedModel?.replicateModelId || latestTrainedModel?.modelId;
        
        if (modelVersion) {
          console.log('✅ Found model version for generation:', modelVersion);
          console.log('✅ Model ID:', modelId);
          this.notificationService.info('Using Existing Model', 'Using your previously trained model for generation');
          await this.generateImagesWithStyles(selectedStyles, modelVersion);
        } else if (modelId) {
          // We have model ID but not version - use the model ID for generation
          console.log('✅ Using model ID for generation:', modelId);
          this.notificationService.info('Using Existing Model', 'Using your previously trained model for generation');
          await this.generateImagesWithStyles(selectedStyles, modelId);
        } else {
          // This shouldn't happen if modelStatus is 'Model Ready'
          console.error('❌ Model Ready but no model data available');
          this.notificationService.error('Model Error', 'Model data not found. Please refresh and try again.');
          this.isTrainingStarted = false;
          this.currentStep = 2;
          return;
        }
      } else {
        // Model not ready - proceed with training
        console.log('❌ Model not ready - proceeding with training');
        await this.startModelTraining(selectedStyles);
      }
    } catch (error) {
      console.error('Error in training workflow:', error);
      this.isTrainingStarted = false;
      this.currentStep = 2;
      this.notificationService.error('Training Error', 'Failed to start training. Please try again.');
    }
  }

  private async startModelTraining(selectedStyles: StyleOption[]) {
    try {
      this.isTraining = true;
      this.progressPercentage = 0;
      this.progressMessage = 'Preparing your images for training...';
      this.estimatedCompletion = '15-20 minutes';
      this.notificationService.info('Starting Training', 'Creating training ZIP and starting model training...');

      // Step 1: Create training ZIP from uploaded images
      this.progressPercentage = 10;
      this.progressMessage = 'Creating training package from your images...';
      const zipResult = await this.fileUploadService.createTrainingZip().toPromise();
      
      if (!zipResult?.success || !zipResult.zipCreated) {
        throw new Error(zipResult?.error?.message || 'Failed to create training ZIP');
      }

      // Step 2: Get the public URL for the latest training ZIP
      this.progressPercentage = 20;
      this.progressMessage = 'Uploading training data...';
      const latestZipResult = await this.fileUploadService.getLatestTrainingZip().toPromise();
      
      if (!latestZipResult?.success || !latestZipResult.data?.publicUrl) {
        throw new Error('Failed to get training ZIP URL');
      }

      // Step 3: Start model training with Replicate
      const userId = this.authService.getCurrentUserId();
      if (!userId) {
        console.error('Failed to get user ID. Token exists:', !!this.authService.getToken());
        console.error('Authentication status:', this.authService.isAuthenticated());
        throw new Error('User not authenticated - unable to extract user ID from token');
      }
      console.log('Starting training for user ID:', userId);

      this.progressPercentage = 30;
      this.progressMessage = 'Initializing AI model training...';
      
      const trainRequest: TrainModelRequest = {
        userId: userId,
        imageZipUrl: latestZipResult.data.publicUrl
      };

      const trainResult = await this.replicateService.trainModel(trainRequest).toPromise();
      
      if (!trainResult?.success) {
        throw new Error(trainResult?.error?.message || 'Failed to start model training');
      }

      this.trainingId = trainResult.data.id;
      this.progressPercentage = 40;
      this.progressMessage = 'AI model is learning your features...';
      this.notificationService.success('Training Started', 'Model training has begun. This will take 15-20 minutes.');
      
      // Calculate estimated completion time
      const estimatedMinutes = 18;
      const completionTime = new Date(Date.now() + estimatedMinutes * 60000);
      this.estimatedCompletion = completionTime.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      
      // Start polling for training completion
      this.startTrainingStatusPolling(selectedStyles);

    } catch (error: any) {
      console.error('Training startup error:', error);
      this.isTraining = false;
      throw new Error(error.message || 'Failed to start training');
    }
  }

  private startTrainingStatusPolling(selectedStyles: StyleOption[]) {
    let progressIncrement = 0;
    const maxTrainingProgress = 90; // Training goes up to 90%, generation takes the last 10%
    
    this.pollingInterval = this.ngZone.runOutsideAngular(() => setInterval(async () => {
      this.ngZone.run(async () => {
        try {
          if (!this.trainingId) {
            clearInterval(this.pollingInterval);
            return;
          }

          const statusResult = await this.replicateService.getTrainingStatus(this.trainingId).toPromise();
          
          if (!statusResult?.success) {
            console.error('Failed to get training status:', statusResult?.error);
            return;
          }

          const status = statusResult.data.status;
          
          // Increment progress during training (40% to 90%)
          if (status === 'processing' || status === 'starting') {
            progressIncrement += 2; // Increment by 2% every 30 seconds
            this.progressPercentage = Math.min(40 + progressIncrement, maxTrainingProgress);
            
            // Update progress messages based on percentage
            if (this.progressPercentage < 60) {
              this.progressMessage = 'AI is analyzing your facial features...';
            } else if (this.progressPercentage < 80) {
              this.progressMessage = 'Training neural network on your photos...';
            } else {
              this.progressMessage = 'Finalizing your custom AI model...';
            }
          }
          
          if (status === 'succeeded') {
            clearInterval(this.pollingInterval);
            this.progressPercentage = maxTrainingProgress;
            this.progressMessage = 'Model training complete! Starting image generation...';
            this.isTraining = false;
            this.notificationService.success('Training Complete', 'Model training finished! Starting image generation...');
            
            // Force reload dashboard data to get updated model status
            // Wait a bit for the webhook to update the database
            await new Promise(resolve => this.ngZone.runOutsideAngular(() => setTimeout(resolve, 3000)));
            await this.stateService.loadInitialDashboardData();
            
            // Wait for state to update
            await new Promise(resolve => this.ngZone.runOutsideAngular(() => setTimeout(resolve, 500)));
            
            // Start generation with the new model
            const userProfile = this.stateService.getState().userProfile;
            if (userProfile?.trainedModelVersionId) {
              await this.generateImagesWithStyles(selectedStyles, userProfile.trainedModelVersionId);
            } else {
              // If model version not found, try to extract from training result
              const versionId = statusResult.data.version;
              if (versionId) {
                await this.generateImagesWithStyles(selectedStyles, versionId);
              } else {
                this.notificationService.error('Generation Error', 'Could not find trained model version. Please refresh and try again.');
              }
            }
          } else if (status === 'failed') {
            clearInterval(this.pollingInterval);
            this.isTraining = false;
            this.progressPercentage = 0;
            this.progressMessage = '';
            this.notificationService.error('Training Failed', 'Model training failed. Please try again.');
            this.isTrainingStarted = false;
            this.currentStep = 2;
          }
        } catch (error) {
          console.error('Error polling training status:', error);
        }
      });
    }, 30000)); // Poll every 30 seconds
  }

  private async startPhotoCompletionPolling(expectedStyleCount: number) {
    const expectedPhotoCount = expectedStyleCount * this.imagesPerStyle;
    
    console.log('📊 Starting photo completion polling with precise counting');
    console.log('📊 Generation started at:', new Date(this.generationStartTime).toISOString());
    console.log('📊 Expected photos for this generation:', expectedPhotoCount);
    
    this.photoCompletionPollingInterval = this.ngZone.runOutsideAngular(() => setInterval(async () => {
      this.ngZone.run(async () => {
        try {
          // Check for new generated photos using timestamp-based counting
          this.fileUploadService.getUserImages().subscribe({
            next: (response) => {
              // Count only images created after generation started
              const newPhotos = response.images.filter(img => 
                img.isGenerated && new Date(img.createdAt).getTime() > this.generationStartTime
              ).length;
              
              console.log('📊 Photo count polling (timestamp-based):', {
                generationStartTime: new Date(this.generationStartTime).toISOString(),
                totalGeneratedImages: response.generatedImages,
                newPhotosAfterGeneration: newPhotos,
                expectedPhotoCount: expectedPhotoCount,
                recentImages: response.images
                  .filter(img => img.isGenerated && new Date(img.createdAt).getTime() > this.generationStartTime)
                  .map(img => ({ style: img.style, createdAt: img.createdAt }))
              });
              
              if (newPhotos >= expectedPhotoCount) {
                // All photos completed - use expected count to be precise
                clearInterval(this.photoCompletionPollingInterval);
                console.log('✅ Photo generation complete! Using expected count:', expectedPhotoCount);
                this.onPhotoGenerationComplete(expectedPhotoCount);
              } else if (newPhotos > 0) {
                // Some photos completed, update progress - override time-based progress
                const photoProgress = (newPhotos / expectedPhotoCount) * 15; // 15% range for photo completion
                const progress = Math.min(85 + photoProgress, 100); // 85% to 100% based on actual photos
                this.progressPercentage = progress;
                this.progressMessage = `Generated ${newPhotos} of ${expectedPhotoCount} photos...`;
                
                // Clear time-based progress since we have real progress
                if (this.timeBasedProgressInterval) {
                  clearInterval(this.timeBasedProgressInterval);
                  this.timeBasedProgressInterval = undefined;
                }
              }
            },
            error: (error) => {
              console.error('Error checking photo completion:', error);
            }
          });
        } catch (error) {
          console.error('Error polling for photo completion:', error);
        }
      });
    }, 15000)); // Poll every 15 seconds
  }

  private startTimeBasedProgress() {
    // Clear any existing time-based progress interval
    if (this.timeBasedProgressInterval) {
      clearInterval(this.timeBasedProgressInterval);
    }

    this.timeBasedProgressInterval = this.ngZone.runOutsideAngular(() => setInterval(() => {
      this.ngZone.run(() => {
        if (!this.isGenerating || this.generationStartTime === 0) {
          return;
        }

        const elapsed = Date.now() - this.generationStartTime;
        const progressRatio = Math.min(elapsed / this.expectedGenerationTime, 0.85); // Cap at 85% for time-based
        const newProgress = 15 + (progressRatio * 70); // 15% to 85% based on time
        
        this.progressPercentage = Math.round(newProgress);
        
        // Update progress message based on elapsed time
        const elapsedMinutes = Math.floor(elapsed / 60000);
        const remainingTime = Math.max(0, Math.ceil((this.expectedGenerationTime - elapsed) / 60000));
        
        if (remainingTime > 0) {
          this.progressMessage = `Creating professional photos... (~${remainingTime} min remaining)`;
        } else {
          this.progressMessage = 'Finalizing your photos...';
        }
      });
    }, 10000)); // Update every 10 seconds
  }

  private onPhotoGenerationComplete(photoCount: number) {
    // Clear time-based progress interval
    if (this.timeBasedProgressInterval) {
      clearInterval(this.timeBasedProgressInterval);
      this.timeBasedProgressInterval = undefined;
    }
    
    // Complete the generation process
    this.progressPercentage = 100;
    this.progressMessage = 'Photo generation complete!';
    this.isGenerating = false;
    
    // Store generation count for success message
    this.lastGenerationCount = photoCount;
    this.showLastGenerationMessage = true;
    
    console.log('🎉 Photo generation complete! Showing success message with count:', photoCount);
    
    this.notificationService.success('Photos Ready!', 
      `${photoCount} professional photos have been generated and are ready to view.`);
    
    // Delay refresh to avoid interfering with success message display
    this.ngZone.runOutsideAngular(() => {
      setTimeout(() => {
        this.ngZone.run(() => {
          this.stateService.refreshGeneratedPhotosCount();
        });
      }, 2000);
    });

    // Reset progress after showing completion but keep generation available
    this.ngZone.runOutsideAngular(() => {
      setTimeout(() => {
        this.ngZone.run(() => {
          this.progressPercentage = 0;
          this.progressMessage = '';
          this.isTrainingStarted = false;
          // Don't change currentStep - keep it on the generation step so user can generate more
        });
      }, 3000);
    });
    
    // Keep the success message visible (removed auto-hide)
  }

  private async generateImagesWithStyles(selectedStyles: StyleOption[], modelVersion: string) {
    try {
      const userId = this.authService.getCurrentUserId();
      if (!userId) {
        console.error('Failed to get user ID for generation. Token exists:', !!this.authService.getToken());
        console.error('Authentication status:', this.authService.isAuthenticated());
        throw new Error('User not authenticated - unable to extract user ID from token');
      }
      console.log('Starting batch generation for user ID:', userId);

      this.isGenerating = true;
      this.notificationService.info('Generating Images', `Starting batch generation for ${selectedStyles.length} style(s)...`);

      // CONSOLIDATED APPROACH: Generate images for all selected styles in a single batch request
      const generateRequest: GenerateBatchImagesRequest = {
        trainedModelVersion: modelVersion,
        userId: userId,
        styles: selectedStyles.map(style => style.name),
        userInfo: {
          gender: this.stateService.getState().userProfile?.gender,
          ethnicity: this.stateService.getState().userProfile?.ethnicity
        },
        numOutputsPerStyle: this.imagesPerStyle // Use the selected number of images per style
      };

      console.log('🎯 BATCH GENERATION: Making single API call for all styles:', generateRequest.styles);
      const generateResult = await this.replicateService.generateBatchImages(generateRequest).toPromise();
      
      if (!generateResult?.success) {
        throw new Error(generateResult?.error?.message || 'Batch generation failed');
      }

      const { successfulStyles, failedStyles, failures, creditsCost } = generateResult.data;
      
      // Report results to user
      if (successfulStyles > 0) {
        this.notificationService.success('Generation Started', 
          `Successfully started generation for ${successfulStyles} style(s). Images will appear in your gallery when ready.`);
      }
      
      if (failedStyles > 0) {
        const failedStyleNames = failures.map(f => f.style).join(', ');
        this.notificationService.warning('Partial Success', 
          `Failed to start generation for ${failedStyles} style(s): ${failedStyleNames}`);
      }

      if (successfulStyles === 0) {
        throw new Error('No styles were successfully started for generation');
      }

      // Calculate estimated time for all images to be ready (approximately 2-3 minutes per successful style)
      const estimatedMinutes = successfulStyles * 2.5;
      const estimatedCompletion = new Date(Date.now() + estimatedMinutes * 60000);
      this.estimatedCompletion = `${Math.ceil(estimatedMinutes)} minutes`;
      
      // Start with realistic progress and update based on time
      this.progressPercentage = 15;
      this.progressMessage = `Creating professional photos with your selected styles...`;
      this.generationStartTime = Date.now();
      this.expectedGenerationTime = estimatedMinutes * 60000; // in milliseconds
      
      // Start time-based progress updates
      this.startTimeBasedProgress();
      
      this.notificationService.info('Generation Progress', 
        `Generating ${successfulStyles} style(s) with ${this.imagesPerStyle} images each. Estimated completion: ${estimatedCompletion.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}. Cost: ${creditsCost} credits.`);

      // Start polling for photo completion (use successful styles count)
      await this.startPhotoCompletionPolling(successfulStyles);
      
      // Refresh dashboard state to update model status
      await this.stateService.loadInitialDashboardData();

    } catch (error: any) {
      console.error('Error in batch image generation:', error);
      this.isGenerating = false;
      this.progressPercentage = 0;
      this.progressMessage = '';
      this.notificationService.error('Generation Error', error.message || 'Failed to generate images');
    }
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
    // Since the dashboard doesn't have direct access to photo data,
    // redirect users to the gallery where they can download photos
    this.notificationService.info('Gallery Navigation', 'Redirecting to gallery to view and download your photos');
    this.router.navigate(['/gallery']);
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
        if (hasUploadedImages && this.generatedPhotosCount === 0) return 'active';
        if (this.generatedPhotosCount > 0) return 'completed';
        return 'pending';
      
      case 3:
        // Step 3 is completed when photos are generated
        if (this.generatedPhotosCount > 0) return 'completed';
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
    const MAX_FILES = 20;
    const currentFileCount = this.uploadedImageThumbnails.length + this.selectedFiles.length;
    
    // Check if adding these files would exceed the 20 file limit
    if (currentFileCount + files.length > MAX_FILES) {
      const remaining = MAX_FILES - currentFileCount;
      if (remaining <= 0) {
        this.notificationService.error('Upload Limit Reached', 
          `You have already reached the maximum of ${MAX_FILES} images. Please remove some images before adding more.`);
        return;
      } else {
        // Only take the files that fit within the limit
        files = files.slice(0, remaining);
        this.notificationService.warning('Upload Limit', 
          `You can only add ${remaining} more image(s) to reach the maximum of ${MAX_FILES}. Only the first ${remaining} image(s) will be processed.`);
      }
    }
    
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
    this.ngZone.runOutsideAngular(() => {
      setTimeout(() => {
        this.ngZone.run(() => {
          this.qualityCheckProgress = 'Quality check complete';
          this.isCheckingQuality = false;
          this.currentStep = 2;
        });
      }, 2000);
    });
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
    // Check if model already exists - no training cost needed
    if (this.modelStatus === 'Model Ready') {
      return 0; // Model already trained, no additional cost
    }
    return 15; // Training required - 15 credits
  }

  calculateGenerationCredits(): number {
    const generationCostPerImage = 5; // 5 credits per image generated
    const selectedStyleCount = this.availableStyles.filter(s => s.selected).length;
    const totalImages = selectedStyleCount * this.imagesPerStyle;
    return totalImages * generationCostPerImage;
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
      this.ngZone.runOutsideAngular(() => {
        const img = new Image();
        const url = URL.createObjectURL(file);
        
        img.onload = () => {
          URL.revokeObjectURL(url);
          this.ngZone.run(() => {
            resolve({ width: img.naturalWidth, height: img.naturalHeight });
          });
        };
        
        img.onerror = () => {
          URL.revokeObjectURL(url);
          this.ngZone.run(() => {
            reject(new Error('Failed to load image'));
          });
        };
        
        img.src = url;
      });
    });
  }

  continueInBackground() {
    // Hide the progress UI but keep the process running
    this.notificationService.info('Continuing in Background', 
      'Training and generation will continue. We\'ll email you when your photos are ready.');
    
    // Navigate to gallery or home page
    this.router.navigate(['/gallery']);
  }

}

