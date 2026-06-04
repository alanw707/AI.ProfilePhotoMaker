import { Injectable } from '@angular/core';
import {
  ModelStatus,
  ModelState,
  ModelCapabilities,
  ModelDisplayInfo,
  StatusIcon,
  StatusVariant,
  ModelStatusAdapter,
  DEFAULT_MODEL_STATUS_CONFIG,
} from '../models/model-status.types';
import {
  GenerationStatusResponse,
  UnifiedModelStatusCode,
  UnifiedModelStatusResponse,
} from './file-upload.service';
import { ModelCreationStatus } from '../models/database.types';
import { LoggingService } from './logging.service';

/**
 * Service responsible for mapping between different status representations
 * and creating the unified ModelStatus interface.
 *
 * This service centralizes all status conversion logic and ensures consistency
 * across the application.
 */
@Injectable({
  providedIn: 'root',
})
export class ModelStatusMapperService {
  private legacyUsageLogged = false;

  constructor(private readonly loggingService: LoggingService) {}

  private logLegacyUsage(origin: string, metadata?: Record<string, unknown>): void {
    const payload = metadata ? { origin, ...metadata } : { origin };

    if (!this.legacyUsageLogged) {
      this.loggingService.warn('Legacy model status bridge invoked', payload);
      this.legacyUsageLogged = true;
    } else {
      this.loggingService.debug(`Legacy model status bridge invoked via ${origin}`, payload);
    }
  }

  /**
   * Create ModelStatus from API response (primary method)
   */
  fromApiResponse(response: UnifiedModelStatusResponse): ModelStatus {
    const generationInProgress =
      this.isGenerationInProgress(response.generationStatus) ||
      response.statusCode === 'Generating';
    const generationFailed = this.isGenerationFailed(response.generationStatus);
    const stateFromApi = this.mapApiCodeToState(response.statusCode);
    const state = generationInProgress ? ModelState.GENERATING : stateFromApi;

    return {
      state,
      capabilities: this.computeCapabilitiesFromApi(
        response,
        generationFailed,
        generationInProgress
      ),
      display: this.createDisplayInfoFromApi(response, generationFailed, generationInProgress),
      metadata: {
        lastUpdated: response.lastUpdated ? new Date(response.lastUpdated) : new Date(),
        source: 'api',
        apiStatusCode: response.statusCode,
        legacyStatus: this.mapApiCodeToLegacyString(response.statusCode),
        debug: {
          hasTrainedModel: response.hasTrainedModel,
          uploadedImages: response.totalUploadedImages,
          errorMessage: response.reason || undefined,
        },
      },
    };
  }

  /**
   * Create ModelStatus from database status (fallback method)
   */
  fromDatabaseStatus(
    dbStatus: ModelCreationStatus,
    hasTrainedModel: boolean,
    uploadedImages: number,
    errorMessage?: string
  ): ModelStatus {
    const state = this.mapDatabaseStatusToState(dbStatus, hasTrainedModel, uploadedImages);

    return {
      state,
      capabilities: this.computeCapabilitiesFromState(state, uploadedImages, hasTrainedModel),
      display: this.createDisplayInfoFromState(state, uploadedImages, errorMessage),
      metadata: {
        lastUpdated: new Date(),
        source: 'database',
        databaseStatus: dbStatus,
        debug: {
          hasTrainedModel,
          uploadedImages,
          errorMessage,
        },
      },
    };
  }

  /**
   * Create ModelStatus from legacy string (migration support)
   */
  fromLegacyStatus(
    legacyStatus: string,
    uploadedImages = 0,
    hasTrainedModel = false,
    progress?: number,
    error?: string
  ): ModelStatus {
    this.logLegacyUsage('fromLegacyStatus', {
      legacyStatus,
      uploadedImages,
      hasTrainedModel,
    });

    const state = this.mapLegacyStringToState(legacyStatus);

    return {
      state,
      capabilities: this.computeCapabilitiesFromState(state, uploadedImages, hasTrainedModel),
      display: this.createDisplayInfoFromLegacy(legacyStatus, progress, error),
      metadata: {
        lastUpdated: new Date(),
        source: 'computed',
        legacyStatus,
        debug: {
          hasTrainedModel,
          uploadedImages,
          errorMessage: error,
        },
      },
    };
  }

