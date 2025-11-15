# SignalR Real-Time Enhancement Updates - Integration Guide

> Status: Optional enhancement guide. Real-time notifications are not required for the current MVP; treat this as a future roadmap reference rather than required implementation.

## Overview
Real-time prediction notifications eliminate the need for frontend polling, providing instant completion notifications.

## Frontend Integration

### 1. Install SignalR Client (Angular)
```bash
npm install @microsoft/signalr
```

### 2. Service Implementation
```typescript
// services/prediction-notification.service.ts
import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class PredictionNotificationService {
  private connection: signalR.HubConnection;
  private predictionCompleted$ = new BehaviorSubject<any>(null);

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/prediction', {
        accessTokenFactory: () => this.getAuthToken()
      })
      .withAutomaticReconnect()
      .build();

    this.startConnection();
    this.setupEventHandlers();
  }

  private async startConnection() {
    try {
      await this.connection.start();
      console.log('Connected to prediction hub');
    } catch (error) {
      console.error('Failed to connect to prediction hub:', error);
    }
  }

  private setupEventHandlers() {
    this.connection.on('PredictionCompleted', (data) => {
      console.log('Enhancement completed:', data);
      this.predictionCompleted$.next(data);
    });
  }

  subscribeToPrediction(predictionId: string) {
    return this.connection.invoke('SubscribeToPrediction', predictionId);
  }

  onPredictionCompleted(): Observable<any> {
    return this.predictionCompleted$.asObservable();
  }

  private getAuthToken(): string {
    return localStorage.getItem('authToken') || '';
  }
}
```

### 3. Component Usage
```typescript
// components/photo-enhancement.component.ts
export class PhotoEnhancementComponent {
  constructor(
    private predictionService: PredictionNotificationService,
    private replicateService: ReplicateService
  ) {}

  async enhancePhoto(imageUrl: string) {
    // Start enhancement
    const response = await this.replicateService.enhancePhoto(imageUrl);
    const predictionId = response.data.prediction.id;

    // Subscribe to real-time updates
    await this.predictionService.subscribeToPrediction(predictionId);
    
    // Listen for completion
    this.predictionService.onPredictionCompleted().subscribe(data => {
      if (data?.predictionId === predictionId) {
        console.log(`Enhancement completed! ${data.imageCount} images ready`);
        this.handleEnhancementComplete(data);
      }
    });
  }

  private handleEnhancementComplete(data: any) {
    // Update UI with completed images
    this.showSuccessMessage(`Enhancement complete! ${data.imageCount} images ready`);
    this.refreshImageGallery();
  }
}
```

## API Changes Summary

### ✅ Fixed Issues
1. **404 Error**: `EnhancePhotoAsync` now persists predictions to local database
2. **Webhook Completion**: Webhook handler now updates prediction status and sends real-time notifications
3. **Real-Time Updates**: SignalR hub provides instant completion notifications

### ✅ New Features
- **SignalR Hub**: `/hubs/prediction` for real-time updates
- **User-Specific Groups**: Targeted notifications via `user_{userId}` groups  
- **Prediction Groups**: Direct prediction updates via `prediction_{predictionId}` groups
- **Automatic Reconnection**: Robust connection handling with retry logic

### ✅ Performance Improvements
- **Eliminated Polling**: No more 2-3 second API polling loops
- **Battery Savings**: Significantly reduced mobile battery consumption
- **Instant Feedback**: Sub-second notification delivery
- **Reduced Load**: Lower server API call volume

## Testing the Implementation

1. **Start Enhancement**: Call `/api/replicate/enhance` endpoint
2. **Connect SignalR**: Frontend connects to `/hubs/prediction`
3. **Wait for Webhook**: Replicate calls `/api/webhooks/replicate/prediction-complete`
4. **Receive Notification**: Real-time `PredictionCompleted` event fired
5. **Update UI**: Frontend immediately updates without polling

## Migration Notes

- **Database Schema**: No changes required - reuses existing `Predictions` table
- **Backward Compatibility**: Existing polling mechanism still works
- **Progressive Enhancement**: SignalR provides better UX, polling serves as fallback
