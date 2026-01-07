import { ChangeDetectionStrategy, Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CreditService, UserCreditStatus } from '../../services/credit.service';

@Component({
  selector: 'app-credit-status',
  imports: [CommonModule],
  templateUrl: './credit-status.component.html',
  styleUrl: './credit-status.component.sass',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreditStatusComponent implements OnInit {
  @Input() showDetailed = false;

  creditStatus: UserCreditStatus | null = null;
  isLoading = false;

  constructor(private _creditService: CreditService) {}

  ngOnInit(): void {
    this.loadCreditStatus();
  }

  loadCreditStatus(): void {
    this.isLoading = true;
    this._creditService.getCreditStatus().subscribe({
      next: response => {
        if (response.success) {
          this.creditStatus = response.data;
        }
      },
      error: error => {
        console.error('Failed to load credit status:', error);
      },
      complete: () => {
        this.isLoading = false;
      },
    });
  }

  refresh(): void {
    this.loadCreditStatus();
  }

  getDaysUntilReset(): number {
    if (!this.creditStatus) {
      return 0;
    }

    const nextReset = new Date(this.creditStatus.nextResetDate);
    const now = new Date();
    const diffTime = nextReset.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    return Math.max(0, diffDays);
  }

  getOperationCost(operation: string): number {
    return this._creditService.getCreditCostSync(operation);
  }

  canAffordOperation(operation: string): boolean {
    if (!this.creditStatus) {
      return false;
    }

    const cost = this.getOperationCost(operation);
    return this.creditStatus.credits >= cost;
  }
}
