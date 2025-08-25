import { Injectable, inject, Injector, NgZone } from '@angular/core';
import { BehaviorSubject, Observable, firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { NotificationService } from './notification.service';
import { DashboardStateService } from './dashboard-state.service';
import { SubscriptionStateService } from './subscription-state.service';
import { ConfigService } from './config.service';
import { StyleOption } from '../components/dashboard/style-selector/style-selector.component';

// Lazy-loaded service types
interface TrainingZipResponse {
  success: boolean;
  zipCreated: boolean;
  zipPath: string;
  message: string;
  error?: any;
}

// Used for polling training status
interface TrainingStatusResponse {
  success: boolean;
  data: {
    id: string;
    status: string;
    created_at: string;
    completed_at?: string;
    version?: string;
    error?: string;
    logs?: string;
  };
  error?: any;
}

// Used for starting training (includes credits info and wraps prediction)
interface TrainingStartApiResponse {
  success: boolean;
  data: {
    prediction: {
      id: string;
      status: string;
      created_at: string;
      completed_at?: string;
      version?: string;
      error?: string;
      logs?: string;
    };
    creditsRemaining: number;
    creditsCost: number;
  } | null;
  error?: { code?: string; message?: string } | any;
}

interface BatchGenerationResponse {
  success: boolean;
  data: {
    predictions: { style: string; result: any }[];
    creditsRemaining: number;
    creditsCost: number;
    successfulStyles: number;
    failedStyles: number;
    failures: { style: string; error: string }[];
  };
  error?: any;
}

interface PredictionStatusResponse {
  success: boolean;
  data: {
    id: string;
    status: string;
    created_at: string;
    completed_at?: string;
    output?: string[];
    error?: string;
    logs?: string;
    dataUrl?: string;
  };
  error?: any;
}

interface FileUploadService {
  createTrainingZip(): Observable<TrainingZipResponse>;
  getLatestTrainingZip(): Observable<{
    success: boolean;
    data: { fileName: string; publicUrl: string; createdAt: string; sizeBytes: number };
    error?: any;
  }>;
  listTrainingFiles(): Observable<{ success: boolean; data: string[]; error: any }>;
  deleteAllTrainingFiles(): Observable<{ success: boolean; message: string }>;
  invalidateUserImagesCache(): void;
}

interface ReplicateService {
  trainModel(request: TrainModelRequest): Observable<TrainingStartApiResponse>;
  getTrainingStatus(trainingId: string): Observable<TrainingStatusResponse>;
  generateBatchImages(request: GenerateBatchImagesRequest): Observable<BatchGenerationResponse>;
  getPredictionStatus(predictionId: string): Observable<PredictionStatusResponse>;
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

interface WorkflowOrchestrationDependencies {
  authService: AuthService;
  notificationService: NotificationService;
  stateService: DashboardStateService;
  subscriptionState: SubscriptionStateService;
  config: ConfigService;
}

@Injectable({
  providedIn: 'root',
})
export class WorkflowOrchestrationService {
  private readonly _initialProgress: WorkflowProgress = {
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

  private readonly _progress = new BehaviorSubject<WorkflowProgress>(this._initialProgress);
  readonly progress$ = this._progress.asObservable();

  private _pollingInterval?: NodeJS.Timeout;
  private _photoCompletionPollingInterval?: NodeJS.Timeout;
  private _timeBasedProgressInterval?: NodeJS.Timeout;

  // Lazy-loaded services
  private _fileUploadService: FileUploadService | null = null;
  private _replicateService: ReplicateService | null = null;

  // Core dependencies
  private readonly _deps: WorkflowOrchestrationDependencies;

  // Use inject pattern to reduce constructor parameters
  private readonly _ngZone = inject(NgZone);
  private readonly _injector = inject(Injector);

  constructor(
    authService: AuthService,
    notificationService: NotificationService,
    stateService: DashboardStateService,
    subscriptionState: SubscriptionStateService,
    config: ConfigService
  ) {
    this._deps = {
      authService,
      notificationService,
      stateService,
      subscriptionState,
      config,
    };
  }

  getProgress(): WorkflowProgress {
    return this._progress.getValue();
  }

  private _setProgress(update: Partial<WorkflowProgress>): void {
    this._progress.next({
      ...this.getProgress(),
      ...update,
    });
  }

  resetProgress(): void {
    this._progress.next(this._initialProgress);
    this._clearAllIntervals();
  }

  private _clearAllIntervals(): void {
    if (this._pollingInterval) {
      clearInterval(this._pollingInterval);
      this._pollingInterval = undefined;
    }
    if (this._photoCompletionPollingInterval) {
      clearInterval(this._photoCompletionPollingInterval);
      this._photoCompletionPollingInterval = undefined;
    }
    if (this._timeBasedProgressInterval) {
      clearInterval(this._timeBasedProgressInterval);
      this._timeBasedProgressInterval = undefined;
    }
  }

  // Credit calculation methods
  calculateCredits(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    modelStatus: string
  ): CreditCalculation {
    const trainingCredits = this._calculateTrainingCredits(modelStatus);
    const generationCredits = this._calculateGenerationCredits(selectedStyles, imagesPerStyle);
    const totalCredits = trainingCredits + generationCredits;

    const availableCredits = this._getTotalAvailableCredits();
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

  private _calculateTrainingCredits(modelStatus: string): number {
    if (modelStatus === 'Model Ready') {
      return 0; // Model already trained, no additional cost
    }
    return 15; // Training required - 15 credits
  }

  private _calculateGenerationCredits(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): number {
    const generationCostPerImage = 5; // 5 credits per image generated
    const selectedStyleCount = selectedStyles.length;
    const totalImages = selectedStyleCount * imagesPerStyle;
    return totalImages * generationCostPerImage;
  }

  private _getTotalAvailableCredits(): number {
    // Primary: Check subscription service first (same source as UI)
    const subscriptionState = this._deps.subscriptionState.getState();
    if (subscriptionState.totalCredits !== undefined && subscriptionState.totalCredits > 0) {
      return subscriptionState.totalCredits;
    }

    // Secondary: Dashboard state service
    const state = this._deps.stateService.getState();
    if (state.totalCredits !== undefined && state.totalCredits > 0) {
      return state.totalCredits;
    }

    // Fallback: Try subscription service user credit status
    const userStatus = subscriptionState.userCreditStatus;
    if (userStatus?.totalCredits !== undefined && userStatus.totalCredits > 0) {
      return userStatus.totalCredits;
    }

    // Last resort: Manual calculation from dashboard state
    return this._calculateCreditsFromState(state);
  }

  private _calculateCreditsFromState(state: any): number {
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
      this._deps.notificationService.error('Training Error', 'Please select at least one style');
      return;
    }

    // Ensure we have the latest credit data before validation
    await this._deps.stateService.loadInitialDashboardData();

    // Wait a moment for state to fully propagate
    await new Promise(resolve => setTimeout(resolve, 500));

    // Get current state after loading
    const currentState = this._deps.stateService.getState();

    // Check if user has enough credits
    const creditCalc = this.calculateCredits(
      selectedStyles,
      imagesPerStyle,
      currentState.modelStatus
    );

    if (!creditCalc.hasEnoughCredits) {
      const availableCredits = this._getTotalAvailableCredits();

      if (availableCredits === 0) {
        this._deps.notificationService.error(
          'Credits Not Loaded',
          `Unable to load current credit balance. Please refresh the page and try again.`
        );
      } else {
        this._deps.notificationService.error(
          'Insufficient Credits',
          `You need ${creditCalc.totalCredits} credits but only have ${availableCredits}. Please purchase more credits.`
        );
      }
      return;
    }

    try {
      // CRITICAL FIX: Ensure data is fully loaded before making routing decisions
      if (
        currentState.modelStatus === 'Loading...' ||
        currentState.modelStatus === '' ||
        !currentState.modelStatus
      ) {
        console.log('⏳ WAITING FOR DATA: Model status not loaded yet, refreshing...');
        await this._deps.stateService.loadInitialDashboardData();
        await new Promise(resolve => setTimeout(resolve, 1000)); // Allow time for state updates

        // Get refreshed state
        const refreshedState = this._deps.stateService.getState();
        console.log('🔄 REFRESHED STATE:', {
          modelStatus: refreshedState.modelStatus,
          hasLatestModel: !!refreshedState.latestTrainedModel,
        });

        // Use refreshed state
        Object.assign(currentState, refreshedState);
      }

      const modelStatus = currentState.modelStatus;
      const latestTrainedModel = currentState.latestTrainedModel;

      // CRITICAL FIX: Determine if user has ANY trained model, regardless of status
      const hasTrainedModel = this._checkForExistingTrainedModel(latestTrainedModel, modelStatus);

      console.log('🚨 WORKFLOW ROUTING DECISION:', {
        modelStatus,
        hasTrainedModel,
        latestTrainedModel: !!latestTrainedModel,
        modelVersion: latestTrainedModel?.trainedModelVersion || latestTrainedModel?.versionId,
        modelId: latestTrainedModel?.replicateModelId || latestTrainedModel?.modelId,
      });

      if (hasTrainedModel) {
        // User has a trained model - always use generation, never training
        console.log('🎯 ROUTING TO GENERATION: User has trained model');
        await this._handleExistingModelGeneration(
          selectedStyles,
          imagesPerStyle,
          latestTrainedModel
        );
      } else {
        // User needs a new model - start training process
        console.log('🔄 ROUTING TO TRAINING: User needs new model');
        await this._startModelTraining(selectedStyles, imagesPerStyle);
      }
    } catch (error) {
      console.error('Error in training workflow:', error);

      // Reset progress state
      this._setProgress({
        isTraining: false,
        progressPercentage: 0,
        progressMessage: '',
      });

      const detail = error instanceof Error ? error.message : 'Failed to start training';
      this._deps.notificationService.error('Training Error', detail);

      // Log detailed error information for debugging
      if (error instanceof Error && error.message.includes('Authentication failed')) {
        console.error('🔑 Authentication issue - check API tokens');
      } else if (error instanceof Error && error.message.includes('Configuration error')) {
        console.error('⚙️ Configuration issue - check settings');
      } else if (error instanceof Error && error.message.includes('Payment required')) {
        console.error('💳 Billing issue - check Replicate account');
      }
    }
  }

  /**
   * CRITICAL FIX: Determine if user has ANY trained model available
   * This prevents incorrect fallback to training when generation should be used
   */
  private _checkForExistingTrainedModel(latestTrainedModel: any, modelStatus: string): boolean {
    // Check 1: Explicit model status indicates ready
    if (modelStatus === 'Model Ready') {
      return true;
    }

    // Check 2: We have trained model data with version/ID
    if (latestTrainedModel) {
      const hasModelVersion = !!(
        latestTrainedModel.trainedModelVersion || latestTrainedModel.versionId
      );
      const hasModelId = !!(latestTrainedModel.replicateModelId || latestTrainedModel.modelId);

      if (hasModelVersion || hasModelId) {
        return true;
      }
    }

    // Check 3: Model status suggests training exists (even if not fully ready)
    const statusesWithModel = ['Training', 'ModelReady', 'Completed', 'Ready'];
    if (statusesWithModel.some(status => modelStatus?.includes(status))) {
      return true;
    }

    return false;
  }

  /**
   * CRITICAL FIX: Handle generation for users with existing trained models
   * This ensures we NEVER create training ZIPs for existing model users
   */
  private async _handleExistingModelGeneration(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    latestTrainedModel: any
  ): Promise<void> {
    try {
      // Attempt to get model version for generation
      const modelVersion = latestTrainedModel?.trainedModelVersion || latestTrainedModel?.versionId;
      const modelId = latestTrainedModel?.replicateModelId || latestTrainedModel?.modelId;

      this._deps.notificationService.info(
        'Using Existing Model',
        'Using your previously trained model for generation'
      );

      // Priority 1: Use explicit model version
      if (modelVersion) {
        console.log('✅ Using model version:', modelVersion);
        await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, modelVersion);
        return;
      }

      // Priority 2: Use model ID if available
      if (modelId) {
        console.log('✅ Using model ID:', modelId);
        await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, modelId);
        return;
      }

      // Priority 3: Attempt to refresh state and try again
      console.log('🔄 No version/ID found, refreshing state...');
      this._deps.stateService.forceRefresh();
      await this._deps.stateService.loadInitialDashboardData();
      await new Promise(resolve => setTimeout(resolve, 1000));

      const refreshedState = this._deps.stateService.getState();
      const refreshedModel = refreshedState?.latestTrainedModel;
      if (refreshedModel) {
        const refreshedVersion = refreshedModel.trainedModelVersion || refreshedModel.versionId;
        const refreshedId = refreshedModel.replicateModelId || refreshedModel.modelId;

        if (refreshedVersion) {
          console.log('✅ Found version after refresh:', refreshedVersion);
          await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, refreshedVersion);
          return;
        }

        if (refreshedId) {
          console.log('✅ Found ID after refresh:', refreshedId);
          await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, refreshedId);
          return;
        }
      }

      // Priority 4: Last resort - let backend resolve from database
      console.log('🎯 Using backend resolution with empty version');
      await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, '');
    } catch (error: any) {
      console.error('❌ Generation with existing model failed:', error);

      // Handle specific error cases
      const errorMsg = error?.message || error?.error?.message || '';

      if (
        errorMsg.toLowerCase().includes('model not found') ||
        errorMsg.toLowerCase().includes('version not found')
      ) {
        // Model data is stale, let backend resolve
        console.log('🔄 Model data stale, using backend resolution');
        await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, '');
      } else if (errorMsg.toLowerCase().includes('already have a trained model')) {
        // Backend says model exists but we couldn't find version - use backend resolution
        this._deps.notificationService.info('Model Found', 'Using your existing trained model');
        await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, '');
      } else {
        // Other errors - propagate up
        throw error;
      }
    }
  }

  // Lazy loading methods
  private async _loadFileUploadService(): Promise<FileUploadService> {
    if (!this._fileUploadService) {
      const { FileUploadService: fileUploadServiceClass } = await import('./file-upload.service');
      this._fileUploadService = this._injector.get(fileUploadServiceClass);
    }
    return this._fileUploadService;
  }

  private async _loadReplicateService(): Promise<ReplicateService> {
    if (!this._replicateService) {
      const { ReplicateService: replicateServiceClass } = await import('./replicate.service');
      this._replicateService = this._injector.get(replicateServiceClass);
    }
    return this._replicateService;
  }

  // Model training workflow
  private async _startModelTraining(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): Promise<void> {
    try {
      this._initializeTrainingProgress();
      await this._createTrainingZip();
      const zipUrl = await this._getTrainingZipUrl();
      const trainingId = await this._startReplicateTraining(zipUrl);
      this._finalizeTrainingSetup(trainingId);
      this._startTrainingStatusPolling(selectedStyles, imagesPerStyle);
    } catch (error: unknown) {
      console.error('Training startup error:', error);
      this._setProgress({ isTraining: false });

      // Provide more specific error messages based on the failure point
      const errorMessage = error instanceof Error ? error.message : 'Unknown error occurred';

      // Auto-fallback: if user already has a trained model, skip training and start generation
      if (errorMessage.toLowerCase().includes('already have a trained model')) {
        this._deps.notificationService.modelAlreadyTrained();
        // Use empty modelVersion to let backend resolve the latest Ready model
        await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, '');
        return;
      }

      if (errorMessage.includes('Failed to create training ZIP')) {
        const detailedMessage = errorMessage.includes('after cleanup and retry')
          ? 'Unable to prepare training files. Please ensure you have uploaded enough images and try again in a few minutes.'
          : 'Unable to create training package. Please try again.';
        throw new Error(detailedMessage);
      } else if (errorMessage.includes('Failed to get training ZIP URL')) {
        throw new Error('Training files were created but could not be accessed. Please try again.');
      } else if (errorMessage.includes('User not authenticated')) {
        throw new Error('Authentication expired. Please refresh the page and log in again.');
      } else {
        throw new Error(errorMessage);
      }
    }
  }

  private _initializeTrainingProgress(): void {
    this._setProgress({
      isTraining: true,
      progressPercentage: 0,
      progressMessage: 'Preparing your images for training...',
      estimatedCompletion: '15-20 minutes',
    });

    this._deps.notificationService.info(
      'Starting Training',
      'Creating training ZIP and starting model training...'
    );
  }

  private async _createTrainingZip(): Promise<void> {
    this._setProgress({
      progressPercentage: 10,
      progressMessage: 'Preparing training environment...',
    });

    const fileUploadService = await this._loadFileUploadService();

    // Clean up any existing training files to prevent conflicts
    try {
      this._setProgress({
        progressPercentage: 12,
        progressMessage: 'Cleaning up previous training files...',
      });

      await this._cleanupExistingTrainingFiles(fileUploadService);
    } catch (cleanupError) {
      console.warn('Non-critical cleanup warning:', cleanupError);
      // Continue with training even if cleanup fails - the backend should handle it
    }

    // Create the new training ZIP
    this._setProgress({
      progressPercentage: 15,
      progressMessage: 'Creating training package from your images...',
    });

    const zipResult = await this._createTrainingZipWithRetry(fileUploadService);

    if (!zipResult?.success || !zipResult.zipCreated) {
      throw new Error(
        (zipResult as any)?.error?.message ||
          (zipResult as any)?.message ||
          'Failed to create training ZIP after cleanup and retry'
      );
    }
  }

  private async _cleanupExistingTrainingFiles(fileUploadService: FileUploadService): Promise<void> {
    try {
      // List existing training files
      const existingFiles = (await firstValueFrom(fileUploadService.listTrainingFiles())) as {
        success: boolean;
        data: string[];
        error: any;
      };

      if (existingFiles?.success && existingFiles.data && existingFiles.data.length > 0) {
        console.log(
          `🧹 Found ${existingFiles.data.length} existing training files, cleaning up...`
        );

        // Delete all existing training files to prevent conflicts
        const deleteResult = (await firstValueFrom(fileUploadService.deleteAllTrainingFiles())) as {
          success: boolean;
          message: string;
        };

        if (deleteResult?.success) {
          console.log('✅ Successfully cleaned up existing training files');
        } else {
          console.warn('⚠️ Cleanup completed with warnings:', deleteResult?.message);
        }
      } else {
        console.log('ℹ️ No existing training files found, proceeding with creation');
      }
    } catch (error) {
      console.warn('⚠️ Error during training file cleanup:', error);
      // Don't throw - let the main creation attempt handle any remaining conflicts
    }
  }

  private async _createTrainingZipWithRetry(
    fileUploadService: FileUploadService,
    maxRetries = 2
  ): Promise<any> {
    let lastError: any = null;

    for (let attempt = 1; attempt <= maxRetries; attempt++) {
      try {
        console.log(`🚀 Training ZIP creation attempt ${attempt}/${maxRetries}`);

        const zipResult = await firstValueFrom(fileUploadService.createTrainingZip());

        if (zipResult?.success && zipResult.zipCreated) {
          console.log('✅ Training ZIP created successfully');
          return zipResult;
        } else {
          lastError = zipResult;
          console.warn(`⚠️ Attempt ${attempt} failed:`, zipResult?.message || zipResult?.error);
        }
      } catch (error) {
        lastError = error;
        console.warn(`⚠️ Attempt ${attempt} threw error:`, error);

        // If this is a file conflict error and not the last attempt, try cleanup again
        if (attempt < maxRetries && this._isFileConflictError(error)) {
          console.log('🔄 Detected file conflict, attempting additional cleanup...');

          try {
            await firstValueFrom(fileUploadService.deleteAllTrainingFiles());
            // Brief delay to allow file system to settle
            await new Promise(resolve => setTimeout(resolve, 1000));
          } catch (cleanupError) {
            console.warn('Additional cleanup failed:', cleanupError);
          }
        }
      }

      // Brief delay between retries
      if (attempt < maxRetries) {
        await new Promise(resolve => setTimeout(resolve, 2000));
      }
    }

    // All attempts failed, return the last error
    return lastError;
  }

  private _isFileConflictError(error: any): boolean {
    const errorMessage = error?.message || error?.error?.message || '';
    const conflictIndicators = [
      'already exists',
      'file conflict',
      'cannot create',
      'permission denied',
      'access denied',
      'file in use',
    ];

    return conflictIndicators.some(indicator =>
      errorMessage.toLowerCase().includes(indicator.toLowerCase())
    );
  }

  private async _getTrainingZipUrl(): Promise<string> {
    this._setProgress({
      progressPercentage: 25,
      progressMessage: 'Uploading training data...',
    });

    const fileUploadService = await this._loadFileUploadService();
    const latestZipResult = await firstValueFrom(fileUploadService.getLatestTrainingZip());

    if (!latestZipResult?.success || !latestZipResult.data?.publicUrl) {
      throw new Error('Failed to get training ZIP URL');
    }

    return latestZipResult.data.publicUrl;
  }

  private async _startReplicateTraining(zipUrl: string): Promise<string> {
    const userId = this._deps.authService.getCurrentUserId();
    if (!userId) {
      console.warn('Failed to get user ID. Token exists:', !!this._deps.authService.getToken());
      console.warn('Authentication status:', this._deps.authService.isAuthenticated());
      throw new Error('User not authenticated - unable to extract user ID from token');
    }
    console.warn('Starting training for user ID:', userId);

    this._setProgress({
      progressPercentage: 35,
      progressMessage: 'Initializing AI model training...',
    });

    const trainRequest: TrainModelRequest = { userId, imageZipUrl: zipUrl };
    const replicateService = await this._loadReplicateService();

    console.log('🚀 Starting training request:', { userId, zipUrl });
    const trainResult = await firstValueFrom(replicateService.trainModel(trainRequest));

    if (!trainResult?.success) {
      console.error('❌ Training request failed:', trainResult);

      // Provide specific error messages based on error codes
      const errorCode = trainResult?.error?.code;
      const errorMessage = trainResult?.error?.message || 'Failed to start model training';

      let userMessage = errorMessage;
      if (errorCode === 'ReplicateAuthFailed') {
        userMessage = 'Authentication failed with Replicate API. Please contact support.';
      } else if (errorCode === 'ReplicateConfigError') {
        userMessage = 'Configuration error. Please contact support.';
      } else if (errorCode === 'TrainingFailed') {
        userMessage = 'Training service temporarily unavailable. Please try again later.';
      } else if (errorCode === 'InsufficientCredits') {
        userMessage = 'Insufficient credits for training. Please purchase more credits.';
      } else if (errorCode === 'ModelAlreadyTrained') {
        userMessage = 'You already have a trained model. Please use image generation instead.';
      }

      throw new Error(userMessage);
    }

    console.log('✅ Training request successful:', trainResult);
    // API returns the training result under data.prediction
    return trainResult.data!.prediction.id;
  }

  private _finalizeTrainingSetup(trainingId: string): void {
    this._setProgress({
      trainingId,
      progressPercentage: 45,
      progressMessage: 'AI model is learning your features...',
    });

    this._deps.notificationService.success(
      'Training Started',
      'Model training has begun. This will take 15-20 minutes.'
    );

    const estimatedMinutes = 18;
    const completionTime = new Date(Date.now() + estimatedMinutes * 60000);
    this._setProgress({
      estimatedCompletion: completionTime.toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
      }),
    });
  }

  // Training status polling
  private _startTrainingStatusPolling(selectedStyles: StyleOption[], imagesPerStyle: number): void {
    let progressIncrement = 0;
    const maxTrainingProgress = 90; // Training goes up to 90%, generation takes the last 10%

    this._pollingInterval = this._ngZone.runOutsideAngular(() =>
      setInterval(async () => {
        this._ngZone.run(async () => {
          try {
            const currentProgress = this.getProgress();
            if (!currentProgress.trainingId) {
              clearInterval(this._pollingInterval);
              return;
            }

            const replicateService = await this._loadReplicateService();
            const statusResult = await firstValueFrom(
              replicateService.getTrainingStatus(currentProgress.trainingId)
            );

            if (!statusResult?.success) {
              console.error('Failed to get training status:', statusResult?.error);
              return;
            }

            const status = statusResult.data.status;

            // Increment progress during training (45% to 90%)
            if (status === 'processing' || status === 'starting') {
              progressIncrement += 2; // Increment by 2% every 30 seconds
              const newProgress = Math.min(45 + progressIncrement, maxTrainingProgress);

              let message = 'AI is analyzing your facial features...';
              if (newProgress >= 60 && newProgress < 80) {
                message = 'Training neural network on your photos...';
              } else if (newProgress >= 80) {
                message = 'Finalizing your custom AI model...';
              }

              this._setProgress({
                progressPercentage: newProgress,
                progressMessage: message,
              });
            }

            if (status === 'succeeded') {
              clearInterval(this._pollingInterval);
              this._setProgress({
                progressPercentage: maxTrainingProgress,
                progressMessage: 'Model training complete! Starting image generation...',
                isTraining: false,
              });

              this._deps.notificationService.success(
                'Training Complete',
                'Model training finished! Starting image generation...'
              );

              // Force reload dashboard data to get updated model status
              // Wait a bit for the webhook to update the database
              await new Promise(resolve =>
                this._ngZone.runOutsideAngular(() => setTimeout(resolve, 3000))
              );
              await this._deps.stateService.loadInitialDashboardData();

              // Wait for state to update
              await new Promise(resolve =>
                this._ngZone.runOutsideAngular(() => setTimeout(resolve, 500))
              );

              // Start generation only with the actual user model version
              // Avoid using training status version (which is the trainer's version)
              const resolvedVersion = await this._waitForUserModelVersionId(6, 1000);
              if (resolvedVersion) {
                await this._generateImagesWithStyles(
                  selectedStyles,
                  imagesPerStyle,
                  resolvedVersion
                );
              } else {
                this._deps.notificationService.error(
                  'Generation Delayed',
                  "Your model is trained, but the version isn't available yet. Please wait a few seconds and try again."
                );
              }
            } else if (status === 'failed') {
              clearInterval(this._pollingInterval);
              this._setProgress({
                isTraining: false,
                progressPercentage: 0,
                progressMessage: '',
              });
              this._deps.notificationService.error(
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
  private async _generateImagesWithStyles(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    modelVersion: string
  ): Promise<void> {
    try {
      const userId = this._deps.authService.getCurrentUserId();
      if (!userId) {
        throw new Error('User not authenticated - unable to extract user ID from token');
      }

      // Clear previous generation state and caches
      this._clearAllIntervals();

      this._setProgress({
        isGenerating: true,
        lastGenerationCount: 0,
        showLastGenerationMessage: false,
        generationStartTime: 0, // Will be set after successful API call
        activePredictionIds: [], // Clear any previous prediction tracking
      });

      // Invalidate image cache to ensure fresh data
      const fileUploadService = await this._loadFileUploadService();
      fileUploadService.invalidateUserImagesCache();

      this._deps.notificationService.info(
        'Generating Images',
        `Starting batch generation for ${selectedStyles.length} style(s)...`
      );

      // CONSOLIDATED APPROACH: Generate images for all selected styles in a single batch request
      const generateRequest: GenerateBatchImagesRequest = {
        trainedModelVersion: modelVersion,
        userId,
        styles: selectedStyles.map(style => style.name),
        userInfo: {
          gender: this._deps.stateService.getState().userProfile?.gender,
          ethnicity: this._deps.stateService.getState().userProfile?.ethnicity,
        },
        numOutputsPerStyle: imagesPerStyle, // Use the selected number of images per style
      };

      // Store precise timestamp BEFORE API call to capture all generated images
      const preciseGenerationStartTime = Date.now();

      const replicateService = await this._loadReplicateService();
      const generateResult = await firstValueFrom(
        replicateService.generateBatchImages(generateRequest)
      );

      if (!generateResult?.success) {
        throw new Error(generateResult?.error?.message || 'Batch generation failed');
      }

      const { successfulStyles, failedStyles, failures, creditsCost, predictions } =
        generateResult.data;

      // Extract prediction IDs for tracking
      const predictionIds = (predictions as PredictionResult[]).map(p => p.result.id);

      // Store prediction IDs for polling
      this._setProgress({
        activePredictionIds: predictionIds,
        generationStartTime: preciseGenerationStartTime,
      });

      // Report results to user
      if (successfulStyles > 0) {
        this._deps.notificationService.success(
          'Generation Started',
          `Successfully started generation for ${successfulStyles} style(s). ` +
            `Images will appear in your gallery when ready.`
        );
      }

      if (failedStyles > 0) {
        const failedStyleNames = (failures as GenerationFailure[])
          .map((f: GenerationFailure) => f.style)
          .join(', ');
        this._deps.notificationService.warning(
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
      this._setProgress({
        progressPercentage: 15,
        progressMessage: `Creating professional photos with your selected styles...`,
        generationStartTime: preciseGenerationStartTime,
        expectedGenerationTime: estimatedMinutes * 60000,
        estimatedCompletion: `${Math.ceil(estimatedMinutes)} minutes`,
      });

      // Start time-based progress updates
      this._startTimeBasedProgress();

      const timeString = estimatedCompletion.toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
      });

      this._deps.notificationService.info(
        'Generation Progress',
        `Generating ${successfulStyles} style(s) with ${imagesPerStyle} images each. ` +
          `Estimated completion: ${timeString}. Cost: ${creditsCost} credits.`
      );

      // Start polling for prediction completion
      await this._startPredictionCompletionPolling();

      // Refresh dashboard state to update model status
      await this._deps.stateService.loadInitialDashboardData();
    } catch (error: unknown) {
      console.error('Error in batch image generation:', error);
      this._setProgress({
        isGenerating: false,
        progressPercentage: 0,
        progressMessage: '',
      });
      this._deps.notificationService.error(
        'Generation Error',
        error instanceof Error ? error.message : 'Failed to generate images'
      );
    }
  }

  // Waits for the user's custom model version to be available in state (poll + refresh)
  // Returns the version string when found, otherwise null
  private async _waitForUserModelVersionId(attempts = 5, delayMs = 1000): Promise<string | null> {
    for (let i = 0; i < attempts; i++) {
      const state = this._deps.stateService.getState();
      const version =
        state.userProfile?.trainedModelVersionId ||
        state.latestTrainedModel?.trainedModelVersion ||
        state.latestTrainedModel?.versionId;
      if (version) {
        return version;
      }
      // Reload dashboard data to pick up DB updates, then wait briefly
      try {
        await this._deps.stateService.loadInitialDashboardData();
      } catch {}
      await new Promise(resolve =>
        this._ngZone.runOutsideAngular(() => setTimeout(resolve, delayMs))
      );
    }
    return null;
  }

  // Prediction completion polling - tracks specific prediction IDs
  private async _startPredictionCompletionPolling(): Promise<void> {
    const currentProgress = this.getProgress();
    const predictionIds = currentProgress.activePredictionIds;

    if (!predictionIds || predictionIds.length === 0) {
      return;
    }

    // Clear any existing polling interval to prevent duplicates
    if (this._photoCompletionPollingInterval) {
      clearInterval(this._photoCompletionPollingInterval);
    }

    this._photoCompletionPollingInterval = this._ngZone.runOutsideAngular(() =>
      setInterval(async () => {
        this._ngZone.run(async () => {
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
                  const replicateService = await this._loadReplicateService();
                  const result = await firstValueFrom(
                    replicateService.getPredictionStatus(predictionId)
                  );
                  return {
                    id: predictionId,
                    status: result?.success ? result.data.status : 'unknown',
                    data: result?.data,
                  };
                } catch {
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

              this._setProgress({
                progressPercentage: progress,
                progressMessage: `Processing ${completedPredictions.length} of ${totalPredictions} generations...`,
              });

              // Clear time-based progress since we have real progress
              if (this._timeBasedProgressInterval) {
                clearInterval(this._timeBasedProgressInterval);
                this._timeBasedProgressInterval = undefined;
              }
            }

            // Check if all predictions are completed (successfully)
            if (completedPredictions.length === totalPredictions) {
              // Stop polling and generating
              clearInterval(this._photoCompletionPollingInterval);
              this._setProgress({ isGenerating: false });
              this._onPhotoGenerationComplete(totalPredictions);
            } else if (
              failedPredictions.length > 0 &&
              completedPredictions.length + failedPredictions.length === totalPredictions
            ) {
              // All predictions finished but some failed
              clearInterval(this._photoCompletionPollingInterval);
              this._setProgress({ isGenerating: false });

              if (completedPredictions.length > 0) {
                // Some succeeded
                this._onPhotoGenerationComplete(completedPredictions.length);
                this._deps.notificationService.warning(
                  'Partial Success',
                  `${completedPredictions.length} photos generated successfully, ${failedPredictions.length} failed.`
                );
              } else {
                // All failed
                this._deps.notificationService.error(
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
  private _startTimeBasedProgress(): void {
    // Clear any existing time-based progress interval
    if (this._timeBasedProgressInterval) {
      clearInterval(this._timeBasedProgressInterval);
    }

    this._timeBasedProgressInterval = this._ngZone.runOutsideAngular(() =>
      setInterval(() => {
        this._ngZone.run(() => {
          const currentProgress = this.getProgress();

          if (!currentProgress.isGenerating || currentProgress.generationStartTime === 0) {
            return;
          }

          const elapsed = Date.now() - currentProgress.generationStartTime;
          // Cap at 85% for time-based progress
          const progressRatio = Math.min(elapsed / currentProgress.expectedGenerationTime, 0.85);
          const newProgress = 15 + progressRatio * 70; // 15% to 85% based on time

          // Update progress message based on elapsed time
          // elapsedMinutes variable removed as it was unused
          const remainingTime = Math.max(
            0,
            Math.ceil((currentProgress.expectedGenerationTime - elapsed) / 60000)
          );

          let message = `Creating professional photos... (~${remainingTime} min remaining)`;
          if (remainingTime <= 0) {
            message = 'Finalizing your photos...';
          }

          this._setProgress({
            progressPercentage: Math.round(newProgress),
            progressMessage: message,
          });
        });
      }, 10000)
    ); // Update every 10 seconds
  }

  // Photo generation completion
  private _onPhotoGenerationComplete(photoCount: number): void {
    // Clear all intervals
    if (this._timeBasedProgressInterval) {
      clearInterval(this._timeBasedProgressInterval);
      this._timeBasedProgressInterval = undefined;
    }

    // Invalidate cache BEFORE showing celebration to ensure gallery has fresh data
    if (this._fileUploadService) {
      this._fileUploadService.invalidateUserImagesCache();
    }

    // Complete the generation process
    this._setProgress({
      progressPercentage: 100,
      progressMessage: 'All photos ready!',
      isGenerating: false,
      lastGenerationCount: photoCount,
      showLastGenerationMessage: true,
    });

    this._deps.notificationService.success(
      'Photos Ready!',
      `${photoCount} professional photos are ready to view in your gallery.`
    );

    // Refresh photo count and dashboard state
    this._ngZone.runOutsideAngular(() => {
      setTimeout(() => {
        this._ngZone.run(() => {
          this._deps.stateService.refreshGeneratedPhotosCount();
          this._deps.stateService.invalidateAndRefreshImages();
        });
      }, 500);
    });

    // Reset progress after showing completion
    this._ngZone.runOutsideAngular(() => {
      setTimeout(() => {
        this._ngZone.run(() => {
          this._setProgress({
            progressPercentage: 0,
            progressMessage: '',
          });
        });
      }, 3000);
    });
  }

  // Utility methods
  dismissSuccessMessage(): void {
    this._setProgress({
      showLastGenerationMessage: false,
      lastGenerationCount: 0,
    });
  }

  // Cleanup method
  dispose(): void {
    this._clearAllIntervals();
    this.resetProgress();
  }
}
