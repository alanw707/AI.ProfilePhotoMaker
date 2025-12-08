/**
 * Unified Model Status Architecture
 *
 * This file defines the semantic model status interface that replaces string-based
 * status logic with type-safe capability flags and clear state management.
 *
 * Key Benefits:
 * - Type safety: Compile-time validation vs runtime string matching
 * - Semantic clarity: capability.canGenerate vs status === 'Model Ready'
 * - Maintainability: Single source of truth for status logic
 * - Performance: Computed capabilities cached in status object
 */

import { UnifiedModelStatusCode } from '../services/file-upload.service';
import { ModelCreationStatus } from './database.types';

/**
 * Core semantic model state - simplified from complex string variations
 */
export enum ModelState {
  NOT_STARTED = 'NOT_STARTED',
  READY_TO_TRAIN = 'READY_TO_TRAIN',
  TRAINING = 'TRAINING',
   GENERATING = 'GENERATING',
  READY = 'READY',
  FAILED = 'FAILED',
}

/**
 * Capability flags that replace complex string matching logic
 */
export interface ModelCapabilities {
  /** Can generate photos with this model */
  canGenerate: boolean;

  /** Can start training process */
  canTrain: boolean;

  /** Can cancel ongoing training */
  canCancel: boolean;

  /** Can retry after failure */
  canRetry: boolean;

  /** Can delete trained model */
  canDelete: boolean;
}

/**
 * Display information for UI components
 */
export interface ModelDisplayInfo {
  /** Primary status text shown to user */
  primaryText: string;

  /** Optional secondary/helper text */
  secondaryText?: string;

  /** Progress percentage for training/generation */
  progressPercent?: number;

  /** Estimated time remaining in minutes */
  estimatedTimeRemaining?: number;

  /** Icon identifier for UI */
  icon: StatusIcon;

  /** Color/style variant for UI */
  variant: StatusVariant;
}

/**
 * Metadata for debugging and API integration
 */
export interface ModelStatusMetadata {
  /** When this status was last updated */
  lastUpdated: Date;

  /** Source of this status information */
  source: 'database' | 'api' | 'computed' | 'cache';

  /** Legacy status string for migration compatibility */
  legacyStatus?: string;

  /** Original API status code */
  apiStatusCode?: UnifiedModelStatusCode;

  /** Original database status */
  databaseStatus?: ModelCreationStatus;

  /** Additional debug information */
  debug?: {
    hasTrainedModel: boolean;
    uploadedImages: number;
    errorMessage?: string;
  };
}

/**
 * Complete unified model status interface
 */
export interface ModelStatus {
  /** Core semantic state */
  state: ModelState;

  /** What operations are possible */
  capabilities: ModelCapabilities;

  /** Information for UI display */
  display: ModelDisplayInfo;

  /** Metadata and debugging info */
  metadata: ModelStatusMetadata;
}

/**
 * Icon types for status display
 */
export enum StatusIcon {
  UPLOAD = 'upload',
  TRAINING = 'training',
  READY = 'ready',
  GENERATING = 'generating',
  ERROR = 'error',
  WARNING = 'warning',
}

/**
 * UI variants for styling status display
 */
export enum StatusVariant {
  DEFAULT = 'default',
  SUCCESS = 'success',
  WARNING = 'warning',
  ERROR = 'error',
  INFO = 'info',
  LOADING = 'loading',
}

/**
 * Configuration for status behavior
 */
export interface ModelStatusConfig {
  /** Minimum images required for training */
  minImagesForTraining: number;

  /** Whether to show detailed progress information */
  showDetailedProgress: boolean;

  /** Whether to auto-refresh status */
  autoRefresh: boolean;

  /** Refresh interval in milliseconds */
  refreshInterval: number;
}

/**
 * Default configuration values
 */
export const DEFAULT_MODEL_STATUS_CONFIG: ModelStatusConfig = {
  minImagesForTraining: 10,
  showDetailedProgress: true,
  autoRefresh: true,
  refreshInterval: 30000, // 30 seconds
};

/**
 * Helper type for migration compatibility
 */
export interface ModelStatusAdapter {
  /** New semantic interface */
  status: ModelStatus;

  /** Legacy string for backward compatibility */
  legacyStatus: string;

  /** Helper methods for migration */
  canGenerate(): boolean;
  canTrain(): boolean;
  getDisplayText(): string;
  isTraining(): boolean;
  needsImages(): boolean;
}

/**
 * Status transition events for monitoring and debugging
 */
export interface ModelStatusTransition {
  /** Previous state */
  from: ModelState | null;

  /** New state */
  to: ModelState;

  /** When transition occurred */
  timestamp: Date;

  /** Reason for transition */
  reason: string;

  /** Source that triggered the transition */
  source: 'user' | 'system' | 'api' | 'timer';
}

/**
 * Type guards for status checking
 */
export class ModelStatusGuards {
  static isNotStarted(status: ModelStatus): boolean {
    return status.state === ModelState.NOT_STARTED;
  }

  static isReadyToTrain(status: ModelStatus): boolean {
    return status.state === ModelState.READY_TO_TRAIN;
  }

  static isTraining(status: ModelStatus): boolean {
    return status.state === ModelState.TRAINING;
  }

  static isGenerating(status: ModelStatus): boolean {
    return status.state === ModelState.GENERATING;
  }

  static isReady(status: ModelStatus): boolean {
    return status.state === ModelState.READY;
  }

  static isFailed(status: ModelStatus): boolean {
    return status.state === ModelState.FAILED;
  }

  static canGenerate(status: ModelStatus): boolean {
    return status.capabilities.canGenerate && status.state === ModelState.READY;
  }

  static canTrain(status: ModelStatus): boolean {
    return (
      status.capabilities.canTrain &&
      status.state !== ModelState.TRAINING &&
      status.state !== ModelState.GENERATING
    );
  }

  static needsImages(status: ModelStatus): boolean {
    return (
      status.state === ModelState.NOT_STARTED &&
      (status.display.primaryText.includes('images') ||
        status.display.primaryText.includes('upload'))
    );
  }
}
