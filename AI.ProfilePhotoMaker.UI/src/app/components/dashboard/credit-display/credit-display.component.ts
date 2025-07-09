import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

export interface CreditInfo {
  availableCredits: number;
  totalCredits?: number;
  [key: string]: any;
}

export interface UserCreditStatus {
  weeklyCredits: number;
  purchasedCredits: number;
  totalCredits?: number;
  [key: string]: any;
}

export interface CreditActionEvent {
  action: 'purchase' | 'upgrade' | 'viewPackages';
  context?: string;
}

@Component({
  selector: 'app-credit-display',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './credit-display.component.html',
  styleUrls: ['./credit-display.component.sass']
})
export class CreditDisplayComponent {
  @Input() creditsInfo: CreditInfo | null = null;
  @Input() userCreditStatus: UserCreditStatus | null = null;
  @Input() isLoading = false;
  @Input() showCard = true;
  @Input() showSettingsHint = false;
  @Input() showBreakdown = false;
  @Input() showPurchasePrompt = false;
  @Input() requiredCredits = 0;
  @Input() trainingCredits = 0;
  @Input() generationCredits = 0;
  @Input() totalCredits = 0;
  @Input() hasEnoughCredits = true;
  @Input() remainingCredits = 0;

  @Output() creditActionRequested = new EventEmitter<CreditActionEvent>();

  /**
   * Gets the total available credits from weekly + purchased credits
   */
  getTotalAvailableCredits(): number {
    const weeklyCredits = this.getWeeklyCredits();
    const purchasedCredits = this.getPurchasedCredits();
    
    // Always calculate total from individual components to ensure accuracy
    return weeklyCredits + purchasedCredits;
  }

  /**
   * Gets the number of purchased credits
   */
  getPurchasedCredits(): number {
    return this.userCreditStatus?.purchasedCredits || 0;
  }

  /**
   * Gets the number of weekly credits
   */
  getWeeklyCredits(): number {
    // Use weeklyCredits from userCreditStatus first, fallback to creditsInfo.availableCredits
    return this.userCreditStatus?.weeklyCredits || this.creditsInfo?.availableCredits || 0;
  }

  /**
   * Gets the display text for credit counts
   */
  getCreditDisplayText(): string {
    const totalCredits = this.getTotalAvailableCredits();
    const weeklyCredits = this.getWeeklyCredits();
    const purchasedCredits = this.getPurchasedCredits();

    if (totalCredits === 0) {
      return '0 Credits';
    }

    if (purchasedCredits > 0 && weeklyCredits > 0) {
      return `${totalCredits} Credits (${weeklyCredits} Weekly + ${purchasedCredits} Purchased)`;
    } else if (purchasedCredits > 0) {
      return `${totalCredits} Purchased Credits`;
    } else if (weeklyCredits > 0) {
      return `${totalCredits} Weekly Credits`;
    } else {
      return `${totalCredits} Credits`;
    }
  }

  /**
   * Gets the subtitle text for credit display
   */
  getCreditSubtitleText(): string {
    const purchasedCredits = this.getPurchasedCredits();
    const weeklyCredits = this.getWeeklyCredits();

    if (purchasedCredits > 0 && weeklyCredits > 0) {
      return 'For model training and generation';
    } else if (purchasedCredits > 0) {
      return 'For premium features';
    } else if (weeklyCredits > 0) {
      return 'For basic photo enhancement';
    } else {
      return 'Purchase credits to get started';
    }
  }

  /**
   * Determines if we should show a purchase prompt
   */
  shouldShowPurchasePrompt(): boolean {
    const totalCredits = this.getTotalAvailableCredits();
    const purchasedCredits = this.getPurchasedCredits();
    
    // Show prompt if no purchased credits and either no total credits or insufficient for required amount
    return (purchasedCredits === 0 && (totalCredits === 0 || (this.requiredCredits > 0 && totalCredits < this.requiredCredits)));
  }

  /**
   * Determines if we should show insufficient credits warning
   */
  shouldShowInsufficientCreditsWarning(): boolean {
    return this.requiredCredits > 0 && !this.hasEnoughCredits;
  }

  /**
   * Handles credit action buttons
   */
  onCreditAction(action: 'purchase' | 'upgrade' | 'viewPackages', context?: string): void {
    this.creditActionRequested.emit({ action, context });
  }

  /**
   * Gets the appropriate icon for the credit display
   */
  getCreditIcon(): string {
    const purchasedCredits = this.getPurchasedCredits();
    const weeklyCredits = this.getWeeklyCredits();
    
    if (purchasedCredits > 0 && weeklyCredits > 0) {
      return '💎';
    } else if (purchasedCredits > 0) {
      return '💰';
    } else if (weeklyCredits > 0) {
      return '⚡';
    } else {
      return '💎';
    }
  }
}