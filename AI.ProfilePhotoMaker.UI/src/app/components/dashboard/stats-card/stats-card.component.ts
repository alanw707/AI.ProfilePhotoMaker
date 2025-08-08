import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';

@Component({
  selector: 'app-stats-card',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './stats-card.component.html',
  styleUrls: ['./stats-card.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatsCardComponent {
  @Input() icon = '';
  @Input() value: string | number = 0;
  @Input() label = '';
  @Input() showCard = true;
  @Input() isModelStatus = false;
  @Input() isLoading = false;
  @Input() isClickable = false;
  @Input() clickAction = ''; // 'gallery', 'settings', etc.

  @Output() cardClicked = new EventEmitter<string>();

  constructor(private _router: Router) {}

  onCardClick(): void {
    if (!this.isClickable || this.isLoading) {
      return;
    }

    if (this.clickAction === 'gallery') {
      // Navigate to gallery with refresh parameter
      this._router.navigate(['/app/gallery'], {
        queryParams: { refresh: Date.now() },
      });
    } else if (this.clickAction === 'settings') {
      // Navigate to settings
      this._router.navigate(['/app/settings']);
    }

    // Emit event for custom handling
    this.cardClicked.emit(this.clickAction);
  }
}
