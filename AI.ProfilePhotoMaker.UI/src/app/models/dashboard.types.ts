import { FaceValidationResult, QualityScore } from '../interfaces/service.interfaces';

export interface GeneratedPhoto {
  id: string;
  url: string;
  style: string;
  createdAt: Date;
}

export interface QualityCheckError {
  fileName: string;
  file: File;
  errors: string[];
  warnings?: string[];
  faceValidation?: FaceValidationResult;
  qualityScore?: QualityScore;
  showErrorDetails?: boolean;
}

export interface SelectedFileWithQuality {
  file: File;
  qualityScore?: QualityScore;
  faceValidation?: FaceValidationResult;
  errors: string[];
  warnings: string[];
  isValid: boolean;
  showDetails?: boolean; // For expandable details UI state
}

export interface QualityCheckResult {
  validFiles: File[];
  invalidFiles: File[];
  errors: QualityCheckError[];
  totalProcessed: number;
}

export interface UploadProgress {
  percentage: number;
  currentFile?: string;
  totalFiles?: number;
  completed?: boolean;
  error?: string;
}

export interface TrainingStatus {
  isTraining: boolean;
  progress: number;
  status: string;
  modelId?: string;
  error?: string;
  estimatedTimeRemaining?: number;
}

export interface GenerationStatus {
  isGenerating: boolean;
  progress: number;
  status: string;
  estimatedTimeRemaining?: number;
  completedImages?: number;
  totalImages?: number;
  error?: string;
}

// Semantic Model Status Interface
export interface ModelStatus {
  state: 'NOT_STARTED' | 'READY_TO_TRAIN' | 'TRAINING' | 'READY' | 'FAILED';
  canGenerate: boolean;
  canTrain: boolean;
  displayText: string;
  progress?: number;
  error?: string;
  // Legacy support during transition
  legacyStatus?: string;
}

// Helper functions for ModelStatus
// DEPRECATED: Use ModelStatusService for new code - provides better semantic validation
export class ModelStatusHelper {
  static fromLegacyStatus(legacyStatus: string, progress?: number, error?: string): ModelStatus {
    const status = legacyStatus?.trim() || '';

    // DEPRECATED: Use ModelStatusService.canGenerate() instead
    // Ready states
    if (
      status === 'Model Ready' ||
      status === 'Ready' ||
      status === 'Done' ||
      status === 'Completed' ||
      status === 'ModelReady'
    ) {
      return {
        state: 'READY',
        canGenerate: true,
        canTrain: false,
        displayText: 'Model Ready',
        legacyStatus: status,
      };
    }

    // Training states
    if (
      ['Training', 'training', 'creating', 'pending', 'processing', 'starting'].some(s =>
        status.toLowerCase().includes(s.toLowerCase())
      )
    ) {
      return {
        state: 'TRAINING',
        canGenerate: false,
        canTrain: false,
        displayText: 'Training in progress...',
        progress,
        legacyStatus: status,
      };
    }

    // Ready to train states
    if (status.startsWith('Ready for training') || status === 'Ready for training') {
      return {
        state: 'READY_TO_TRAIN',
        canGenerate: false,
        canTrain: true,
        displayText: 'Ready for training',
        legacyStatus: status,
      };
    }

    // Upload prompts and initial states
    if (
      status.startsWith('No images uploaded') ||
      status.startsWith('Need at least') ||
      status === 'Not Started' ||
      status === 'Loading...'
    ) {
      return {
        state: 'NOT_STARTED',
        canGenerate: false,
        canTrain: false,
        displayText: status || 'Not Started',
        legacyStatus: status,
      };
    }

    // Failed states
    if (status.toLowerCase().includes('failed') || status.toLowerCase().includes('error')) {
      return {
        state: 'FAILED',
        canGenerate: false,
        canTrain: true,
        displayText: 'Training failed',
        error,
        legacyStatus: status,
      };
    }

    // Fallback
    return {
      state: 'NOT_STARTED',
      canGenerate: false,
      canTrain: false,
      displayText: status || 'Not Started',
      legacyStatus: status,
    };
  }

  static canStartTraining(status: ModelStatus): boolean {
    return status.canTrain && status.state !== 'TRAINING';
  }

  static canGenerate(status: ModelStatus): boolean {
    return status.canGenerate && status.state === 'READY';
  }

  static isTraining(status: ModelStatus): boolean {
    return status.state === 'TRAINING';
  }

  static needsImages(status: ModelStatus): boolean {
    return (
      status.state === 'NOT_STARTED' &&
      (status.displayText.includes('No images') || status.displayText.includes('Need at least'))
    );
  }
}
