import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { ConfigService, BackendConfig, ConfigurationStatus } from '../../services/config.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-config-status',
  standalone: true,
  imports: [CommonModule],
  styleUrl: './config-status.component.sass',
  template: `
    <div *ngIf="showDebugInfo" class="config-status" [class.collapsed]="isCollapsed">
      <div class="config-header" (click)="toggleCollapsed()">
        <span class="config-title">🔧 Config Status</span>
        <span class="config-indicator" [class]="getStatusClass()">●</span>
        <span class="config-toggle">{{ isCollapsed ? '▼' : '▲' }}</span>
      </div>
      
      <div *ngIf="!isCollapsed" class="config-content">
        <div class="config-section">
          <h4>Status</h4>
          <div class="config-item">
            <span class="label">Loaded:</span>
            <span class="value" [class.success]="status.isLoaded">{{ status.isLoaded ? '✅' : '❌' }}</span>
          </div>
          <div class="config-item">
            <span class="label">Source:</span>
            <span class="value">{{ getConfigSource() }}</span>
          </div>
          <div *ngIf="status.error" class="config-item error">
            <span class="label">Error:</span>
            <span class="value">{{ status.error }}</span>
          </div>
        </div>

        <div *ngIf="config" class="config-section">
          <h4>Configuration</h4>
          <div class="config-item">
            <span class="label">Environment:</span>
            <span class="value">{{ config.environment }}</span>
          </div>
          <div class="config-item">
            <span class="label">Backend:</span>
            <span class="value url">{{ config.appBaseUrl }}</span>
          </div>
          <div class="config-item">
            <span class="label">Frontend:</span>
            <span class="value url">{{ config.frontendBaseUrl }}</span>
          </div>
          <div class="config-item">
            <span class="label">External:</span>
            <span class="value" [class.success]="!config.appBaseUrl.includes('localhost')">
              {{ config.appBaseUrl.includes('localhost') ? 'No' : 'Yes' }}
            </span>
          </div>
        </div>

        <div class="config-actions">
          <button (click)="refreshConfig()" [disabled]="refreshing">
            {{ refreshing ? 'Refreshing...' : 'Refresh' }}
          </button>
          <button (click)="showConfigDetails = !showConfigDetails">
            {{ showConfigDetails ? 'Hide' : 'Show' }} Details
          </button>
        </div>

        <div *ngIf="showConfigDetails && config" class="config-details">
          <pre>{{ config | json }}</pre>
        </div>
      </div>
    </div>
  `
})
export class ConfigStatusComponent implements OnInit, OnDestroy {
  config: BackendConfig | null = null;
  status: ConfigurationStatus = {
    isLoaded: false,
    isFromBackend: false,
    isFromCache: false,
    lastUpdated: null,
    error: null
  };
  
  isCollapsed = true;
  showConfigDetails = false;
  refreshing = false;
  showDebugInfo = false;
  
  private subscriptions: Subscription[] = [];

  constructor(private configService: ConfigService) {}

  ngOnInit() {
    // Only show in development or test environments
    this.showDebugInfo = !environment.production || environment.development || environment.test;
    
    if (!this.showDebugInfo) {
      return;
    }

    // Subscribe to configuration changes
    this.subscriptions.push(
      this.configService.config$.subscribe(config => {
        this.config = config;
      })
    );

    // Subscribe to status changes
    this.subscriptions.push(
      this.configService.status$.subscribe(status => {
        this.status = status;
      })
    );
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  toggleCollapsed() {
    this.isCollapsed = !this.isCollapsed;
  }

  getStatusClass(): string {
    if (!this.status.isLoaded) return 'error';
    if (this.status.error) return 'warning';
    if (this.status.isFromBackend) return 'success';
    return 'warning';
  }

  getConfigSource(): string {
    if (!this.status.isLoaded) return 'Not loaded';
    if (this.status.isFromBackend) return 'Backend API';
    if (this.status.isFromCache) return 'Cache';
    return 'Fallback';
  }

  refreshConfig() {
    this.refreshing = true;
    this.configService.refreshConfiguration().subscribe({
      next: () => {
        this.refreshing = false;
      },
      error: () => {
        this.refreshing = false;
      }
    });
  }
}