import { Injectable, Injector, NgZone } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { NotificationService } from './notification.service';
import { DashboardStateService } from './dashboard-state.service';
import { SubscriptionStateService } from './subscription-state.service';
import { ConfigService } from './config.service';
import { StyleOption } from '../components/dashboard/style-selector/style-selector.component';

// Lazy-loaded service types
interface FileUploadService {
  createTrainingZip(): Observable<any>;
  getLatestTrainingZip(): Observable<any>;
  invalidateUserImagesCache(): void;
}

interface ReplicateService {
  trainModel(request: TrainModelRequest): Observable<any>;
  getTrainingStatus(trainingId: string): Observable<any>;
  generateBatchImages(request: GenerateBatchImagesRequest): Observable<any>;
  getPredictionStatus(predictionId: string): Observable<any>;
}

interface TrainModelRequest {
  userId: string;
  imageZipUrl: string;
}

interface GenerateBatchImagesRequest {
  trainedModelVersion: string;
  userId: string;
  styles: string[];
  userInfo?: UserInfo;
  numOutputsPerStyle?: number;
}

interface UserInfo {
  gender?: string;
  ethnicity?: string;
  attributes?: Record<string, string>;
}

interface PredictionResult {
  style: string;
  result: {
    id: string;
  };
}

interface GenerationFailure {
  style: string;
  error: string;
}

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
  activePredictionIds: string[]; // Track specific predictions we're waiting for
}

export interface CreditCalculation {
  trainingCredits: number;
  generationCredits: number;
  totalCredits: number;
  hasEnoughCredits: boolean;
  remainingCredits: number;
}

