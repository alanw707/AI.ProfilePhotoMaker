import { Injectable, NgZone } from '@angular/core';
import { Observable, BehaviorSubject } from 'rxjs';
import { AuthService } from './auth.service';
import { FileUploadService } from './file-upload.service';
import { ReplicateService, TrainModelRequest, GenerateBatchImagesRequest } from './replicate.service';
import { NotificationService } from './notification.service';
import { DashboardStateService } from './dashboard-state.service';
import { ConfigService } from './config.service';
import { StyleOption } from '../components/dashboard/style-selector/style-selector.component';

export interface WorkflowProgress {
  isTraining: boolean;
  isGenerating: boolean;
  progressPercentage: number;
  progressMessage: string;
  estimatedCompletion: string;
  trainingId: string;
  generationStartTime: number;
  expectedGenerationTime: number;
  lastGenerationCount: number;
  showLastGenerationMessage: boolean;
}

export interface CreditCalculation {
  trainingCredits: number;
  generationCredits: number;
  totalCredits: number;
  hasEnoughCredits: boolean;
  remainingCredits: number;
}

@Injectable({
  providedIn: 'root'
})
export class WorkflowOrchestrationService {
  private readonly initialProgress: WorkflowProgress = {
    isTraining: false,
    isGenerating: false,
    progressPercentage: 0,
    progressMessage: '',
    estimatedCompletion: '',
    trainingId: '',
    generationStartTime: 0,
    expectedGenerationTime: 0,
    lastGenerationCount: 0,
    showLastGenerationMessage: false
  };

  private readonly _progress = new BehaviorSubject<WorkflowProgress>(this.initialProgress);
  readonly progress$ = this._progress.asObservable();

  private pollingInterval?: any;
  private photoCompletionPollingInterval?: any;
  private timeBasedProgressInterval?: any;

  constructor(
    private authService: AuthService,
    private fileUploadService: FileUploadService,
    private replicateService: ReplicateService,
    private notificationService: NotificationService,
    private stateService: DashboardStateService,
    private config: ConfigService,
    private ngZone: NgZone
  ) {}

  getProgress(): WorkflowProgress {
    return this._progress.getValue();
  }

  private setProgress(update: Partial<WorkflowProgress>) {
    this._progress.next({
      ...this.getProgress(),
      ...update
    });
  }

  resetProgress() {
    this._progress.next(this.initialProgress);
    this.clearAllIntervals();
  }

  private clearAllIntervals() {
    if (this.pollingInterval) {
      clearInterval(this.pollingInterval);
      this.pollingInterval = undefined;
    }
    if (this.photoCompletionPollingInterval) {
      clearInterval(this.photoCompletionPollingInterval);
      this.photoCompletionPollingInterval = undefined;
    }
    if (this.timeBasedProgressInterval) {
      clearInterval(this.timeBasedProgressInterval);
      this.timeBasedProgressInterval = undefined;
    }
  }

  // Credit calculation methods
  calculateCredits(selectedStyles: StyleOption[], imagesPerStyle: number, modelStatus: string): CreditCalculation {
    const trainingCredits = this.calculateTrainingCredits(modelStatus);
    const generationCredits = this.calculateGenerationCredits(selectedStyles, imagesPerStyle);
    const totalCredits = trainingCredits + generationCredits;
    
    const availableCredits = this.getTotalAvailableCredits();
    const hasEnoughCredits = availableCredits >= totalCredits;
    const remainingCredits = availableCredits - totalCredits;

    return {
      trainingCredits,
      generationCredits,
      totalCredits,
      hasEnoughCredits,
      remainingCredits
    };
  }

  private calculateTrainingCredits(modelStatus: string): number {
    // Check if model already exists - no training cost needed
    if (modelStatus === 'Model Ready') {
      return 0; // Model already trained, no additional cost
    }
    return 15; // Training required - 15 credits
  }

  private calculateGenerationCredits(selectedStyles: StyleOption[], imagesPerStyle: number): number {
    const generationCostPerImage = 5; // 5 credits per image generated
    const selectedStyleCount = selectedStyles.length;
    const totalImages = selectedStyleCount * imagesPerStyle;
    return totalImages * generationCostPerImage;
  }

