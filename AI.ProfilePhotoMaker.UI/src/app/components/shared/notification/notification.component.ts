import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Notification, NotificationService } from '../../../services/notification.service';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

@Component({
  selector: 'app-notification',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notification.component.html',
  styleUrls: ['./notification.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationComponent implements OnInit, OnDestroy {
  notifications: Notification[] = [];
  private destroy$ = new Subject<void>();

  constructor(
    private _notificationService: NotificationService,
    private _cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this._notificationService.notifications$
      .pipe(takeUntil(this.destroy$))
      .subscribe(notifications => {
        this.notifications = notifications;
        this._cdr.detectChanges();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  removeNotification(id: string): void {
    this._notificationService.removeNotification(id);
    this._cdr.detectChanges();
  }

  getSvgIcon(type: string): string {
    switch (type) {
      case 'success':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="m9 12 2 2 4-4"/><circle cx="12" cy="12" r="10"/></svg>';
      case 'error':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><circle cx="12" cy="12" r="10"/><path d="m15 9-6 6"/><path d="m9 9 6 6"/></svg>';
      case 'warning':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><path d="M12 9v4"/><path d="m12 17.02.01-.01"/></svg>';
      case 'info':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><circle cx="12" cy="12" r="10"/><path d="m12 16v-4"/><path d="m12 8h.01"/></svg>';
      default:
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><circle cx="12" cy="12" r="10"/><path d="m9 9 6 6"/><path d="m15 9-6 6"/></svg>';
    }
  }

  trackByNotificationId(index: number, notification: Notification): string {
    return notification.id;
  }
}
