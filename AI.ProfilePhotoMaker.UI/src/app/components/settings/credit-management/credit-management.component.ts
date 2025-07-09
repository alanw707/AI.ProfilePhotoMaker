import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-credit-management',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './credit-management.component.html',
  styleUrl: './credit-management.component.sass'
})
export class CreditManagementComponent {
  @Input() creditsInfo: any = null;
  @Input() userCreditStatus: any = null;

  getTotalAvailableCredits(): number {
    const weeklyCredits = this.getWeeklyCredits();
    const purchasedCredits = this.getPurchasedCredits();
    return weeklyCredits + purchasedCredits;
  }

  getPurchasedCredits(): number {
    return this.userCreditStatus?.purchasedCredits || 0;
  }

  getWeeklyCredits(): number {
    return this.userCreditStatus?.weeklyCredits || this.creditsInfo?.availableCredits || 0;
  }

  getMaxWeeklyCredits(): number {
    return 3; // Fixed weekly credit limit
  }

  getCreditUsagePercentage(): number {
    const weekly = this.getWeeklyCredits();
    const max = this.getMaxWeeklyCredits();
    return max > 0 ? (weekly / max) * 100 : 0;
  }

  getNextCreditReset(): string {
    // Calculate next weekly reset (simplified)
    const now = new Date();
    const nextWeek = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
    return nextWeek.toLocaleDateString('en-US', {
      weekday: 'long',
      month: 'short',
      day: 'numeric'
    });
  }
}
