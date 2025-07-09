import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
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
            <span class="progress-percent">{{trainingStatus.progress}}%</span>
          </div>
          
          <div class="progress-details">
            <p class="status-text">{{trainingStatus.status}}</p>
            <p class="time-estimate" *ngIf="trainingStatus.estimatedTimeRemaining">
              Estimated time remaining: {{formatTime(trainingStatus.estimatedTimeRemaining)}}
            </p>
          </div>
        </div>

        <div class="training-actions">
          <button 
            class="btn btn-secondary" 
            (click)="onContinueInBackground()"
            type="button">
            Continue in Background
          </button>
          <button 
            class="btn btn-outline" 
            (click)="onRefreshStatus()"
            type="button">
            Refresh Status
          </button>
        </div>
      </div>
    </div>

    <!-- Training Complete State -->
    <div class="training-complete" *ngIf="trainingStatus.progress === 100 && !trainingStatus.isTraining">
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
        <div class="error-icon">❌</div>
        <h3>Training Failed</h3>
        <p>{{trainingStatus.error}}</p>
        <button class="btn btn-primary" (click)="onRetryTraining()">
          Retry Training
        </button>
      </div>
    </div>
  `,
  styleUrls: ['./training-progress.component.sass']
})
export class TrainingProgressComponent implements OnInit, OnDestroy {
  @Input() trainingStatus: TrainingStatus = {
    isTraining: false,
    progress: 0,
    status: ''
  };

  @Output() continueInBackground = new EventEmitter<void>();
  @Output() refreshStatus = new EventEmitter<void>();
  @Output() startGeneration = new EventEmitter<void>();
  @Output() retryTraining = new EventEmitter<void>();

  private progressSubscription?: Subscription;

  ngOnInit() {
    // Auto-refresh status every 30 seconds during training
    if (this.trainingStatus.isTraining) {
      this.startProgressPolling();
    }
  }

  ngOnDestroy() {
    this.stopProgressPolling();
  }

  private startProgressPolling() {
    this.progressSubscription = interval(30000).subscribe(() => {
      if (this.trainingStatus.isTraining) {
        this.onRefreshStatus();
      } else {
        this.stopProgressPolling();
      }
    });
  }

  private stopProgressPolling() {
    if (this.progressSubscription) {
      this.progressSubscription.unsubscribe();
      this.progressSubscription = undefined;
    }
  }

  onContinueInBackground() {
    this.continueInBackground.emit();
  }

  onRefreshStatus() {
    this.refreshStatus.emit();
  }

  onStartGeneration() {
    this.startGeneration.emit();
  }

  onRetryTraining() {
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
}