  /**
   * Create adapter for gradual migration (supports both old and new)
   */
  createAdapter(status: ModelStatus): ModelStatusAdapter {
    if (status.metadata?.legacyStatus) {
      this.logLegacyUsage('createAdapter', {
        legacyStatus: status.metadata.legacyStatus,
        source: status.metadata.source,
      });
    }

    return {
      status,
      legacyStatus: status.metadata.legacyStatus || this.mapStateToLegacyString(status.state),

      canGenerate(): boolean {
        return status.capabilities.canGenerate;
      },

      canTrain(): boolean {
        return status.capabilities.canTrain;
      },

      getDisplayText(): string {
        return status.display.primaryText;
      },

      isTraining(): boolean {
        return status.state === ModelState.TRAINING;
      },

      needsImages(): boolean {
        return (
          status.state === ModelState.NOT_STARTED &&
          (status.display.primaryText.includes('images') ||
            status.display.primaryText.includes('upload'))
        );
      },
    };
  }

  // ===============================
  // Private Mapping Methods
  // ===============================

  private mapApiCodeToState(code: UnifiedModelStatusCode): ModelState {
    switch (code) {
      case 'NotStarted':
        return ModelState.NOT_STARTED;
      case 'ReadyForTraining':
        return ModelState.READY_TO_TRAIN;
      case 'Training':
        return ModelState.TRAINING;
      case 'Generating':
        return ModelState.GENERATING;
      case 'ModelReady':
        return ModelState.READY;
      case 'Failed':
        return ModelState.FAILED;
      default:
        console.warn(`Unknown API status code: ${code}`);
        return ModelState.NOT_STARTED;
    }
  }

  private mapDatabaseStatusToState(
    dbStatus: ModelCreationStatus,
    hasTrainedModel: boolean,
    uploadedImages: number
  ): ModelState {
    switch (dbStatus) {
      case ModelCreationStatus.PENDING:
      case ModelCreationStatus.CREATING:
        return ModelState.TRAINING;
      case ModelCreationStatus.READY:
        return hasTrainedModel ? ModelState.READY : ModelState.READY_TO_TRAIN;
      case ModelCreationStatus.FAILED:
        return ModelState.FAILED;
      default:
        // Determine initial state based on uploaded images
        return uploadedImages >= DEFAULT_MODEL_STATUS_CONFIG.minImagesForTraining
          ? ModelState.READY_TO_TRAIN
          : ModelState.NOT_STARTED;
    }
  }

  private mapLegacyStringToState(legacyStatus: string): ModelState {
    const status = legacyStatus?.toLowerCase().trim() || '';

    // Ready states
    if (['model ready', 'ready', 'done', 'completed', 'modelready'].includes(status)) {
      return ModelState.READY;
    }

    // Training states
    if (
      ['training', 'creating', 'pending', 'processing', 'starting'].includes(status) ||
      status.includes('training') ||
      status.includes('processing')
    ) {
      return ModelState.TRAINING;
    }

    // Ready to train states
    if (status.includes('ready for training')) {
      return ModelState.READY_TO_TRAIN;
    }

    // Failed states
    if (status.includes('failed') || status.includes('error')) {
      return ModelState.FAILED;
    }

    // Default to not started
    return ModelState.NOT_STARTED;
  }

  private computeCapabilitiesFromApi(
    response: UnifiedModelStatusResponse,
    generationFailed: boolean,
    generationInProgress: boolean
  ): ModelCapabilities {
    return {
      canGenerate:
        response.hasTrainedModel && response.statusCode === 'ModelReady' && !generationInProgress,
      canTrain:
        response.canStartTraining && response.statusCode !== 'Training' && !generationInProgress,
      canCancel:
        (response.statusCode === 'Training' && !!response.currentRequest) || generationInProgress,
      canRetry: response.statusCode === 'Failed' || generationFailed,
      canDelete: response.hasTrainedModel,
    };
  }

