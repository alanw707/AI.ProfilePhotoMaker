import { Component, OnInit, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CreditService, UserCreditStatus } from '../../services/credit.service';

@Component({
  selector: 'app-credit-status',
  imports: [CommonModule],
  templateUrl: './credit-status.component.html',
  styleUrl: './credit-status.component.sass'
})
export class CreditStatusComponent implements OnInit {
  @Input() showDetailed: boolean = false;
  
  creditStatus: UserCreditStatus | null = null;
  isLoading = false;
  
  constructor(private creditService: CreditService) {}

  ngOnInit() {
    this.loadCreditStatus();
  }

  loadCreditStatus() {
    this.isLoading = true;
    this.creditService.getCreditStatus().subscribe({
      next: (response) => {
        if (response.success) {
          this.creditStatus = response.data;
        }
      },
      error: (error) => {
        console.error('Failed to load credit status:', error);
      },
      complete: () => {
        this.isLoading = false;
      }
    });
  }

  refresh() {
    this.loadCreditStatus();
  }

  getDaysUntilReset(): number {
    if (!this.creditStatus) return 0;
    
    const nextReset = new Date(this.creditStatus.nextResetDate);
    const now = new Date();
    const diffTime = nextReset.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    return Math.max(0, diffDays);
  }

  getOperationCost(operation: string): number {
    return this.creditService.getCreditCost(operation);
  }

  canAffordOperation(operation: string): boolean {
    if (!this.creditStatus) return false;
    
    const cost = this.getOperationCost(operation);
    const canUseWeekly = this.creditService.canUseWeeklyCredits(operation);
    
    const availableCredits = this.creditStatus.purchasedCredits + 
                           (canUseWeekly ? this.creditStatus.weeklyCredits : 0);
    
    return availableCredits >= cost;
  }
}