  private getTotalAvailableCredits(): number {
    const userCreditStatus = this.stateService.getState().userCreditStatus;
    const creditsInfo = this.stateService.getState().creditsInfo;
    
    const weeklyCredits = userCreditStatus?.weeklyCredits || creditsInfo?.availableCredits || 0;
    const purchasedCredits = userCreditStatus?.purchasedCredits || 0;
    
    return weeklyCredits + purchasedCredits;
  }

  // Main workflow orchestration method
  async startTrainingWithStyles(selectedStyles: StyleOption[], imagesPerStyle: number): Promise<void> {
    if (selectedStyles.length === 0) {
      this.notificationService.error('Training Error', 'Please select at least one style');
      return;
    }

    // Check if user has enough credits
    const creditCalc = this.calculateCredits(selectedStyles, imagesPerStyle, this.stateService.getState().modelStatus);
    if (!creditCalc.hasEnoughCredits) {
      this.notificationService.error('Insufficient Credits', 
        `You need ${creditCalc.totalCredits} credits but only have ${creditCalc.totalCredits - creditCalc.remainingCredits}. Please purchase more credits.`);
      return;
    }

    try {
      // UNIFIED LOGIC: Use the same model status that determines the UI display
      const currentState = this.stateService.getState();
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
          await this.generateImagesWithStyles(selectedStyles, imagesPerStyle, modelVersion);
        } else if (modelId) {
          // We have model ID but not version - use the model ID for generation
          console.log('✅ Using model ID for generation:', modelId);
          this.notificationService.info('Using Existing Model', 'Using your previously trained model for generation');
          await this.generateImagesWithStyles(selectedStyles, imagesPerStyle, modelId);
        } else {
          // This shouldn't happen if modelStatus is 'Model Ready'
          console.error('❌ Model Ready but no model data available');
          this.notificationService.error('Model Error', 'Model data not found. Please refresh and try again.');
          return;
        }
      } else {
        // Model not ready - proceed with training
        console.log('❌ Model not ready - proceeding with training');
        await this.startModelTraining(selectedStyles, imagesPerStyle);
      }
    } catch (error) {
      console.error('Error in training workflow:', error);
      this.notificationService.error('Training Error', 'Failed to start training. Please try again.');
    }
  }

  // Model training workflow
  private async startModelTraining(selectedStyles: StyleOption[], imagesPerStyle: number): Promise<void> {
    try {
      this.setProgress({
        isTraining: true,
        progressPercentage: 0,
        progressMessage: 'Preparing your images for training...',
        estimatedCompletion: '15-20 minutes'
      });

      this.notificationService.info('Starting Training', 'Creating training ZIP and starting model training...');

      // Step 1: Create training ZIP from uploaded images
      this.setProgress({
        progressPercentage: 10,
        progressMessage: 'Creating training package from your images...'
      });

      const zipResult = await this.fileUploadService.createTrainingZip().toPromise();
      
      if (!zipResult?.success || !zipResult.zipCreated) {
        throw new Error(zipResult?.error?.message || 'Failed to create training ZIP');
      }

      // Step 2: Get the public URL for the latest training ZIP
      this.setProgress({
        progressPercentage: 20,
        progressMessage: 'Uploading training data...'
      });

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

      this.setProgress({
        progressPercentage: 30,
        progressMessage: 'Initializing AI model training...'
      });
      
      const trainRequest: TrainModelRequest = {
        userId: userId,
        imageZipUrl: latestZipResult.data.publicUrl
      };

      const trainResult = await this.replicateService.trainModel(trainRequest).toPromise();
      
      if (!trainResult?.success) {
        throw new Error(trainResult?.error?.message || 'Failed to start model training');
      }

      this.setProgress({
        trainingId: trainResult.data.id,
        progressPercentage: 40,
        progressMessage: 'AI model is learning your features...'
      });

      this.notificationService.success('Training Started', 'Model training has begun. This will take 15-20 minutes.');
      
      // Calculate estimated completion time
      const estimatedMinutes = 18;
      const completionTime = new Date(Date.now() + estimatedMinutes * 60000);
      this.setProgress({
        estimatedCompletion: completionTime.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      });
      
      // Start polling for training completion
      this.startTrainingStatusPolling(selectedStyles, imagesPerStyle);

    } catch (error: any) {
      console.error('Training startup error:', error);
      this.setProgress({ isTraining: false });
      throw new Error(error.message || 'Failed to start training');
    }
  }

  // Training status polling
  private startTrainingStatusPolling(selectedStyles: StyleOption[], imagesPerStyle: number): void {
    let progressIncrement = 0;
    const maxTrainingProgress = 90; // Training goes up to 90%, generation takes the last 10%
    
    this.pollingInterval = this.ngZone.runOutsideAngular(() => setInterval(async () => {
      this.ngZone.run(async () => {
        try {
          const currentProgress = this.getProgress();
          if (!currentProgress.trainingId) {
            clearInterval(this.pollingInterval);
            return;
          }

          const statusResult = await this.replicateService.getTrainingStatus(currentProgress.trainingId).toPromise();
          
          if (!statusResult?.success) {
            console.error('Failed to get training status:', statusResult?.error);
            return;
          }

          const status = statusResult.data.status;
          
          // Increment progress during training (40% to 90%)
          if (status === 'processing' || status === 'starting') {
            progressIncrement += 2; // Increment by 2% every 30 seconds
            const newProgress = Math.min(40 + progressIncrement, maxTrainingProgress);
            
            let message = 'AI is analyzing your facial features...';
            if (newProgress >= 60 && newProgress < 80) {
              message = 'Training neural network on your photos...';
            } else if (newProgress >= 80) {
              message = 'Finalizing your custom AI model...';
            }
            
            this.setProgress({
              progressPercentage: newProgress,
              progressMessage: message
            });
          }
          
          if (status === 'succeeded') {
            clearInterval(this.pollingInterval);
            this.setProgress({
              progressPercentage: maxTrainingProgress,
              progressMessage: 'Model training complete! Starting image generation...',
              isTraining: false
            });

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
              await this.generateImagesWithStyles(selectedStyles, imagesPerStyle, userProfile.trainedModelVersionId);
            } else {
              // If model version not found, try to extract from training result
              const versionId = statusResult.data.version;
              if (versionId) {
                await this.generateImagesWithStyles(selectedStyles, imagesPerStyle, versionId);
              } else {
                this.notificationService.error('Generation Error', 'Could not find trained model version. Please refresh and try again.');
              }
            }
          } else if (status === 'failed') {
            clearInterval(this.pollingInterval);
            this.setProgress({
              isTraining: false,
              progressPercentage: 0,
              progressMessage: ''
            });
            this.notificationService.error('Training Failed', 'Model training failed. Please try again.');
          }
        } catch (error) {
          console.error('Error polling training status:', error);
        }
      });
    }, 30000)); // Poll every 30 seconds
  }

  // Image generation workflow
  private async generateImagesWithStyles(selectedStyles: StyleOption[], imagesPerStyle: number, modelVersion: string): Promise<void> {
    try {
      const userId = this.authService.getCurrentUserId();
      if (!userId) {
        console.error('Failed to get user ID for generation. Token exists:', !!this.authService.getToken());
        console.error('Authentication status:', this.authService.isAuthenticated());
        throw new Error('User not authenticated - unable to extract user ID from token');
      }
      console.log('Starting batch generation for user ID:', userId);

      this.setProgress({ isGenerating: true });
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
        numOutputsPerStyle: imagesPerStyle // Use the selected number of images per style
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
      
      // Start with realistic progress and update based on time
      this.setProgress({
        progressPercentage: 15,
        progressMessage: `Creating professional photos with your selected styles...`,
        generationStartTime: Date.now(),
        expectedGenerationTime: estimatedMinutes * 60000,
        estimatedCompletion: `${Math.ceil(estimatedMinutes)} minutes`
      });
      
      // Start time-based progress updates
      this.startTimeBasedProgress();
      
      this.notificationService.info('Generation Progress', 
        `Generating ${successfulStyles} style(s) with ${imagesPerStyle} images each. Estimated completion: ${estimatedCompletion.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}. Cost: ${creditsCost} credits.`);

      // Start polling for photo completion (use successful styles count)
      await this.startPhotoCompletionPolling(successfulStyles, imagesPerStyle);
      
      // Refresh dashboard state to update model status
      await this.stateService.loadInitialDashboardData();

    } catch (error: any) {
      console.error('Error in batch image generation:', error);
      this.setProgress({
        isGenerating: false,
        progressPercentage: 0,
        progressMessage: ''
      });
      this.notificationService.error('Generation Error', error.message || 'Failed to generate images');
    }
  }

  // Photo completion polling
  private async startPhotoCompletionPolling(expectedStyleCount: number, imagesPerStyle: number): Promise<void> {
    const expectedPhotoCount = expectedStyleCount * imagesPerStyle;
    
    console.log('📊 Starting photo completion polling with precise counting');
    console.log('📊 Generation started at:', new Date(this.getProgress().generationStartTime).toISOString());
    console.log('📊 Expected photos for this generation:', expectedPhotoCount);
    
    this.photoCompletionPollingInterval = this.ngZone.runOutsideAngular(() => setInterval(async () => {
      this.ngZone.run(async () => {
        try {
          const currentProgress = this.getProgress();
          
          // Check for new generated photos using timestamp-based counting
          this.fileUploadService.getUserImages().subscribe({
            next: (response) => {
              // Count only images created after generation started
              const newPhotos = response.images.filter(img => 
                img.isGenerated && new Date(img.createdAt).getTime() > currentProgress.generationStartTime
              ).length;
              
              console.log('📊 Photo count polling (timestamp-based):', {
                generationStartTime: new Date(currentProgress.generationStartTime).toISOString(),
                totalGeneratedImages: response.generatedImages,
                newPhotosAfterGeneration: newPhotos,
                expectedPhotoCount: expectedPhotoCount,
                recentImages: response.images
                  .filter(img => img.isGenerated && new Date(img.createdAt).getTime() > currentProgress.generationStartTime)
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
                
                this.setProgress({
                  progressPercentage: progress,
                  progressMessage: `Generated ${newPhotos} of ${expectedPhotoCount} photos...`
                });
                
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

  // Time-based progress tracking
  private startTimeBasedProgress(): void {
    // Clear any existing time-based progress interval
    if (this.timeBasedProgressInterval) {
      clearInterval(this.timeBasedProgressInterval);
    }

    this.timeBasedProgressInterval = this.ngZone.runOutsideAngular(() => setInterval(() => {
      this.ngZone.run(() => {
        const currentProgress = this.getProgress();
        
        if (!currentProgress.isGenerating || currentProgress.generationStartTime === 0) {
          return;
        }

        const elapsed = Date.now() - currentProgress.generationStartTime;
        const progressRatio = Math.min(elapsed / currentProgress.expectedGenerationTime, 0.85); // Cap at 85% for time-based
        const newProgress = 15 + (progressRatio * 70); // 15% to 85% based on time
        
        // Update progress message based on elapsed time
        const elapsedMinutes = Math.floor(elapsed / 60000);
        const remainingTime = Math.max(0, Math.ceil((currentProgress.expectedGenerationTime - elapsed) / 60000));
        
        let message = `Creating professional photos... (~${remainingTime} min remaining)`;
        if (remainingTime <= 0) {
          message = 'Finalizing your photos...';
        }
        
        this.setProgress({
          progressPercentage: Math.round(newProgress),
          progressMessage: message
        });
      });
    }, 10000)); // Update every 10 seconds
  }

  // Photo generation completion
  private onPhotoGenerationComplete(photoCount: number): void {
    // Clear time-based progress interval
    if (this.timeBasedProgressInterval) {
      clearInterval(this.timeBasedProgressInterval);
      this.timeBasedProgressInterval = undefined;
    }
    
    // Complete the generation process
    this.setProgress({
      progressPercentage: 100,
      progressMessage: 'Photo generation complete!',
      isGenerating: false,
      lastGenerationCount: photoCount,
      showLastGenerationMessage: true
    });
    
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
          this.setProgress({
            progressPercentage: 0,
            progressMessage: ''
          });
        });
      }, 3000);
    });
  }

  // Utility methods
  dismissSuccessMessage(): void {
    this.setProgress({
      showLastGenerationMessage: false,
      lastGenerationCount: 0
    });
  }

  // Cleanup method
  dispose(): void {
    this.clearAllIntervals();
    this.resetProgress();
  }
}