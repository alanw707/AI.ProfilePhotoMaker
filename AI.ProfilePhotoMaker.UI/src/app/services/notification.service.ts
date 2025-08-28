import { Injectable, NgZone } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface Notification {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  duration?: number; // in milliseconds, 0 means permanent
  timestamp: Date;
}

@Injectable({
  providedIn: 'root',
})
export class NotificationService {
  private notificationsSubject = new BehaviorSubject<Notification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();

  constructor(private ngZone: NgZone) {}

  private generateId(): string {
    return Math.random().toString(36).substr(2, 9);
  }

  private addNotification(notification: Omit<Notification, 'id' | 'timestamp'>): string {
    const id = this.generateId();
    const newNotification: Notification = {
      ...notification,
      id,
      timestamp: new Date(),
      duration: notification.duration ?? 5000, // 5 seconds default
    };

    const currentNotifications = this.notificationsSubject.value;
    this.notificationsSubject.next([...currentNotifications, newNotification]);

    // Auto-remove notification after duration
    if (newNotification.duration && newNotification.duration > 0) {
      this.ngZone.runOutsideAngular(() => {
        setTimeout(() => {
          this.ngZone.run(() => {
            this.removeNotification(id);
          });
        }, newNotification.duration);
      });
    }

    return id;
  }

  success(title: string, message: string, duration?: number): string {
    return this.addNotification({
      type: 'success',
      title,
      message,
      duration,
    });
  }

  error(title: string, message: string, duration?: number): string {
    return this.addNotification({
      type: 'error',
      title,
      message,
      duration: duration ?? 0, // Errors stay until manually dismissed
    });
  }

  warning(title: string, message: string, duration?: number): string {
    return this.addNotification({
      type: 'warning',
      title,
      message,
      duration,
    });
  }

  info(title: string, message: string, duration?: number): string {
    return this.addNotification({
      type: 'info',
      title,
      message,
      duration,
    });
  }

  removeNotification(id: string): void {
    const currentNotifications = this.notificationsSubject.value;
    const updatedNotifications = currentNotifications.filter(n => n.id !== id);
    this.notificationsSubject.next(updatedNotifications);
  }

  clearAll(): void {
    this.notificationsSubject.next([]);
  }

  // Convenience methods for common scenarios
  uploadSuccess(fileCount: number): string {
    return this.success(
      'Upload Successful',
      `Successfully uploaded ${fileCount} image${fileCount > 1 ? 's' : ''}.`
    );
  }

  uploadError(error: string): string {
    return this.error('Upload Failed', error || 'Failed to upload images. Please try again.');
  }

  generationSuccess(creditsRemaining?: number): string {
    const message =
      creditsRemaining !== undefined
        ? `Image generated successfully! You have ${creditsRemaining} credits remaining.`
        : 'Image generated successfully!';

    return this.success('Generation Complete', message);
  }

  generationError(error: string): string {
    return this.error('Generation Failed', error || 'Failed to generate image. Please try again.');
  }

  // Friendly toast when user already has a trained model
  modelAlreadyTrained(): string {
    return this.info(
      'Model Already Trained',
      "Great news! You already have a trained model. We'll use it to generate your photos now.",
      6000
    );
  }

  creditsExhausted(): string {
    return this.warning(
      'No Credits Remaining',
      'You have no credits remaining. Credits reset weekly, or upgrade for unlimited generations.'
    );
  }

  profileUpdateSuccess(): string {
    return this.success('Profile Updated', 'Your profile has been successfully updated.');
  }

  profileUpdateError(error: string): string {
    return this.error(
      'Profile Update Failed',
      error || 'Failed to update profile. Please try again.'
    );
  }

  // Enhanced training error notifications with actionable guidance
  trainingConfigurationError(): string {
    return this.error(
      'Service Configuration Issue',
      'Our training service needs attention. The team has been notified and is working on a fix. Please try again in a few minutes.',
      0 // Stay visible until dismissed
    );
  }

  trainingAuthenticationError(): string {
    return this.error(
      'Service Authentication Issue',
      'API authentication needs attention. Our team has been notified and is working on a fix. Please try again in a few minutes.',
      0 // Stay visible until dismissed
    );
  }

  trainingServiceUnavailable(): string {
    return this.warning(
      'Service Temporarily Unavailable',
      'Our training service is temporarily busy. This usually resolves quickly - please try again in a few minutes.',
      8000 // 8 seconds for service issues
    );
  }

  trainingRetryableError(message: string): string {
    return this.warning(
      'Training Issue',
      `${message} Please try again in a few minutes, or contact support if the problem persists.`,
      8000
    );
  }

  trainingNonRetryableError(message: string): string {
    return this.error(
      'Training Configuration',
      `${message} Please contact support for assistance.`,
      0 // Stay visible until dismissed
    );
  }

  // Credit-related errors with purchase guidance
  insufficientCreditsForTraining(required: number, available: number): string {
    return this.error(
      'Insufficient Credits',
      `Training requires ${required} credits, but you have ${available}. Please purchase more credits or upgrade your plan.`,
      0 // Stay visible until dismissed
    );
  }

  // Network and connectivity errors
  networkConnectionError(): string {
    return this.warning(
      'Connection Issue',
      'Network connection problem detected. Please check your internet connection and try again.',
      6000
    );
  }

  requestTimeoutError(): string {
    return this.warning(
      'Request Timeout',
      'The request took too long to complete. Please try again.',
      6000
    );
  }

  // User-friendly retry suggestions
  trainingErrorWithRetry(title: string, message: string, isRetryable = true): string {
    const enhancedMessage = isRetryable
      ? `${message} You can try again, or contact support if the issue continues.`
      : `${message} Please contact support for assistance.`;

    return isRetryable
      ? this.warning(title, enhancedMessage, 8000)
      : this.error(title, enhancedMessage, 0);
  }

  // Development/debugging helper (only shows in dev mode)
  debugError(title: string, technicalDetails: string): string {
    // Only show technical details in development
    const isDevelopment =
      !!(window as any)?.['ng']?.['development'] ||
      location.hostname === 'localhost' ||
      location.hostname.includes('ngrok');

    if (isDevelopment) {
      return this.error(
        `${title} (Debug)`,
        `${technicalDetails}\n\nThis detailed error is only shown in development mode.`,
        0
      );
    }

    // In production, show user-friendly message
    return this.error(
      title,
      'We encountered a technical issue. Please try again or contact support.',
      0
    );
  }
}
