import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

export interface CreditInfo {
  credits: number;
  [key: string]: unknown;
}

export interface UserCreditStatus {
  credits: number;
  lastCreditReset?: string;
  nextResetDate?: string;
  [key: string]: unknown;
}

export interface CreditActionEvent {
  action: 'purchase' | 'upgrade' | 'viewPackages';
  context?: string;
}

export type CreditDisplayContext = 'default' | 'headshots';

@Component({
  selector: 'app-credit-display',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './credit-display.component.html',
  styleUrls: ['./credit-display.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
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
  @Input() displayContext: CreditDisplayContext = 'default';

  @Output() creditActionRequested = new EventEmitter<CreditActionEvent>();
  /** Emitted when the user should be shown the out-of-credits upgrade modal. */
  @Output() showUpgradeModal = new EventEmitter<void>();

  /**
   * Gets the total available credits
   */
  getTotalAvailableCredits(): number {
    if (this.userCreditStatus?.credits !== undefined) {
      return this.userCreditStatus.credits;
    }

    return this.creditsInfo?.credits || 0;
  }

  /**
   * Gets the display text for credit counts
   */
  getCreditDisplayText(): string {
    const totalCredits = this.getTotalAvailableCredits();

    if (totalCredits === 0) {
      return '0 Credits';
    }

    if (this.isHeadshotContext()) {
      return `${totalCredits} Credits`;
    }

    return `${totalCredits} Credits`;
  }

  /**
   * Gets the subtitle text for credit display
   */
  getCreditSubtitleText(): string {
    const totalCredits = this.getTotalAvailableCredits();
    if (this.isHeadshotContext()) {
      return totalCredits > 0
        ? 'Credits available for training and generation'
        : 'Purchase credits to get started';
    }
    return totalCredits > 0
      ? 'Use credits for training, generation, and enhancement'
      : 'Purchase credits to get started';
  }

  /**
   * Determines if we should show a purchase prompt
   */
  shouldShowPurchasePrompt(): boolean {
    const totalCredits = this.getTotalAvailableCredits();
    return totalCredits === 0 || (this.requiredCredits > 0 && totalCredits < this.requiredCredits);
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
   * Opens the out-of-credits upgrade modal
   */
  onShowUpgradeModal(): void {
    this.showUpgradeModal.emit();
  }

  /**
   * Gets the appropriate icon for the credit display
   */
  getCreditIcon(): string {
    return this.getTotalAvailableCredits() > 0 ? '💎' : '⚠️';
  }

  private isHeadshotContext(): boolean {
    return this.displayContext === 'headshots';
  }
}
