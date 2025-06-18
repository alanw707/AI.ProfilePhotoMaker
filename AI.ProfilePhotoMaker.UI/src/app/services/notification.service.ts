import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface Notification {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  duration?: number; // in milliseconds, 0 means permanent
  timestamp: Date;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private notificationsSubject = new BehaviorSubject<Notification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();

  constructor() {}

  private generateId(): string {
    return Math.random().toString(36).substr(2, 9);
  }

  private addNotification(notification: Omit<Notification, 'id' | 'timestamp'>): string {
    const id = this.generateId();
    const newNotification: Notification = {
      ...notification,
      id,
      timestamp: new Date(),
      duration: notification.duration ?? 5000 // 5 seconds default
    };

    const currentNotifications = this.notificationsSubject.value;
    this.notificationsSubject.next([...currentNotifications, newNotification]);

    // Auto-remove notification after duration
    if (newNotification.duration && newNotification.duration > 0) {
      setTimeout(() => {
        this.removeNotification(id);
      }, newNotification.duration);
    }

    return id;
  }

  success(title: string, message: string, duration?: number): string {
    return this.addNotification({
      type: 'success',
      title,
      message,
      duration
    });
  }

  error(title: string, message: string, duration?: number): string {
    return this.addNotification({
      type: 'error',
      title,
      message,
      duration: duration ?? 0 // Errors stay until manually dismissed
    });
  }

  warning(title: string, message: string, duration?: number): string {
    return this.addNotification({
      type: 'warning',
      title,
      message,
      duration
    });
  }

  info(title: string, message: string, duration?: number): string {
    return this.addNotification({
      type: 'info',
      title,
      message,
      duration
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
    return this.error(
      'Upload Failed',
      error || 'Failed to upload images. Please try again.'
    );
  }

  generationSuccess(creditsRemaining?: number): string {
    const message = creditsRemaining !== undefined 
      ? `Image generated successfully! You have ${creditsRemaining} credits remaining.`
      : 'Image generated successfully!';
    
    return this.success('Generation Complete', message);
  }

  generationError(error: string): string {
    return this.error(
      'Generation Failed',
      error || 'Failed to generate image. Please try again.'
    );
  }

  creditsExhausted(): string {
    return this.warning(
      'No Credits Remaining',
      'You have no credits remaining. Credits reset weekly, or upgrade for unlimited generations.'
    );
  }

  profileUpdateSuccess(): string {
    return this.success(
      'Profile Updated',
      'Your profile has been successfully updated.'
    );
  }

  profileUpdateError(error: string): string {
    return this.error(
      'Profile Update Failed',
      error || 'Failed to update profile. Please try again.'
    );
  }
}