  private computeCapabilitiesFromState(
    state: ModelState,
    uploadedImages: number,
    hasTrainedModel: boolean
  ): ModelCapabilities {
    const minImages = DEFAULT_MODEL_STATUS_CONFIG.minImagesForTraining;

    return {
      canGenerate: state === ModelState.READY && hasTrainedModel,
      canTrain:
        (state === ModelState.READY_TO_TRAIN || state === ModelState.FAILED) &&
        uploadedImages >= minImages,
      canCancel: state === ModelState.TRAINING || state === ModelState.GENERATING,
      canRetry: state === ModelState.FAILED,
      canDelete: hasTrainedModel,
    };
  }

  private createDisplayInfoFromApi(
    response: UnifiedModelStatusResponse,
    generationFailed: boolean,
    generationInProgress: boolean
  ): ModelDisplayInfo {
    if (generationInProgress) {
      return {
        primaryText: 'Generating images',
        secondaryText: 'Running in background',
        icon: StatusIcon.GENERATING,
        variant: StatusVariant.LOADING,
      };
    }

    if (generationFailed) {
      return {
        primaryText: 'Generation failed',
        secondaryText: response.generationStatus?.error || 'Try again or contact support',
        icon: StatusIcon.ERROR,
        variant: StatusVariant.ERROR,
      };
    }

    switch (response.statusCode) {
      case 'NotStarted':
        return {
          primaryText: response.reason || this.getImageUploadPrompt(response.totalUploadedImages),
          secondaryText:
            response.totalUploadedImages > 0
              ? `${response.totalUploadedImages} of ${DEFAULT_MODEL_STATUS_CONFIG.minImagesForTraining} images`
              : undefined,
          icon: StatusIcon.UPLOAD,
          variant: StatusVariant.DEFAULT,
        };

      case 'ReadyForTraining':
        return {
          primaryText: 'Ready for training',
          secondaryText: `${response.totalUploadedImages} images uploaded`,
          icon: StatusIcon.READY,
          variant: StatusVariant.INFO,
        };

      case 'Training':
        return {
          primaryText: 'Training in progress',
          secondaryText: 'Advanced custom model processing is running',
          progressPercent: this.estimateTrainingProgress(response.currentRequest),
          estimatedTimeRemaining: this.estimateTimeRemaining(response.currentRequest),
          icon: StatusIcon.TRAINING,
          variant: StatusVariant.LOADING,
        };

      case 'ModelReady':
        return {
          primaryText: 'Model ready',
          secondaryText: 'Generate photos now',
          icon: StatusIcon.READY,
          variant: StatusVariant.SUCCESS,
        };

      case 'Failed':
        return {
          primaryText: 'Training failed',
          secondaryText: response.reason || 'Try again or contact support',
          icon: StatusIcon.ERROR,
          variant: StatusVariant.ERROR,
        };

      default:
        return {
          primaryText: 'Status unknown',
          secondaryText: 'Please refresh the page',
          icon: StatusIcon.WARNING,
          variant: StatusVariant.WARNING,
        };
    }
  }

  private createDisplayInfoFromState(
    state: ModelState,
    uploadedImages: number,
    errorMessage?: string
  ): ModelDisplayInfo {
    switch (state) {
      case ModelState.NOT_STARTED:
        return {
          primaryText: this.getImageUploadPrompt(uploadedImages),
          icon: StatusIcon.UPLOAD,
          variant: StatusVariant.DEFAULT,
        };

      case ModelState.READY_TO_TRAIN:
        return {
          primaryText: 'Ready for training',
          secondaryText: `${uploadedImages} images uploaded`,
          icon: StatusIcon.READY,
          variant: StatusVariant.INFO,
        };

      case ModelState.TRAINING:
        return {
          primaryText: 'Training in progress',
          secondaryText: 'Advanced custom model processing is running',
          icon: StatusIcon.TRAINING,
          variant: StatusVariant.LOADING,
        };

      case ModelState.GENERATING:
        return {
          primaryText: 'Generating images',
          secondaryText: 'Running in background',
          icon: StatusIcon.GENERATING,
          variant: StatusVariant.LOADING,
        };

      case ModelState.READY:
        return {
          primaryText: 'Model ready',
          secondaryText: 'Generate photos now',
          icon: StatusIcon.READY,
          variant: StatusVariant.SUCCESS,
        };

      case ModelState.FAILED:
        return {
          primaryText: 'Training failed',
          secondaryText: errorMessage || 'Try again or contact support',
          icon: StatusIcon.ERROR,
          variant: StatusVariant.ERROR,
        };

      default:
        return {
          primaryText: 'Status unknown',
          icon: StatusIcon.WARNING,
          variant: StatusVariant.WARNING,
        };
    }
  }

