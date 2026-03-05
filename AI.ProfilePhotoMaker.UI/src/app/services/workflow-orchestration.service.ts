import { Injectable, inject, Injector, NgZone } from '@angular/core';
import { LoggingService } from './logging.service';
import { BehaviorSubject, Observable, Subject, firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { NotificationService } from './notification.service';
import { DashboardStateService } from './dashboard-state.service';
import { SubscriptionStateService } from './subscription-state.service';
import { ConfigService } from './config.service';
import { ModelStatusService } from './model-status.service';
import { StyleOption } from '../components/dashboard/style-selector/style-selector.component';
// Removed unused imports to reduce lint noise

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
  // Additional methods used at runtime (declared here to satisfy type checker)
  getUnifiedModelStatus(): Observable<any>;
  getUserModelRequests(): Observable<{
    success: boolean;
    data?: {
      totalRequests: number;
      hasTrainedModel: boolean;
      latestTrainedModel: any;
      allRequests: any[];
    };
    error?: any;
  }>;
}

interface ReplicateService {
  trainModel(request: TrainModelRequest): Observable<TrainingStartApiResponse>;
  getTrainingStatus(trainingId: string): Observable<TrainingStatusResponse>;
  finalizeTraining(trainingId: string): Observable<{ success: boolean; data: any; error: any }>;
  queueGeneration(
    trainingId: string,
    styles: string[],
    numOutputsPerStyle: number
  ): Observable<any>;
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

  /** Emits when a generation attempt is blocked because the user has 0 credits. */
  readonly showOutOfCreditsModal$ = new Subject<void>();

  private _pollingInterval?: NodeJS.Timeout;
  private _photoCompletionPollingInterval?: NodeJS.Timeout;
  private _timeBasedProgressInterval?: NodeJS.Timeout;
  /** Suppresses direct frontend generation when backend handles it via PendingGenerationService.
   *  Set in queueBackgroundGeneration(), reset in _startModelTraining() and resetProgress().
   *  See also DashboardComponent._backgroundQueued for the UI-level click guard. */
  private _backgroundGenerationQueued = false;

  // Lazy-loaded services
  private _fileUploadService: FileUploadService | null = null;
  private _replicateService: ReplicateService | null = null;

  // Core dependencies
  private readonly _deps: WorkflowOrchestrationDependencies;

  // Use inject pattern to reduce constructor parameters
  private readonly _ngZone = inject(NgZone);
  private readonly _injector = inject(Injector);
  private readonly _logger = inject(LoggingService);

