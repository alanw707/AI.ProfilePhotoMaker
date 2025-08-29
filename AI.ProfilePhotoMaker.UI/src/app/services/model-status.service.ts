import { Injectable } from '@angular/core';
import { UnifiedModelStatusCode } from './file-upload.service';
import { ModelStatus, ModelStatusHelper } from '../models/dashboard.types';

/**
 * Centralized model status validation service
 * Eliminates scattered string matching and defensive status arrays
 */
@Injectable({
  providedIn: 'root',
})
export class ModelStatusService {
  /**
   * Check if model can generate photos
   * Replaces: ['Model Ready', 'Ready', 'Done', 'Completed', 'ModelReady'].some(status => ...)
   */
  canGenerate(status: string | UnifiedModelStatusCode): boolean {
    if (!status) {
      return false;
    }

    // Unified status codes (preferred)
    if (status === 'ModelReady') {
      return true;
    }

    // Legacy display strings (transitional support)
    const normalizedStatus = status.trim();
    // Tight legacy handling: allow only explicit known-good readiness phrases
    if (normalizedStatus === 'Model Ready' || normalizedStatus === 'ModelReady') {
      return true;
    }
    const inferredReady =
      normalizedStatus.toLowerCase().includes('ready for generation') ||
      normalizedStatus.startsWith('Model trained');
    if (inferredReady) {
      // Lightweight diagnostic to observe legacy path usage in prod
      try {
        console.debug('canGenerate: legacy readiness match', normalizedStatus);
      } catch {}
      return true;
    }
    return false;
  }

  /**
   * Check if model is currently training
   */
  isTraining(status: string | UnifiedModelStatusCode): boolean {
    if (!status) {
      return false;
    }

    // Unified status codes (preferred)
    if (status === 'Training') {
      return true;
    }

    // Legacy display strings (transitional support)
    const normalizedStatus = status.toLowerCase().trim();
    return (
      normalizedStatus === 'training' ||
      normalizedStatus === 'creating' ||
      normalizedStatus === 'pending' ||
      normalizedStatus.includes('starting training')
    );
  }

  /**
   * Check if ready for training (has images but no trained model)
   */
  canStartTraining(status: string | UnifiedModelStatusCode): boolean {
    if (!status) {
      return false;
    }

    // Unified status codes (preferred)
    if (status === 'ReadyForTraining') {
      return true;
    }

    // Legacy display strings (transitional support)
    const normalizedStatus = status.trim();
    return (
      normalizedStatus.startsWith('Ready for training') ||
      normalizedStatus.startsWith('Need at least') ||
      normalizedStatus === 'Not Started'
    );
  }

  /**
   * Check if training failed
   */
  hasFailed(status: string | UnifiedModelStatusCode): boolean {
    if (!status) {
      return false;
    }

    // Unified status codes (preferred)
    if (status === 'Failed') {
      return true;
    }

    // Legacy display strings (transitional support)
    const normalizedStatus = status.toLowerCase().trim();
    return normalizedStatus.includes('failed') || normalizedStatus.includes('error');
  }

  /**
   * Check if model is not started
   */
  isNotStarted(status: string | UnifiedModelStatusCode): boolean {
    if (!status) {
      return true;
    }

    // Unified status codes (preferred)
    if (status === 'NotStarted') {
      return true;
    }

    // Legacy display strings (transitional support)
    const normalizedStatus = status.trim();
    return normalizedStatus === 'Not Started' || normalizedStatus.startsWith('No images uploaded');
  }

  /**
   * Get semantic status information
   * Replaces complex status translation logic
   */
  getStatusInfo(status: string | UnifiedModelStatusCode): {
    canGenerate: boolean;
    isTraining: boolean;
    canStartTraining: boolean;
    hasFailed: boolean;
    isNotStarted: boolean;
    displayText: string;
    actionable: boolean;
  } {
    return {
      canGenerate: this.canGenerate(status),
      isTraining: this.isTraining(status),
      canStartTraining: this.canStartTraining(status),
      hasFailed: this.hasFailed(status),
      isNotStarted: this.isNotStarted(status),
      displayText: this.getDisplayText(status),
      actionable: this.canGenerate(status) || this.canStartTraining(status),
    };
  }

  /**
   * Get semantic model status from legacy string status
   * This is the new unified approach that replaces all string matching
   */
  getSemanticStatus(
    legacyStatus: string | UnifiedModelStatusCode,
    progress?: number,
    error?: string
  ): ModelStatus {
    return ModelStatusHelper.fromLegacyStatus(legacyStatus?.toString() || '', progress, error);
  }

  /**
   * Enhanced semantic status methods using the new interface
   */
  canGenerateSemantic(status: ModelStatus): boolean {
    return ModelStatusHelper.canGenerate(status);
  }

  canTrainSemantic(status: ModelStatus): boolean {
    return ModelStatusHelper.canStartTraining(status);
  }

  isTrainingSemantic(status: ModelStatus): boolean {
    return ModelStatusHelper.isTraining(status);
  }

  needsImagesSemantic(status: ModelStatus): boolean {
    return ModelStatusHelper.needsImages(status);
  }

  /**
   * Get user-friendly display text
   * Centralizes display logic
   */
  private getDisplayText(status: string | UnifiedModelStatusCode): string {
    if (!status) {
      return 'Not Started';
    }

    // For unified status codes, convert to display text
    if (typeof status === 'string' && status.length <= 20 && !status.includes(' ')) {
      switch (status as UnifiedModelStatusCode) {
        case 'ModelReady':
          return 'Model Ready';
        case 'Training':
          return 'Training...';
        case 'ReadyForTraining':
          return 'Ready for Training';
        case 'Failed':
          return 'Training Failed';
        case 'NotStarted':
          return 'Not Started';
      }
    }

    // Return display strings as-is (already user-friendly)
    return status.toString().trim();
  }
}