  private createDisplayInfoFromLegacy(
    legacyStatus: string,
    progress?: number,
    error?: string
  ): ModelDisplayInfo {
    const status = legacyStatus?.trim() || '';

    // Use the existing logic but with standardized display info
    if (
      status === 'Model Ready' ||
      status === 'Ready' ||
      status === 'Done' ||
      status === 'Completed'
    ) {
      return {
        primaryText: 'Model ready',
        secondaryText: 'Generate photos now',
        icon: StatusIcon.READY,
        variant: StatusVariant.SUCCESS,
      };
    }

    if (status.toLowerCase().includes('training') || status.toLowerCase().includes('processing')) {
      return {
        primaryText: 'Training in progress',
        secondaryText: 'Advanced custom model processing is running',
        progressPercent: progress,
        icon: StatusIcon.TRAINING,
        variant: StatusVariant.LOADING,
      };
    }

    if (status.includes('Ready for training')) {
      return {
        primaryText: 'Ready for training',
        icon: StatusIcon.READY,
        variant: StatusVariant.INFO,
      };
    }

    if (status.toLowerCase().includes('failed') || error) {
      return {
        primaryText: 'Training failed',
        secondaryText: error || 'Try again or contact support',
        icon: StatusIcon.ERROR,
        variant: StatusVariant.ERROR,
      };
    }

    // Default/not started
    return {
      primaryText: status || 'Upload images to begin',
      icon: StatusIcon.UPLOAD,
      variant: StatusVariant.DEFAULT,
    };
  }

  // ===============================
  // Utility Methods
  // ===============================

  private getImageUploadPrompt(uploadedImages: number): string {
    const minImages = DEFAULT_MODEL_STATUS_CONFIG.minImagesForTraining;

    if (uploadedImages === 0) {
      return 'Upload images to begin';
    } else if (uploadedImages < minImages) {
      return `Need ${minImages - uploadedImages} more images`;
    } else {
      return 'Ready for training';
    }
  }

  private estimateTrainingProgress(currentRequest: any): number {
    if (!currentRequest) {
      return 25;
    }

    const status = currentRequest.status?.toLowerCase();
    if (status === 'pending') {
      return 25;
    }
    if (status === 'creating') {
      return 50;
    }
    return 75;
  }

  private estimateTimeRemaining(currentRequest: any): number | undefined {
    if (!currentRequest?.createdAt) {
      return undefined;
    }

    const startTime = new Date(currentRequest.createdAt).getTime();
    const elapsed = (Date.now() - startTime) / 1000 / 60; // minutes
    const typical = 12; // typical training time in minutes

    return Math.max(0, typical - elapsed);
  }

  private mapApiCodeToLegacyString(code: UnifiedModelStatusCode): string {
    switch (code) {
      case 'NotStarted':
        return 'Not Started';
      case 'ReadyForTraining':
        return 'Ready for training';
      case 'Training':
        return 'training';
      case 'Generating':
        return 'Generating';
      case 'ModelReady':
        return 'Model Ready';
      case 'Failed':
        return 'Training failed';
      default:
        return 'Not Started';
    }
  }

  private mapStateToLegacyString(state: ModelState): string {
    switch (state) {
      case ModelState.NOT_STARTED:
        return 'Not Started';
      case ModelState.READY_TO_TRAIN:
        return 'Ready for training';
      case ModelState.TRAINING:
        return 'training';
      case ModelState.GENERATING:
        return 'Generation in progress';
      case ModelState.READY:
        return 'Model Ready';
      case ModelState.FAILED:
        return 'Training failed';
      default:
        return 'Not Started';
    }
  }

  private isGenerationInProgress(generation?: GenerationStatusResponse | null): boolean {
    if (!generation?.status) {
      return false;
    }
    return generation.status === 'queued' || generation.status === 'processing';
  }

  private isGenerationFailed(generation?: GenerationStatusResponse | null): boolean {
    if (!generation?.status) {
      return false;
    }
    return generation.status === 'failed' || generation.status === 'canceled';
  }
}
