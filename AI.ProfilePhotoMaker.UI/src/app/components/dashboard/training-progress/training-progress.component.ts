import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { interval, Subscription } from 'rxjs';
import { TrainingStatus } from '../../../models/dashboard.types';

@Component({
  selector: 'app-training-progress',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="training-progress-container" *ngIf="trainingStatus.isTraining">
      <div class="training-card">
        <div class="training-header">
          <div class="training-icon">🎯</div>
          <div class="training-info">
            <h3>Training Your AI Model</h3>
            <p>Creating your personalized photo generation model...</p>
          </div>
        </div>

        <div class="progress-section">
          <div class="progress-bar-container">
            <div class="progress-bar">
              <div class="progress-fill" [style.width.%]="trainingStatus.progress"></div>
            </div>
            <span class="progress-percent">{{ trainingStatus.progress }}%</span>
          </div>

          <div class="progress-details">
            <p class="status-text">{{ trainingStatus.status }}</p>
            <p class="time-estimate" *ngIf="trainingStatus.estimatedTimeRemaining">
              Estimated time remaining: {{ formatTime(trainingStatus.estimatedTimeRemaining) }}
            </p>
          </div>
        </div>

        <div class="training-actions">
          <button class="btn btn-secondary" (click)="onContinueInBackground()" type="button">
            Continue in Background
          </button>
          <button class="btn btn-outline" (click)="onRefreshStatus()" type="button">
            Refresh Status
          </button>
        </div>
      </div>
    </div>

    <!-- Training Complete State -->
    <div
      class="training-complete"
      *ngIf="trainingStatus.progress === 100 && !trainingStatus.isTraining"
    >
      <div class="success-card">
        <div class="success-icon">✅</div>
        <h3>Model Training Complete!</h3>
        <p>Your AI model is ready to generate professional photos.</p>
        <button class="btn btn-primary" (click)="onStartGeneration()">
          Start Generating Photos
        </button>
      </div>
    </div>

    <!-- Training Error State -->
    <div class="training-error" *ngIf="trainingStatus.error">
      <div class="error-card">
        <div class="error-icon">⚠️</div>
        <h3>{{ getErrorTitle() }}</h3>
        <p class="error-message">{{ trainingStatus.error }}</p>

        <!-- Error-specific guidance -->
        <div class="error-guidance" *ngIf="getErrorGuidance()">
          <p class="guidance-text">{{ getErrorGuidance() }}</p>
        </div>

        <!-- Action buttons based on error type -->
        <div class="error-actions">
          <button *ngIf="isRetryableError()" class="btn btn-primary" (click)="onRetryTraining()">
            Try Again
          </button>
          <button
            *ngIf="!isRetryableError()"
            class="btn btn-secondary"
            (click)="onContactSupport()"
          >
            Contact Support
          </button>
          <button class="btn btn-outline" (click)="onRefreshStatus()">Check Status</button>
        </div>

        <!-- Technical details (development only) -->
        <div class="technical-details" *ngIf="showTechnicalDetails()">
          <details>
            <summary>Technical Details</summary>
            <pre>{{ getTechnicalDetails() }}</pre>
          </details>
        </div>
      </div>
    </div>
  `,
  styleUrls: ['./training-progress.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrainingProgressComponent implements OnInit, OnDestroy {
  @Input() trainingStatus: TrainingStatus = {
    isTraining: false,
    progress: 0,
    status: '',
  };

  @Output() continueInBackground = new EventEmitter<void>();
  @Output() refreshStatus = new EventEmitter<void>();
  @Output() startGeneration = new EventEmitter<void>();
  @Output() retryTraining = new EventEmitter<void>();
  @Output() contactSupport = new EventEmitter<void>();

  private _progressSubscription?: Subscription;

  ngOnInit(): void {
    // Auto-refresh status every 30 seconds during training
    if (this.trainingStatus.isTraining) {
      this.startProgressPolling();
    }
  }

  ngOnDestroy(): void {
    this.stopProgressPolling();
  }

  private startProgressPolling(): void {
    this._progressSubscription = interval(30000).subscribe(() => {
      if (this.trainingStatus.isTraining) {
        this.onRefreshStatus();
      } else {
        this.stopProgressPolling();
      }
    });
  }

  private stopProgressPolling(): void {
    if (this._progressSubscription) {
      this._progressSubscription.unsubscribe();
      this._progressSubscription = undefined;
    }
  }

  onContinueInBackground(): void {
    this.continueInBackground.emit();
  }

  onRefreshStatus(): void {
    this.refreshStatus.emit();
  }

  onStartGeneration(): void {
    this.startGeneration.emit();
  }

  onRetryTraining(): void {
    this.retryTraining.emit();
  }

  formatTime(seconds: number): string {
    if (seconds < 60) {
      return `${Math.round(seconds)} seconds`;
    } else if (seconds < 3600) {
      const minutes = Math.round(seconds / 60);
      return `${minutes} minute${minutes !== 1 ? 's' : ''}`;
    } else {
      const hours = Math.round(seconds / 3600);
      return `${hours} hour${hours !== 1 ? 's' : ''}`;
    }
  }

  onContactSupport(): void {
    this.contactSupport.emit();
  }

  // Enhanced error handling methods
  getErrorTitle(): string {
    if (!this.trainingStatus.error) {
      return 'Training Issue';
    }

    const error = this.trainingStatus.error.toLowerCase();

    if (error.includes('authentication') || error.includes('api authentication issue')) {
      return 'Service Authentication Issue';
    }
    if (error.includes('configuration')) {
      return 'Service Configuration Issue';
    }
    if (error.includes('temporarily unavailable') || error.includes('service is busy')) {
      return 'Service Temporarily Unavailable';
    }
    if (error.includes('insufficient credits')) {
      return 'Insufficient Credits';
    }
    if (error.includes('network') || error.includes('connection')) {
      return 'Connection Issue';
    }
    if (error.includes('timeout')) {
      return 'Request Timeout';
    }

    return 'Training Issue';
  }

  getErrorGuidance(): string | null {
    if (!this.trainingStatus.error) {
      return null;
    }

    const error = this.trainingStatus.error.toLowerCase();

    if (error.includes('authentication') || error.includes('api authentication issue')) {
      return 'This is a technical issue on our end. Our team has been notified and is working on a fix.';
    }
    if (error.includes('configuration')) {
      return 'Our service configuration needs updating. The team is working on this issue.';
    }
    if (error.includes('temporarily unavailable') || error.includes('service is busy')) {
      return 'This usually resolves quickly. The service should be available again shortly.';
    }
    if (error.includes('insufficient credits')) {
      return 'You can purchase more credits or upgrade your plan to continue training.';
    }
    if (error.includes('network') || error.includes('connection')) {
      return 'Please check your internet connection and try again.';
    }
    if (error.includes('timeout')) {
      return 'The request took too long. This might be a temporary network issue.';
    }

    return 'If this problem continues, please contact our support team for assistance.';
  }

  isRetryableError(): boolean {
    if (!this.trainingStatus.error) {
      return true;
    }

    const error = this.trainingStatus.error.toLowerCase();

    // Non-retryable errors (need support intervention)
    if (error.includes('authentication') || error.includes('api authentication issue')) {
      return false;
    }
    if (error.includes('configuration')) {
      return false;
    }
    if (error.includes('insufficient credits')) {
      return false;
    }

    // Retryable errors (user can try again)
    if (
      error.includes('temporarily unavailable') ||
      error.includes('service is busy') ||
      error.includes('network') ||
      error.includes('connection') ||
      error.includes('timeout') ||
      error.includes('service encountered an issue')
    ) {
      return true;
    }

    // Default to retryable for unknown errors
    return true;
  }

  showTechnicalDetails(): boolean {
    // Only show in development environment
    return (
      !!(window as any)?.['ng']?.['development'] ||
      location.hostname === 'localhost' ||
      location.hostname.includes('ngrok')
    );
  }

  getTechnicalDetails(): string {
    if (!this.showTechnicalDetails()) {
      return '';
    }

    return JSON.stringify(
      {
        error: this.trainingStatus.error,
        status: this.trainingStatus.status,
        progress: this.trainingStatus.progress,
        timestamp: new Date().toISOString(),
        isTraining: this.trainingStatus.isTraining,
      },
      null,
      2
    );
  }
}
