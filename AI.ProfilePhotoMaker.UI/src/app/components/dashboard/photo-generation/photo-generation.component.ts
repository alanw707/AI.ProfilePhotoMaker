import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { interval, Subscription } from 'rxjs';
import { GeneratedPhoto, GenerationStatus } from '../../../models/dashboard.types';

@Component({
  selector: 'app-photo-generation',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Generation In Progress -->
    <div class="generation-progress-container" *ngIf="generationStatus.isGenerating">
      <div class="generation-card">
        <div class="generation-header">
          <div class="generation-icon">🎨</div>
          <div class="generation-info">
            <h3>Generating Your Photos</h3>
            <p>Creating professional headshots with your trained AI model...</p>
          </div>
        </div>
        
        <div class="progress-section">
          <div class="progress-bar-container">
            <div class="progress-bar">
              <div class="progress-fill" [style.width.%]="generationStatus.progress"></div>
            </div>
            <span class="progress-percent">{{generationStatus.progress}}%</span>
          </div>
          
          <div class="progress-details">
            <p class="status-text">{{generationStatus.status}}</p>
            <p class="image-count" *ngIf="generationStatus.completedImages !== undefined">
              Generated {{generationStatus.completedImages}} of {{generationStatus.totalImages}} images
            </p>
            <p class="time-estimate" *ngIf="generationStatus.estimatedTimeRemaining">
              Estimated time remaining: {{formatTime(generationStatus.estimatedTimeRemaining)}}
            </p>
          </div>
        </div>

        <div class="generation-actions">
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

    <!-- Generation Complete -->
    <div class="generation-complete" *ngIf="generatedPhotos.length > 0 && !generationStatus.isGenerating">
      <div class="success-card">
        <div class="success-header">
          <div class="success-icon">🎉</div>
          <h3>Photos Generated Successfully!</h3>
          <p>{{generatedPhotos.length}} professional headshots have been created.</p>
        </div>

        <div class="photo-preview">
          <div class="photo-grid">
            <div 
              class="photo-item" 
              *ngFor="let photo of generatedPhotos.slice(0, 4)"
              (click)="onViewPhoto(photo)">
              <img [src]="photo.url" [alt]="photo.style + ' photo'">
              <div class="photo-style">{{formatStyleName(photo.style)}}</div>
            </div>
            <div class="more-photos" *ngIf="generatedPhotos.length > 4">
              +{{generatedPhotos.length - 4}} more
            </div>
          </div>
        </div>

        <div class="success-actions">
          <button 
            class="btn btn-primary" 
            routerLink="/gallery"
            type="button">
            View All Photos
          </button>
          <button 
            class="btn btn-secondary" 
            (click)="onDownloadAll()"
            type="button">
            Download All
          </button>
          <button 
            class="btn btn-outline" 
            (click)="onGenerateMore()"
            type="button">
            Generate More
          </button>
        </div>
      </div>
    </div>

    <!-- Generation Error -->
    <div class="generation-error" *ngIf="generationStatus.error">
      <div class="error-card">
        <div class="error-icon">❌</div>
        <h3>Generation Failed</h3>
        <p>{{generationStatus.error}}</p>
        <button class="btn btn-primary" (click)="onRetryGeneration()">
          Retry Generation
        </button>
      </div>
    </div>
  `,
  styleUrls: ['./photo-generation.component.sass']
})
export class PhotoGenerationComponent implements OnInit, OnDestroy {
  @Input() generationStatus: GenerationStatus = {
    isGenerating: false,
    progress: 0,
    status: ''
  };
  
  @Input() generatedPhotos: GeneratedPhoto[] = [];

  @Output() continueInBackground = new EventEmitter<void>();
  @Output() refreshStatus = new EventEmitter<void>();
  @Output() viewPhoto = new EventEmitter<GeneratedPhoto>();
  @Output() downloadAll = new EventEmitter<void>();
  @Output() generateMore = new EventEmitter<void>();
  @Output() retryGeneration = new EventEmitter<void>();

  private progressSubscription?: Subscription;

  ngOnInit(): void {
    if (this.generationStatus.isGenerating) {
      this.startProgressPolling();
    }
  }

  ngOnDestroy(): void {
    this.stopProgressPolling();
  }

  private startProgressPolling(): void {
    this.progressSubscription = interval(10000).subscribe(() => {
      if (this.generationStatus.isGenerating) {
        this.onRefreshStatus();
      } else {
        this.stopProgressPolling();
      }
    });
  }

  private stopProgressPolling(): void {
    if (this.progressSubscription) {
      this.progressSubscription.unsubscribe();
      this.progressSubscription = undefined;
    }
  }

  onContinueInBackground(): void {
    this.continueInBackground.emit();
  }

  onRefreshStatus(): void {
    this.refreshStatus.emit();
  }

  onViewPhoto(photo: GeneratedPhoto): void {
    this.viewPhoto.emit(photo);
  }

  onDownloadAll(): void {
    this.downloadAll.emit();
  }

  onGenerateMore(): void {
    this.generateMore.emit();
  }

  onRetryGeneration(): void {
    this.retryGeneration.emit();
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

  formatStyleName(style: string): string {
    if (!style) {
      return '';
    }
    return style
      .replace(/[-_/]/g, ' ')
      .split(' ')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(' ');
  }
}