import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FeedbackCategory, FeedbackService } from '../../services/feedback.service';
import { NotificationService } from '../../services/notification.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';

@Component({
  selector: 'app-support',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, HeaderNavigationComponent],
  templateUrl: './support.component.html',
  styleUrls: ['./support.component.sass'],
})
export class SupportComponent {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _formBuilder = inject(FormBuilder);
  private readonly _feedbackService = inject(FeedbackService);
  private readonly _notificationService = inject(NotificationService);

  isSubmitting = false;

  readonly categories: FeedbackCategory[] = ['Bug', 'Feature', 'Question', 'Other'];

  readonly form = this._formBuilder.group({
    category: this._formBuilder.control<FeedbackCategory>('Bug', { nonNullable: true }),
    message: this._formBuilder.control('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(10), Validators.maxLength(2000)],
    }),
  });

  constructor() {
    const query = this._route.snapshot.queryParamMap;
    const category = query.get('category');
    const message = query.get('message');

    const normalizedCategory = this.categories.find(c => c.toLowerCase() === (category || '').toLowerCase());
    if (normalizedCategory) {
      this.form.controls.category.setValue(normalizedCategory);
    }

    if (message) {
      this.form.controls.message.setValue(message);
    }
  }

  get messageLength(): number {
    return (this.form.value.message || '').length;
  }

  submit(): void {
    if (this.isSubmitting) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;

    this._feedbackService
      .submitFeedback({
        category: this.form.value.category || 'Other',
        message: this.form.value.message || '',
        pageUrl: window.location.href,
        userAgent: navigator.userAgent,
      })
      .subscribe({
        next: response => {
          if (response.success) {
            this._notificationService.success(
              'Message sent',
              response.message || 'Thanks — we received your message.'
            );
            this.form.reset({ category: 'Bug', message: '' });
          } else {
            this._notificationService.error(
              'Send failed',
              response.error?.message || 'Unable to send your message. Please try again.'
            );
          }
          this.isSubmitting = false;
        },
        error: () => {
          this._notificationService.error(
            'Send failed',
            'Unable to send your message. Please try again.'
          );
          this.isSubmitting = false;
        },
      });
  }

  backToSettings(): void {
    void this._router.navigate(['/app/settings']);
  }
}
