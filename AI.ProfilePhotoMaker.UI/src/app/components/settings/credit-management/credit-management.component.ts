import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-credit-management',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './credit-management.component.html',
  styleUrl: './credit-management.component.sass',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreditManagementComponent {
  @Input() creditsInfo: unknown = null;
  @Input() userCreditStatus: unknown = null;

  getTotalAvailableCredits(): number {
    const weeklyCredits = this.getWeeklyCredits();
    const purchasedCredits = this.getPurchasedCredits();
    return weeklyCredits + purchasedCredits;
  }

  getPurchasedCredits(): number {
    return (this.userCreditStatus as any)?.purchasedCredits || 0;
  }

  getWeeklyCredits(): number {
    return (this.userCreditStatus as any)?.weeklyCredits || (this.creditsInfo as any)?.availableCredits || 0;
  }

  getMaxWeeklyCredits(): number {
    return 3; // Fixed weekly credit limit
  }

  getCreditUsagePercentage(): number {
    const weekly = this.getWeeklyCredits();
    const max = this.getMaxWeeklyCredits();
    const used = max - weekly;
    return max > 0 ? (used / max) * 100 : 0;
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
