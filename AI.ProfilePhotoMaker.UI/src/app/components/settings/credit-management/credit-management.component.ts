import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-credit-management',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './credit-management.component.html',
  styleUrl: './credit-management.component.sass',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreditManagementComponent {
  @Input() creditsInfo: unknown = null;
  @Input() userCreditStatus: unknown = null;

  getTotalAvailableCredits(): number {
    return (this.userCreditStatus as any)?.credits || (this.creditsInfo as any)?.credits || 0;
  }

  getNextCreditReset(): string {
    if ((this.userCreditStatus as any)?.nextResetDate) {
      const resetDate = new Date((this.userCreditStatus as any).nextResetDate);
      return resetDate.toLocaleDateString('en-US', {
        weekday: 'long',
        month: 'short',
        day: 'numeric',
      });
    }

    // Fallback: Calculate next weekly reset
    const now = new Date();
    const daysUntilReset = 7 - now.getDay(); // Days until next Sunday
    const nextReset = new Date(now);
    nextReset.setDate(now.getDate() + daysUntilReset);
    nextReset.setHours(0, 0, 0, 0);

    return nextReset.toLocaleDateString('en-US', {
      weekday: 'long',
      month: 'short',
      day: 'numeric',
    });
  }
}