@Injectable({
  providedIn: 'root',
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
    showLastGenerationMessage: false,
    activePredictionIds: [],
  };

  private readonly _progress = new BehaviorSubject<WorkflowProgress>(this.initialProgress);
  readonly progress$ = this._progress.asObservable();

  private pollingInterval?: any;
  private photoCompletionPollingInterval?: any;
  private timeBasedProgressInterval?: any;

  // Lazy-loaded services
  private fileUploadService: FileUploadService | null = null;
  private replicateService: ReplicateService | null = null;

  constructor(
    private authService: AuthService,
    private notificationService: NotificationService,
    private stateService: DashboardStateService,
    private subscriptionState: SubscriptionStateService,
    private config: ConfigService,
    private ngZone: NgZone,
    private injector: Injector
  ) {}

  getProgress(): WorkflowProgress {
    return this._progress.getValue();
  }

  private setProgress(update: Partial<WorkflowProgress>) {
    this._progress.next({
      ...this.getProgress(),
      ...update,
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
  calculateCredits(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    modelStatus: string
  ): CreditCalculation {
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
      remainingCredits,
    };
  }

  private calculateTrainingCredits(modelStatus: string): number {
    if (modelStatus === 'Model Ready') {
      return 0; // Model already trained, no additional cost
    }
    return 15; // Training required - 15 credits
  }

  private calculateGenerationCredits(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): number {
    const generationCostPerImage = 5; // 5 credits per image generated
    const selectedStyleCount = selectedStyles.length;
    const totalImages = selectedStyleCount * imagesPerStyle;
    return totalImages * generationCostPerImage;
  }

  private getTotalAvailableCredits(): number {
    // Primary: Check subscription service first (same source as UI)
    const subscriptionState = this.subscriptionState.getState();
    if (subscriptionState.totalCredits !== undefined && subscriptionState.totalCredits > 0) {
      return subscriptionState.totalCredits;
    }

    // Secondary: Dashboard state service
    const state = this.stateService.getState();
    if (state.totalCredits !== undefined && state.totalCredits > 0) {
      return state.totalCredits;
    }

    // Fallback: Try subscription service user credit status
    const userStatus = subscriptionState.userCreditStatus;
    if (userStatus?.totalCredits !== undefined && userStatus.totalCredits > 0) {
      return userStatus.totalCredits;
    }

    // Last resort: Manual calculation from dashboard state
    const userCreditStatus = state.userCreditStatus;
    const creditsInfo = state.creditsInfo;

    const weeklyCredits = userCreditStatus?.weeklyCredits || creditsInfo?.availableCredits || 0;
    const purchasedCredits = userCreditStatus?.purchasedCredits || 0;

    return weeklyCredits + purchasedCredits;
  }

  // Main workflow orchestration method
  async startTrainingWithStyles(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): Promise<void> {
    if (selectedStyles.length === 0) {
      this.notificationService.error('Training Error', 'Please select at least one style');
      return;
    }

    // Ensure we have the latest credit data before validation
    await this.stateService.loadInitialDashboardData();

    // Wait a moment for state to fully propagate
    await new Promise(resolve => setTimeout(resolve, 500));

    // Get current state after loading
    const currentState = this.stateService.getState();

    // Check if user has enough credits
    const creditCalc = this.calculateCredits(
      selectedStyles,
      imagesPerStyle,
      currentState.modelStatus
    );

    if (!creditCalc.hasEnoughCredits) {
      const availableCredits = this.getTotalAvailableCredits();

      if (availableCredits === 0) {
        this.notificationService.error(
          'Credits Not Loaded',
          `Unable to load current credit balance. Please refresh the page and try again.`
        );
      } else {
        this.notificationService.error(
          'Insufficient Credits',
          `You need ${creditCalc.totalCredits} credits but only have ${availableCredits}. Please purchase more credits.`
        );
      }
      return;
    }

    try {
      const modelStatus = currentState.modelStatus;
      const latestTrainedModel = currentState.latestTrainedModel;

      if (modelStatus === 'Model Ready') {
        const modelVersion =
          latestTrainedModel?.trainedModelVersion || latestTrainedModel?.versionId;
        const modelId = latestTrainedModel?.replicateModelId || latestTrainedModel?.modelId;

        if (modelVersion) {
          this.notificationService.info(
            'Using Existing Model',
            'Using your previously trained model for generation'
          );
          await this.generateImagesWithStyles(selectedStyles, imagesPerStyle, modelVersion);
        } else if (modelId) {
          this.notificationService.info(
            'Using Existing Model',
            'Using your previously trained model for generation'
          );
          await this.generateImagesWithStyles(selectedStyles, imagesPerStyle, modelId);
        } else {
          this.notificationService.error(
            'Model Error',
            'Model data not found. Please refresh and try again.'
          );
          return;
        }
      } else {
        await this.startModelTraining(selectedStyles, imagesPerStyle);
      }
    } catch (error) {
      console.error('Error in training workflow:', error);
      this.notificationService.error(
        'Training Error',
        'Failed to start training. Please try again.'
      );
    }
  }

  // Lazy loading methods
  private async loadFileUploadService(): Promise<FileUploadService> {
    if (!this.fileUploadService) {
      const { FileUploadService } = await import('./file-upload.service');
      this.fileUploadService = this.injector.get(FileUploadService);
    }
    return this.fileUploadService;
  }

  private async loadReplicateService(): Promise<ReplicateService> {
    if (!this.replicateService) {
      const { ReplicateService } = await import('./replicate.service');
      this.replicateService = this.injector.get(ReplicateService);
    }
    return this.replicateService;
  }

  // Model training workflow
  private async startModelTraining(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): Promise<void> {
    try {
      this.setProgress({
        isTraining: true,
        progressPercentage: 0,
        progressMessage: 'Preparing your images for training...',
        estimatedCompletion: '15-20 minutes',
      });

      this.notificationService.info(
        'Starting Training',
        'Creating training ZIP and starting model training...'
      );

      // Step 1: Create training ZIP from uploaded images
      this.setProgress({
        progressPercentage: 10,
        progressMessage: 'Creating training package from your images...',
      });

      const fileUploadService = await this.loadFileUploadService();
      const zipResult = await fileUploadService.createTrainingZip().toPromise();

      if (!zipResult?.success || !zipResult.zipCreated) {
        throw new Error(zipResult?.error?.message || 'Failed to create training ZIP');
      }

      // Step 2: Get the public URL for the latest training ZIP
      this.setProgress({
        progressPercentage: 20,
        progressMessage: 'Uploading training data...',
      });

      const latestZipResult = await fileUploadService.getLatestTrainingZip().toPromise();

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
        progressMessage: 'Initializing AI model training...',
      });

      const trainRequest: TrainModelRequest = {
        userId,
        imageZipUrl: latestZipResult.data.publicUrl,
      };

      const replicateService = await this.loadReplicateService();
      const trainResult = await replicateService.trainModel(trainRequest).toPromise();

      if (!trainResult?.success) {
        throw new Error(trainResult?.error?.message || 'Failed to start model training');
      }

      this.setProgress({
        trainingId: trainResult.data.id,
        progressPercentage: 40,
        progressMessage: 'AI model is learning your features...',
      });

      this.notificationService.success(
        'Training Started',
        'Model training has begun. This will take 15-20 minutes.'
      );

      // Calculate estimated completion time
      const estimatedMinutes = 18;
      const completionTime = new Date(Date.now() + estimatedMinutes * 60000);
      this.setProgress({
        estimatedCompletion: completionTime.toLocaleTimeString([], {
          hour: '2-digit',
          minute: '2-digit',
        }),
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

    this.pollingInterval = this.ngZone.runOutsideAngular(() =>
      setInterval(async () => {
        this.ngZone.run(async () => {
          try {
            const currentProgress = this.getProgress();
            if (!currentProgress.trainingId) {
              clearInterval(this.pollingInterval);
              return;
            }

            const replicateService = await this.loadReplicateService();
            const statusResult = await replicateService
              .getTrainingStatus(currentProgress.trainingId)
              .toPromise();

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
                progressMessage: message,
              });
            }

            if (status === 'succeeded') {
              clearInterval(this.pollingInterval);
              this.setProgress({
                progressPercentage: maxTrainingProgress,
                progressMessage: 'Model training complete! Starting image generation...',
                isTraining: false,
              });

              this.notificationService.success(
                'Training Complete',
                'Model training finished! Starting image generation...'
              );

              // Force reload dashboard data to get updated model status
              // Wait a bit for the webhook to update the database
              await new Promise(resolve =>
                this.ngZone.runOutsideAngular(() => setTimeout(resolve, 3000))
              );
              await this.stateService.loadInitialDashboardData();

              // Wait for state to update
              await new Promise(resolve =>
                this.ngZone.runOutsideAngular(() => setTimeout(resolve, 500))
              );

              // Start generation with the new model
              const userProfile = this.stateService.getState().userProfile;
              if (userProfile?.trainedModelVersionId) {
                await this.generateImagesWithStyles(
                  selectedStyles,
                  imagesPerStyle,
                  userProfile.trainedModelVersionId
                );
              } else {
                // If model version not found, try to extract from training result
                const versionId = statusResult.data.version;
                if (versionId) {
                  await this.generateImagesWithStyles(selectedStyles, imagesPerStyle, versionId);
                } else {
                  this.notificationService.error(
                    'Generation Error',
                    'Could not find trained model version. Please refresh and try again.'
                  );
                }
              }
            } else if (status === 'failed') {
              clearInterval(this.pollingInterval);
              this.setProgress({
                isTraining: false,
                progressPercentage: 0,
                progressMessage: '',
              });
              this.notificationService.error(
                'Training Failed',
                'Model training failed. Please try again.'
              );
            }
          } catch (error) {
            console.error('Error polling training status:', error);
          }
        });
      }, 30000)
    ); // Poll every 30 seconds
  }

  // Image generation workflow
  private async generateImagesWithStyles(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    modelVersion: string
  ): Promise<void> {
    try {
      const userId = this.authService.getCurrentUserId();
      if (!userId) {
        throw new Error('User not authenticated - unable to extract user ID from token');
      }

      // Clear previous generation state and caches
      this.clearAllIntervals();

      this.setProgress({
        isGenerating: true,
        lastGenerationCount: 0,
        showLastGenerationMessage: false,
        generationStartTime: 0, // Will be set after successful API call
        activePredictionIds: [], // Clear any previous prediction tracking
      });

      // Invalidate image cache to ensure fresh data
      const fileUploadService = await this.loadFileUploadService();
      fileUploadService.invalidateUserImagesCache();

      this.notificationService.info(
        'Generating Images',
        `Starting batch generation for ${selectedStyles.length} style(s)...`
      );

      // CONSOLIDATED APPROACH: Generate images for all selected styles in a single batch request
      const generateRequest: GenerateBatchImagesRequest = {
        trainedModelVersion: modelVersion,
        userId,
        styles: selectedStyles.map(style => style.name),
        userInfo: {
          gender: this.stateService.getState().userProfile?.gender,
          ethnicity: this.stateService.getState().userProfile?.ethnicity,
        },
        numOutputsPerStyle: imagesPerStyle, // Use the selected number of images per style
      };

      // Store precise timestamp BEFORE API call to capture all generated images
      const preciseGenerationStartTime = Date.now();

      const replicateService = await this.loadReplicateService();
      const generateResult = await replicateService
        .generateBatchImages(generateRequest)
        .toPromise();

      if (!generateResult?.success) {
        throw new Error(generateResult?.error?.message || 'Batch generation failed');
      }

      const { successfulStyles, failedStyles, failures, creditsCost, predictions } =
        generateResult.data;

      // Extract prediction IDs for tracking
      const predictionIds = (predictions as PredictionResult[]).map(p => p.result.id);

      // Store prediction IDs for polling
      this.setProgress({
        activePredictionIds: predictionIds,
        generationStartTime: preciseGenerationStartTime,
      });

      // Report results to user
      if (successfulStyles > 0) {
        this.notificationService.success(
          'Generation Started',
          `Successfully started generation for ${successfulStyles} style(s). Images will appear in your gallery when ready.`
        );
      }

      if (failedStyles > 0) {
        const failedStyleNames = (failures as GenerationFailure[]).map(f => f.style).join(', ');
        this.notificationService.warning(
          'Partial Success',
          `Failed to start generation for ${failedStyles} style(s): ${failedStyleNames}`
        );
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
        generationStartTime: preciseGenerationStartTime,
        expectedGenerationTime: estimatedMinutes * 60000,
        estimatedCompletion: `${Math.ceil(estimatedMinutes)} minutes`,
      });

      // Start time-based progress updates
      this.startTimeBasedProgress();

      this.notificationService.info(
        'Generation Progress',
        `Generating ${successfulStyles} style(s) with ${imagesPerStyle} images each. Estimated completion: ${estimatedCompletion.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}. Cost: ${creditsCost} credits.`
      );

      // Start polling for prediction completion
      await this.startPredictionCompletionPolling();

      // Refresh dashboard state to update model status
      await this.stateService.loadInitialDashboardData();
    } catch (error: any) {
      console.error('Error in batch image generation:', error);
      this.setProgress({
        isGenerating: false,
        progressPercentage: 0,
        progressMessage: '',
      });
      this.notificationService.error(
        'Generation Error',
        error.message || 'Failed to generate images'
      );
    }
  }

  // Prediction completion polling - tracks specific prediction IDs
  private async startPredictionCompletionPolling(): Promise<void> {
    const currentProgress = this.getProgress();
    const predictionIds = currentProgress.activePredictionIds;

    if (!predictionIds || predictionIds.length === 0) {
      return;
    }

    // Clear any existing polling interval to prevent duplicates
    if (this.photoCompletionPollingInterval) {
      clearInterval(this.photoCompletionPollingInterval);
    }

    this.photoCompletionPollingInterval = this.ngZone.runOutsideAngular(() =>
      setInterval(async () => {
        this.ngZone.run(async () => {
          try {
            const currentProgress = this.getProgress();

            // Only poll if we're actively generating
            if (!currentProgress.isGenerating || !currentProgress.activePredictionIds.length) {
              return;
            }

            // Check status of each prediction
            const predictionStatuses = await Promise.all(
              currentProgress.activePredictionIds.map(async predictionId => {
                try {
                  const replicateService = await this.loadReplicateService();
                  const result = await replicateService
                    .getPredictionStatus(predictionId)
                    .toPromise();
                  return {
                    id: predictionId,
                    status: result?.success ? result.data.status : 'unknown',
                    data: result?.data,
                  };
                } catch (error) {
                  return {
                    id: predictionId,
                    status: 'error',
                    data: null,
                  };
                }
              })
            );

            // Count completed predictions
            const completedPredictions = predictionStatuses.filter(p => p.status === 'succeeded');
            const failedPredictions = predictionStatuses.filter(
              p => p.status === 'failed' || p.status === 'error'
            );
            const totalPredictions = currentProgress.activePredictionIds.length;

            // Update progress based on completion ratio
            if (completedPredictions.length > 0 || failedPredictions.length > 0) {
              const completionRatio = completedPredictions.length / totalPredictions;
              const progress = Math.round(85 + completionRatio * 15); // 85% to 100%

              this.setProgress({
                progressPercentage: progress,
                progressMessage: `Processing ${completedPredictions.length} of ${totalPredictions} generations...`,
              });

              // Clear time-based progress since we have real progress
              if (this.timeBasedProgressInterval) {
                clearInterval(this.timeBasedProgressInterval);
                this.timeBasedProgressInterval = undefined;
              }
            }

            // Check if all predictions are completed (successfully)
            if (completedPredictions.length === totalPredictions) {
              // Stop polling and generating
              clearInterval(this.photoCompletionPollingInterval);
              this.setProgress({ isGenerating: false });
              this.onPhotoGenerationComplete(totalPredictions);
            } else if (
              failedPredictions.length > 0 &&
              completedPredictions.length + failedPredictions.length === totalPredictions
            ) {
              // All predictions finished but some failed
              clearInterval(this.photoCompletionPollingInterval);
              this.setProgress({ isGenerating: false });

              if (completedPredictions.length > 0) {
                // Some succeeded
                this.onPhotoGenerationComplete(completedPredictions.length);
                this.notificationService.warning(
                  'Partial Success',
                  `${completedPredictions.length} photos generated successfully, ${failedPredictions.length} failed.`
                );
              } else {
                // All failed
                this.notificationService.error(
                  'Generation Failed',
                  'All photo generations failed. Please try again.'
                );
              }
            }
          } catch (error) {
            console.error('Error polling for prediction completion:', error);
          }
        });
      }, 5000)
    ); // Poll every 5 seconds for better responsiveness
  }

  // Time-based progress tracking
  private startTimeBasedProgress(): void {
    // Clear any existing time-based progress interval
    if (this.timeBasedProgressInterval) {
      clearInterval(this.timeBasedProgressInterval);
    }

    this.timeBasedProgressInterval = this.ngZone.runOutsideAngular(() =>
      setInterval(() => {
        this.ngZone.run(() => {
          const currentProgress = this.getProgress();

          if (!currentProgress.isGenerating || currentProgress.generationStartTime === 0) {
            return;
          }

          const elapsed = Date.now() - currentProgress.generationStartTime;
          const progressRatio = Math.min(elapsed / currentProgress.expectedGenerationTime, 0.85); // Cap at 85% for time-based
          const newProgress = 15 + progressRatio * 70; // 15% to 85% based on time

          // Update progress message based on elapsed time
          const elapsedMinutes = Math.floor(elapsed / 60000);
          const remainingTime = Math.max(
            0,
            Math.ceil((currentProgress.expectedGenerationTime - elapsed) / 60000)
          );

          let message = `Creating professional photos... (~${remainingTime} min remaining)`;
          if (remainingTime <= 0) {
            message = 'Finalizing your photos...';
          }

          this.setProgress({
            progressPercentage: Math.round(newProgress),
            progressMessage: message,
          });
        });
      }, 10000)
    ); // Update every 10 seconds
  }

  // Photo generation completion
  private onPhotoGenerationComplete(photoCount: number): void {
    // Clear all intervals
    if (this.timeBasedProgressInterval) {
      clearInterval(this.timeBasedProgressInterval);
      this.timeBasedProgressInterval = undefined;
    }

    // Invalidate cache BEFORE showing celebration to ensure gallery has fresh data
    if (this.fileUploadService) {
      this.fileUploadService.invalidateUserImagesCache();
    }

    // Complete the generation process
    this.setProgress({
      progressPercentage: 100,
      progressMessage: 'All photos ready!',
      isGenerating: false,
      lastGenerationCount: photoCount,
      showLastGenerationMessage: true,
    });

    this.notificationService.success(
      'Photos Ready!',
      `${photoCount} professional photos are ready to view in your gallery.`
    );

    // Refresh photo count and dashboard state
    this.ngZone.runOutsideAngular(() => {
      setTimeout(() => {
        this.ngZone.run(() => {
          this.stateService.refreshGeneratedPhotosCount();
          this.stateService.invalidateAndRefreshImages();
        });
      }, 500);
    });

    // Reset progress after showing completion
    this.ngZone.runOutsideAngular(() => {
      setTimeout(() => {
        this.ngZone.run(() => {
          this.setProgress({
            progressPercentage: 0,
            progressMessage: '',
          });
        });
      }, 3000);
    });
  }

  // Utility methods
  dismissSuccessMessage(): void {
    this.setProgress({
      showLastGenerationMessage: false,
      lastGenerationCount: 0,
    });
  }

  // Cleanup method
  dispose(): void {
    this.clearAllIntervals();
    this.resetProgress();
  }
}
