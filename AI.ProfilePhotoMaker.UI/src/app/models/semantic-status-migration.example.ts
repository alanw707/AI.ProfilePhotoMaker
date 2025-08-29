/**
 * MIGRATION EXAMPLE: Frontend Status Handling Migration
 * From string matching to semantic status interface
 *
 * This file demonstrates how to migrate from complex string-based status checking
 * to clean semantic status handling.
 */

import { ModelStatus, ModelStatusHelper } from './dashboard.types';

// ========================================
// BEFORE: Complex String-Based Logic
// ========================================

class LegacyStatusHandler {
  // 🚨 PROBLEMATIC: Complex string matching with defensive arrays
  private static readonly READY_STATUSES = [
    'Model Ready',
    'Ready',
    'Done',
    'Completed',
    'ModelReady',
  ];

  private static readonly TRAINING_STATUSES = [
    'Training',
    'training',
    'creating',
    'pending',
    'processing',
    'starting',
  ];

  // 🚨 PROBLEMATIC: 9+ status variations causing routing bugs
  checkForExistingTrainedModel(modelStatus: string): boolean {
    // Complex string matching logic
    if (
      this.READY_STATUSES.some(status => modelStatus === status || modelStatus?.includes(status))
    ) {
      return true;
    }

    const trainingInProgress = this.TRAINING_STATUSES.some(status =>
      modelStatus?.toLowerCase().includes(status.toLowerCase())
    );

    if (trainingInProgress) {
      return false;
    }

    return false;
  }

  // 🚨 PROBLEMATIC: Scattered status validation logic
  canStartTraining(modelStatus: string): boolean {
    return (
      modelStatus.startsWith('Ready for training') ||
      modelStatus.startsWith('Need at least') ||
      modelStatus === 'Not Started'
    );
  }

  // 🚨 PROBLEMATIC: Credit calculation with string comparisons
  calculateTrainingCredits(modelStatus: string): number {
    if (modelStatus === 'Model Ready') {
      return 0;
    }
    return 15;
  }
}

// ========================================
// AFTER: Clean Semantic Status Logic
// ========================================

class SemanticStatusHandler {
  // ✅ CLEAN: Single method to get semantic status
  private getSemanticStatus(legacyStatus: string): ModelStatus {
    return ModelStatusHelper.fromLegacyStatus(legacyStatus);
  }

  // ✅ CLEAN: Simple capability-based routing
  checkForExistingTrainedModel(modelStatus: string): boolean {
    const semantic = this.getSemanticStatus(modelStatus);
    return semantic.canGenerate;
  }

  // ✅ CLEAN: Unified capability checking
  canStartTraining(modelStatus: string): boolean {
    const semantic = this.getSemanticStatus(modelStatus);
    return semantic.canTrain && semantic.state !== 'TRAINING';
  }

  // ✅ CLEAN: Logic based on capabilities, not strings
  calculateTrainingCredits(modelStatus: string): number {
    const semantic = this.getSemanticStatus(modelStatus);
    return semantic.canGenerate ? 0 : 15;
  }

  // ✅ CLEAN: Type-safe state checking
  isTraining(modelStatus: string): boolean {
    const semantic = this.getSemanticStatus(modelStatus);
    return semantic.state === 'TRAINING';
  }

  // ✅ CLEAN: Consistent display text
  getDisplayText(modelStatus: string): string {
    const semantic = this.getSemanticStatus(modelStatus);
    return semantic.displayText;
  }
}

// ========================================
// MIGRATION BENEFITS DEMONSTRATION
// ========================================

export class MigrationBenefitsDemo {
  static demonstrateBenefits() {
    const testStatuses = [
      'Model Ready',
      'Training',
      'Ready for training',
      'Not Started',
      'Need at least 10 images',
      'Training failed',
    ];

    const legacy = new LegacyStatusHandler();
    const semantic = new SemanticStatusHandler();

    console.log('=== MIGRATION COMPARISON ===');

    testStatuses.forEach(status => {
      const semanticStatus = ModelStatusHelper.fromLegacyStatus(status);

      console.log(`\nStatus: "${status}"`);
      console.log(`  Legacy canGenerate: ${legacy.checkForExistingTrainedModel(status)}`);
      console.log(`  Semantic canGenerate: ${semanticStatus.canGenerate}`);
      console.log(`  Semantic state: ${semanticStatus.state}`);
      console.log(`  Semantic display: ${semanticStatus.displayText}`);
    });
  }

  // Template usage examples
  static getTemplateExamples() {
    return {
      // BEFORE: Complex ngIf conditions
      legacyTemplate: `
        <!-- 🚨 PROBLEMATIC: String matching in templates -->
        <div *ngIf="modelStatus === 'Model Ready' || modelStatus === 'Ready' || modelStatus === 'Done'">
          <button>Generate Images</button>
        </div>
        
        <div *ngIf="modelStatus.startsWith('Ready for training') || modelStatus === 'Not Started'">
          <button>Start Training</button>
        </div>
      `,

      // AFTER: Clean semantic properties
      semanticTemplate: `
        <!-- ✅ CLEAN: Semantic capability checks -->
        <div *ngIf="canGenerateImages">
          <button>Generate Images</button>
        </div>
        
        <div *ngIf="canStartTraining">
          <button>Start Training</button>
        </div>
        
        <div class="status-display">{{ modelDisplayText }}</div>
      `,
    };
  }
}

// ========================================
// COMPONENT MIGRATION PATTERN
// ========================================

export class ComponentMigrationPattern {
  // ✅ Component properties using semantic status
  get canStartTraining(): boolean {
    const semantic = this.getSemanticStatus();
    return semantic?.canTrain ?? false;
  }

  get canGenerateImages(): boolean {
    const semantic = this.getSemanticStatus();
    return semantic?.canGenerate ?? false;
  }

  get isModelTraining(): boolean {
    const semantic = this.getSemanticStatus();
    return semantic ? semantic.state === 'TRAINING' : false;
  }

  get modelDisplayText(): string {
    const semantic = this.getSemanticStatus();
    return semantic?.displayText ?? 'Loading...';
  }

  private getSemanticStatus(): ModelStatus | undefined {
    // In real implementation, get from DashboardStateService
    // return this.dashboardStateService.getState().modelStatusSemantic;
    return undefined;
  }
}