  constructor(
    authService: AuthService,
    notificationService: NotificationService,
    stateService: DashboardStateService,
    subscriptionState: SubscriptionStateService,
    config: ConfigService,
    private readonly _modelStatus: ModelStatusService
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
    this._backgroundGenerationQueued = false;
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

    // Paywall enforcement: training + styled generation use total credits
    const availableCredits = this._getAvailableCredits();
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
    if (this._modelStatus.canGenerate(modelStatus)) {
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

  private _calculateCreditsFromState(state: any): number {
    const userCreditStatus = state.userCreditStatus;
    const creditsInfo = state.creditsInfo;

    return userCreditStatus?.credits || creditsInfo?.credits || 0;
  }

  private _getAvailableCredits(): number {
    // Primary: subscription state (used across the UI)
    const subscriptionState = this._deps.subscriptionState.getState();
    if (subscriptionState.userCreditStatus?.credits !== undefined) {
      return subscriptionState.userCreditStatus.credits;
    }
    if (subscriptionState.creditsInfo?.credits !== undefined) {
      return subscriptionState.creditsInfo.credits;
    }

    // Secondary: dashboard state
    const state = this._deps.stateService.getState();
    if (state.userCreditStatus?.credits !== undefined) {
      return state.userCreditStatus.credits;
    }
    if (state.creditsInfo?.credits !== undefined) {
      return state.creditsInfo.credits;
    }

    // Last resort: totalCredits (prefer over zero to avoid blocking legitimate users)
    return state.totalCredits || subscriptionState.totalCredits || 0;
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
      const availableCredits = this._getAvailableCredits();

      if (availableCredits === 0) {
        // Emit modal trigger — dashboard will intercept and show the out-of-credits modal
        this.showOutOfCreditsModal$.next();
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
      const isLoadingState =
        currentState.modelStatus === 'Loading...' ||
        currentState.modelStatus === '' ||
        !currentState.modelStatus;

      if (isLoadingState) {
        this._logger.workflowDebug('Waiting for model status refresh before routing decision');
        await this._deps.stateService.loadInitialDashboardData();
        await new Promise(resolve => setTimeout(resolve, 1000)); // Allow time for state updates

        // Get refreshed state
        const refreshedState = this._deps.stateService.getState();
        this._logger.workflowDebug('Refreshed dashboard state after loading', {
          modelStatus: refreshedState.modelStatus,
          hasLatestModel: !!refreshedState.latestTrainedModel,
        });

        // Use refreshed state
        Object.assign(currentState, refreshedState);
      }

      let modelStatus = currentState.modelStatus;
      let latestTrainedModel = currentState.latestTrainedModel;

      // Force a fresh unified status read to avoid stale cache
      try {
        const fileUploadService = await this._loadFileUploadService();
        const unified = await firstValueFrom(fileUploadService.getUnifiedModelStatus());
        if (unified?.statusCode === 'ModelReady' && unified.hasTrainedModel) {
          modelStatus = 'ModelReady';
          if (!latestTrainedModel || !latestTrainedModel.trainedModelVersion) {
            const requests = await firstValueFrom(fileUploadService.getUserModelRequests());
            if (requests?.success && requests.data?.latestTrainedModel) {
              latestTrainedModel = requests.data.latestTrainedModel;
            }
          }
        }
      } catch (freshErr) {
        this._logger.warn(
          'Unified model status refresh failed; proceeding with existing state',
          freshErr
        );
      }

      // CRITICAL FIX: Determine if user has ANY trained model using proper data validation
      const hasTrainedModel = this._checkForExistingTrainedModel(latestTrainedModel, modelStatus);

      this._logger.workflowDebug('Workflow routing decision', {
        modelStatus,
        hasTrainedModel,
        latestTrainedModel: !!latestTrainedModel,
        modelVersion: latestTrainedModel?.trainedModelVersion || latestTrainedModel?.versionId,
        modelId: latestTrainedModel?.replicateModelId || latestTrainedModel?.modelId,
        routingDecision: hasTrainedModel ? 'GENERATION' : 'TRAINING',
      });

      if (hasTrainedModel) {
        // User has a trained model - always use generation, never training
        this._logger.workflowDebug('Routing to generation: user has trained model');
        await this._handleExistingModelGeneration(
          selectedStyles,
          imagesPerStyle,
          latestTrainedModel
        );
      } else {
        // User needs a new model - start training process
        this._logger.workflowDebug('Routing to training: user needs new model');
        await this._startModelTraining(selectedStyles, imagesPerStyle);
      }
    } catch (error) {
      this._logger.error('Training workflow error', {
        errorMessage: error instanceof Error ? error.message : 'Unknown error',
        errorType: error instanceof Error ? error.constructor.name : typeof error,
        stackTrace: error instanceof Error ? error.stack?.substring(0, 500) : undefined,
        currentState: {
          modelStatus: currentState?.modelStatus,
          hasLatestModel: !!currentState?.latestTrainedModel,
          userAuth: this._deps.authService.isAuthenticated(),
        },
      });

      // CRITICAL FIX: Handle ModelAlreadyTrained error by routing to generation
      const errorMessage = error instanceof Error ? error.message : String(error);
      const apiError = (error as any)?.error;

      if (
        apiError?.code === 'ModelAlreadyTrained' ||
        errorMessage.includes('ModelAlreadyTrained')
      ) {
        this._logger.workflowDebug(
          'Fallback recovery: ModelAlreadyTrained detected, attempting generation route'
        );

        try {
          // Force refresh model status to get latest data
          await this._deps.stateService.loadInitialDashboardData();
          await new Promise(resolve => setTimeout(resolve, 1000));

          const refreshedState = this._deps.stateService.getState();

          // Route to generation using any available model data
          await this._handleExistingModelGeneration(
            selectedStyles,
            imagesPerStyle,
            refreshedState.latestTrainedModel
          );

          // Show success message instead of error
          this._deps.notificationService.success(
            'Model Available!',
            "Great news! You already have a trained model. We're using it to generate your photos."
          );

          return; // Exit successfully
        } catch (fallbackError) {
          this._logger.error('Fallback generation also failed', fallbackError);
          // Continue to normal error handling below
        }
      }

      // Reset progress state
      this._setProgress({
        isTraining: false,
        progressPercentage: 0,
        progressMessage: '',
      });

      // Enhanced error categorization and user feedback
      const errorDetails = this._categorizeWorkflowError(error);
      this._deps.notificationService.error(errorDetails.title, errorDetails.message);

      // Log specific debugging information
      this._logErrorDiagnostics(error, errorDetails.category);
    }
  }

  /**
   * CRITICAL FIX: Determine if user has ANY trained model available
   * This prevents incorrect fallback to training when generation should be used
   * UPDATED: Now properly validates actual model data exists, matching backend validation
   */
  private _checkForExistingTrainedModel(_latestTrainedModel: any, modelStatus: string): boolean {
    // Single source of truth: Use semantic status validation
    if (this._modelStatus.canGenerate(modelStatus)) {
      this._logger.workflowDebug('Model ready for generation - routing to generation workflow', {
        modelStatus,
      });
      return true;
    }

    // Only check for training states that require different handling
    if (this._modelStatus.isTraining(modelStatus)) {
      this._logger.workflowDebug('Training in progress; will poll for completion');
      return false;
    }

    // Default: No model available
    this._logger.workflowDebug('No trained model available; initiating training workflow');
    return false;
  }

  /**
   * CRITICAL FIX: Handle generation for users with existing trained models
   * This ensures we NEVER create training ZIPs for existing model users
   * UPDATED: Only show "Using Existing Model" toast when model data actually exists
   */
  private async _handleExistingModelGeneration(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    latestTrainedModel: any
  ): Promise<void> {
    // Try model version first, fall back to backend resolution
    const modelVersion =
      latestTrainedModel?.trainedModelVersion || latestTrainedModel?.versionId || '';

    this._deps.notificationService.info(
      'Using Existing Model',
      'Generating with your trained model'
    );

    // Let backend resolve model details if version not available
    await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, modelVersion);
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
    // Reset background generation flag for this new session
    this._backgroundGenerationQueued = false;

    try {
      // Safety guard: never train if model already ready (race conditions, stale state)
      try {
        const fileUploadService = await this._loadFileUploadService();
        const unified = await firstValueFrom(fileUploadService.getUnifiedModelStatus());
        if (unified?.statusCode === 'ModelReady' && unified.hasTrainedModel) {
          this._logger.workflowDebug(
            'Model already ready at training start; routing to generation'
          );
          // Try to get latest model details
          let latestTrainedModel: any = null;
          try {
            const requests = await firstValueFrom(fileUploadService.getUserModelRequests());
            latestTrainedModel = requests?.success ? requests.data?.latestTrainedModel : null;
          } catch {}
          await this._handleExistingModelGeneration(
            selectedStyles,
            imagesPerStyle,
            latestTrainedModel
          );
          return;
        }
      } catch (guardErr) {
        this._logger.warn(
          'Pre-training readiness guard failed; proceeding with training',
          guardErr
        );
      }

      this._initializeTrainingProgress();
      await this._createTrainingZip();
      const zipUrl = await this._getTrainingZipUrl();
      const trainingId = await this._startReplicateTraining(zipUrl);
      this._finalizeTrainingSetup(trainingId);
      this._startTrainingStatusPolling(selectedStyles, imagesPerStyle);
    } catch (error: unknown) {
      this._logger.error('Training startup error', error);
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
      this._logger.warn('Non-critical training cleanup warning', cleanupError);
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
        this._logger.workflowDebug('Cleaning up existing training files before creating new ZIP', {
          existingFiles: existingFiles.data.length,
        });

        // Delete all existing training files to prevent conflicts
        const deleteResult = (await firstValueFrom(fileUploadService.deleteAllTrainingFiles())) as {
          success: boolean;
          message: string;
        };

        if (deleteResult?.success) {
          this._logger.workflowDebug('Successfully cleaned up existing training files');
        } else {
          this._logger.warn(
            'Cleanup completed with warnings before training ZIP creation',
            deleteResult?.message
          );
        }
      } else {
        this._logger.workflowDebug('No existing training files found; proceeding with creation');
      }
    } catch (error) {
      this._logger.warn('Error during training file cleanup', error);
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
        this._logger.workflowDebug('Training ZIP creation attempt', { attempt, maxRetries });

        const zipResult = await firstValueFrom(fileUploadService.createTrainingZip());

        if (zipResult?.success && zipResult.zipCreated) {
          this._logger.workflowDebug('Training ZIP created successfully');
          return zipResult;
        } else {
          lastError = zipResult;
          this._logger.warn(
            `Training ZIP attempt ${attempt} failed`,
            zipResult?.message || zipResult?.error
          );
        }
      } catch (error) {
        lastError = error;
        this._logger.warn(`Training ZIP attempt ${attempt} threw error`, error);

        // If this is a file conflict error and not the last attempt, try cleanup again
        if (attempt < maxRetries && this._isFileConflictError(error)) {
          this._logger.workflowDebug(
            'Detected file conflict during ZIP creation; attempting additional cleanup'
          );

          try {
            await firstValueFrom(fileUploadService.deleteAllTrainingFiles());
            // Brief delay to allow file system to settle
            await new Promise(resolve => setTimeout(resolve, 1000));
          } catch (cleanupError) {
            this._logger.warn(
              'Additional cleanup failed while resolving file conflict',
              cleanupError
            );
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
    // With HttpOnly-cookie auth, the client does not have access to JWT claims.
    // Backend derives the user from the cookie; the DTO `UserId` is optional and
    // validated against the authenticated user when present. Send an empty
    // string here and rely on server-side user context.
    const userId = '';
    this._logger.workflowDebug('Starting training (server-enforced user context)');

    this._setProgress({
      progressPercentage: 35,
      progressMessage: 'Initializing AI model training...',
    });

    const trainRequest: TrainModelRequest = { userId, imageZipUrl: zipUrl };
    const replicateService = await this._loadReplicateService();

    this._logger.workflowDebug('Training request details', {
      userId,
      zipUrlPreview: zipUrl.substring(0, 50) + '...',
    });

    try {
      const trainResult = await firstValueFrom(replicateService.trainModel(trainRequest));

      if (!trainResult?.success) {
        this._logger.error('Training API response failed', {
          success: trainResult?.success,
          errorCode: trainResult?.error?.code,
          errorMessage: trainResult?.error?.message,
          fullResponse: trainResult,
        });

        const errorDetails = this._parseTrainingError(trainResult);
        throw new Error(errorDetails.userMessage);
      }

      this._logger.workflowDebug('Training request successful', {
        predictionId: trainResult.data?.prediction?.id,
        success: true,
      });
      return trainResult.data!.prediction.id;
    } catch (error: any) {
      // Enhanced error logging for debugging
      this._logger.error('Training request error details', {
        errorType: error.constructor.name,
        errorMessage: error.message,
        errorStack: error.stack?.substring(0, 500),
        httpStatus: error.status,
        httpStatusText: error.statusText,
        apiResponse: error.error,
      });

      // Parse and enhance error for user display
      const enhancedError = this._enhanceTrainingError(error);
      throw new Error(enhancedError.userMessage);
    }
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
              this._logger.error('Failed to get training status', statusResult?.error);
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
                progressMessage: 'Model training complete! Finalizing your model...',
                isTraining: false,
              });

              this._deps.notificationService.success(
                'Training Complete',
                'Model training finished! Getting your model ready for generation...'
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

              // Actively finalize training to resolve the user's model version,
              // then automatically start generation as soon as it's ready.
              const finalVersion = await this._attemptFinalizeTraining(
                this.getProgress().trainingId
              );
              if (finalVersion) {
                if (this._backgroundGenerationQueued) {
                  // Backend will handle generation via ProcessTrainingCompletion → ProcessAsync
                  this._logger.workflowDebug(
                    'Skipping frontend generation — background generation queued'
                  );
                } else {
                  await this._generateImagesWithStyles(
                    selectedStyles,
                    imagesPerStyle,
                    finalVersion
                  );
                }
              } else {
                // If we somehow still couldn't resolve, inform user but avoid hard stop
                this._deps.notificationService.warning(
                  'Model Finalizing',
                  'Your model is finalizing on Replicate. We will start generation automatically once ready.'
                );
                // Soft fallback: continue trying in background for a bit
                this._ngZone.runOutsideAngular(() => {
                  setTimeout(async () => {
                    this._ngZone.run(async () => {
                      const laterVersion = await this._attemptFinalizeTraining(
                        this.getProgress().trainingId,
                        60000
                      );
                      if (laterVersion) {
                        if (this._backgroundGenerationQueued) {
                          this._logger.workflowDebug(
                            'Skipping frontend generation (retry path) — background generation queued'
                          );
                        } else {
                          await this._generateImagesWithStyles(
                            selectedStyles,
                            imagesPerStyle,
                            laterVersion
                          );
                        }
                      }
                    });
                  }, 15000);
                });
              }
            } else if (status === 'failed') {
              clearInterval(this._pollingInterval);
              this._backgroundGenerationQueued = false;
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
            this._logger.error('Error polling training status', error);
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
      // Cookie-based auth: do not depend on client-side JWT for user ID
      const userId = '';

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

      const canonicalStyles = selectedStyles
        .map(style => this._toCanonicalStyleName(style.name))
        .filter((style): style is string => style.length > 0);

      if (canonicalStyles.length === 0) {
        throw new Error('No valid styles selected for generation');
      }

      // CONSOLIDATED APPROACH: Generate images for all selected styles in a single batch request
      const generateRequest: GenerateBatchImagesRequest = {
        trainedModelVersion: modelVersion,
        userId,
        styles: canonicalStyles,
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
      this._logger.error('Error in batch image generation', error);
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

  // Finalize training by calling the backend finalize endpoint with retries and backoff.
  // Returns the resolved version string when available, otherwise null.
  private async _attemptFinalizeTraining(
    trainingId: string,
    maxWaitMs = 120000
  ): Promise<string | null> {
    if (!trainingId) {
      return null;
    }
    const replicateService = await this._loadReplicateService();

    const start = Date.now();
    let attempt = 0;
    let nextDelay = 5000; // initial 5s

    while (Date.now() - start < maxWaitMs) {
      attempt++;
      try {
        // Update UI message while waiting
        this._setProgress({
          progressMessage: 'Finalizing your model on Replicate...',
        });

        const resp = await firstValueFrom(replicateService.finalizeTraining(trainingId));
        if (resp?.success && resp?.data?.ready && resp?.data?.version) {
          return resp.data.version;
        }

        // Use server-provided retry hint if present
        const retryAfter = (resp?.data?.retryAfterSeconds ?? 15) * 1000;
        nextDelay = Math.max(nextDelay, retryAfter);
      } catch (err) {
        // Non-fatal; proceed with backoff
        this._logger.warn('Finalize training attempt failed', { attempt, err });
      }

      await new Promise(resolve =>
        this._ngZone.runOutsideAngular(() => setTimeout(resolve, nextDelay))
      );
      // Exponential backoff with cap ~20s
      nextDelay = Math.min(Math.floor(nextDelay * 1.5), 20000);
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
            this._logger.error('Error polling for prediction completion', error);
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

  // Enhanced error handling methods
  private _parseTrainingError(trainResult: any): {
    userMessage: string;
    category: string;
    isRetryable: boolean;
  } {
    const errorCode = trainResult?.error?.code;
    const errorMessage = trainResult?.error?.message || 'Failed to start model training';

    switch (errorCode) {
      case 'ReplicateAuthFailed':
      case 'Unauthorized':
        return {
          userMessage:
            'API authentication issue detected. Our team has been notified and is working on a fix. Please try again in a few minutes.',
          category: 'authentication',
          isRetryable: false,
        };

      case 'ReplicateConfigError':
        return {
          userMessage:
            'Service configuration needs attention. Our team has been notified. Please try again in a few minutes.',
          category: 'configuration',
          isRetryable: false,
        };

      case 'TrainingFailed':
        return {
          userMessage:
            'Training service is temporarily experiencing issues. Please try again in a few minutes, or contact support if the problem persists.',
          category: 'service_unavailable',
          isRetryable: true,
        };

      case 'InsufficientCredits':
        return {
          userMessage:
            'Insufficient credits for training. Please purchase more credits to continue.',
          category: 'billing',
          isRetryable: false,
        };

      case 'ModelAlreadyTrained':
        return {
          userMessage:
            "You already have a trained model available. We'll use your existing model for image generation.",
          category: 'business_logic',
          isRetryable: false,
        };

      case 'ServiceUnavailable':
      case 'ServerError':
        return {
          userMessage:
            'Training service is temporarily unavailable. Please try again in a few minutes.',
          category: 'service_unavailable',
          isRetryable: true,
        };

      default:
        return {
          userMessage:
            errorMessage.includes('API') ||
            errorMessage.includes('authentication') ||
            errorMessage.includes('token')
              ? 'Service authentication needs attention. Our team has been notified. Please try again in a few minutes.'
              : 'Training service encountered an issue. Please try again, or contact support if the problem persists.',
          category: 'unknown',
          isRetryable: true,
        };
    }
  }

  private _enhanceTrainingError(error: any): {
    userMessage: string;
    category: string;
    isRetryable: boolean;
  } {
    // Network/HTTP errors
    if (error.status === 401) {
      return {
        userMessage:
          'API authentication issue detected. Our team has been notified and is working on a fix. Please try again in a few minutes.',
        category: 'authentication',
        isRetryable: false,
      };
    }

    if (error.status === 403) {
      return {
        userMessage: 'Access permission issue detected. Please contact support for assistance.',
        category: 'authorization',
        isRetryable: false,
      };
    }

    if (error.status >= 500) {
      return {
        userMessage:
          'Training service is temporarily experiencing issues. Please try again in a few minutes.',
        category: 'server_error',
        isRetryable: true,
      };
    }

    if (error.status === 429) {
      return {
        userMessage: 'Service is busy. Please wait a moment and try again.',
        category: 'rate_limit',
        isRetryable: true,
      };
    }

    // Network connectivity issues
    if (error.name === 'NetworkError' || error.message?.includes('network')) {
      return {
        userMessage:
          'Network connection issue. Please check your internet connection and try again.',
        category: 'network',
        isRetryable: true,
      };
    }

    // Timeout errors
    if (error.name === 'TimeoutError' || error.message?.includes('timeout')) {
      return {
        userMessage: 'Request timed out. Please try again.',
        category: 'timeout',
        isRetryable: true,
      };
    }

    // Parse API-specific errors from response
    const apiErrorMessage = error.error?.message || error.message || 'Unknown error occurred';

    if (apiErrorMessage.includes('authentication') || apiErrorMessage.includes('token')) {
      return {
        userMessage:
          'API authentication issue detected. Our team has been notified and is working on a fix. Please try again in a few minutes.',
        category: 'authentication',
        isRetryable: false,
      };
    }

    if (apiErrorMessage.includes('configuration') || apiErrorMessage.includes('config')) {
      return {
        userMessage:
          'Service configuration needs attention. Our team has been notified. Please try again in a few minutes.',
        category: 'configuration',
        isRetryable: false,
      };
    }

    return {
      userMessage:
        'Training service encountered an issue. Please try again, or contact support if the problem persists.',
      category: 'unknown',
      isRetryable: true,
    };
  }

  private _categorizeWorkflowError(error: unknown): {
    title: string;
    message: string;
    category: string;
  } {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error occurred';
    const apiError = (error as any)?.error;

    // Authentication/Authorization errors
    if (
      errorMessage.includes('Authentication failed') ||
      errorMessage.includes('User not authenticated')
    ) {
      return {
        title: 'Authentication Issue',
        message: 'Your session has expired. Please refresh the page and try again.',
        category: 'authentication',
      };
    }

    // API/Service errors
    if (
      errorMessage.includes('API authentication issue') ||
      errorMessage.includes('authentication issue detected')
    ) {
      return {
        title: 'Service Issue',
        message:
          'Our training service needs attention. The team has been notified and is working on a fix. Please try again in a few minutes.',
        category: 'service_authentication',
      };
    }

    if (
      errorMessage.includes('Configuration error') ||
      errorMessage.includes('configuration needs attention')
    ) {
      return {
        title: 'Service Configuration',
        message:
          'Our service configuration needs updating. The team has been notified. Please try again in a few minutes.',
        category: 'configuration',
      };
    }

    // Training-specific errors
    if (errorMessage.includes('Failed to create training ZIP')) {
      return {
        title: 'Training Preparation Failed',
        message:
          'Unable to prepare your images for training. Please ensure you have uploaded at least 10 clear photos and try again.',
        category: 'training_preparation',
      };
    }

    if (errorMessage.includes('Failed to get training ZIP URL')) {
      return {
        title: 'Training Upload Failed',
        message:
          "Your images were prepared but couldn't be uploaded. Please try again in a few minutes.",
        category: 'training_upload',
      };
    }

    // Credit/billing errors
    if (errorMessage.includes('Insufficient credits')) {
      return {
        title: 'Insufficient Credits',
        message:
          "You don't have enough credits for training. Please purchase more credits to continue.",
        category: 'billing',
      };
    }

    // ENHANCED: Model status errors - handle both API error codes and message content
    if (
      apiError?.code === 'ModelAlreadyTrained' ||
      errorMessage.includes('ModelAlreadyTrained') ||
      errorMessage.includes('already have a trained model')
    ) {
      return {
        title: 'Model Already Available',
        message:
          "You already have a trained model available. We'll use your existing model for image generation.",
        category: 'business_logic',
      };
    }

    // Network/service unavailable
    if (
      errorMessage.includes('temporarily unavailable') ||
      errorMessage.includes('service is busy')
    ) {
      return {
        title: 'Service Temporarily Unavailable',
        message: 'Our training service is temporarily busy. Please try again in a few minutes.',
        category: 'service_unavailable',
      };
    }

    // Generic fallback with more helpful messaging
    return {
      title: 'Training Error',
      message:
        'We encountered an issue starting your training. Please try again, or contact support if the problem continues.',
      category: 'unknown',
    };
  }

  private _logErrorDiagnostics(error: unknown, category: string): void {
    const diagnostics = {
      category,
      timestamp: new Date().toISOString(),
      userAuthenticated: this._deps.authService.isAuthenticated(),
      hasToken: !!this._deps.authService.getToken(),
      currentState: this._deps.stateService.getState()?.modelStatus,
    };

    switch (category) {
      case 'authentication':
      case 'service_authentication':
        this._logger.error('Authentication diagnostics', {
          ...diagnostics,
          tokenExists: !!this._deps.authService.getToken(),
          tokenValid: this._deps.authService.isAuthenticated(),
          userId: this._deps.authService.getCurrentUserId(),
          suggestedAction: 'Check API token configuration and user session',
        });
        break;

      case 'configuration':
        this._logger.error('Configuration diagnostics', {
          ...diagnostics,
          configService: this._deps.config.constructor.name,
          suggestedAction: 'Verify Replicate API configuration and environment variables',
        });
        break;

      case 'service_unavailable':
      case 'server_error':
        this._logger.error('Service diagnostics', {
          ...diagnostics,
          suggestedAction: 'Check Replicate API status and network connectivity',
        });
        break;

      case 'training_preparation':
        this._logger.error('Training preparation diagnostics', {
          ...diagnostics,
          suggestedAction: 'Check file upload service and user uploaded images count',
        });
        break;

      case 'billing':
        this._logger.error('Billing diagnostics', {
          ...diagnostics,
          userCredits: this._getAvailableCredits(),
          suggestedAction: 'Check user credit balance and subscription status',
        });
        break;

      default:
        this._logger.error('General error diagnostics', {
          ...diagnostics,
          errorDetails: error,
          suggestedAction: 'Review full error context and network logs',
        });
        break;
    }
  }

  // Cleanup method
  pause(): void {
    this._clearAllIntervals();
  }

  dispose(): void {
    this.pause();
    this.resetProgress();
  }

  async queueBackgroundGeneration(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): Promise<boolean> {
    // Set flag synchronously before any await so the polling interval sees it immediately.
    // The flag may be reset below on early-exit or API failure.
    this._backgroundGenerationQueued = true;

    const trainingId = this.getProgress().trainingId;
    if (!trainingId || selectedStyles.length === 0) {
      this._backgroundGenerationQueued = false;
      return false;
    }

    const canonicalStyles = selectedStyles
      .map(style => this._toCanonicalStyleName(style.name))
      .filter((style): style is string => style.length > 0);

    if (canonicalStyles.length === 0) {
      this._backgroundGenerationQueued = false;
      return false;
    }

    try {
      const replicateService = await this._loadReplicateService();
      const result = await firstValueFrom(
        replicateService.queueGeneration(trainingId, canonicalStyles, imagesPerStyle)
      );
      if (!result || !result.success) {
        this._backgroundGenerationQueued = false;
        return false;
      } else {
        this._deps.notificationService.info(
          'Background Generation Queued',
          'We will start generating your selected styles as soon as training is ready.'
        );
        return true;
      }
    } catch (error) {
      this._backgroundGenerationQueued = false;
      this._logger.error('Failed to queue background generation', error);
      this._deps.notificationService.warning(
        'Could not queue generation',
        'Please stay on this page or retry to ensure generation starts after training.'
      );
      return false;
    }
  }

  private _toCanonicalStyleName(styleName: string | null | undefined): string {
    return (styleName || '').trim().toLowerCase();
  }
}
