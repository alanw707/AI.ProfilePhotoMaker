import { Component, OnInit, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CreditService, CreditPackage, UserCreditStatus } from '../../services/credit.service';
import { NotificationService } from '../../services/notification.service';

@Component({
  selector: 'app-credit-packages',
  imports: [CommonModule],
  templateUrl: './credit-packages.component.html',
  styleUrl: './credit-packages.component.sass'
})
export class CreditPackagesComponent implements OnInit {
  @Output() packagePurchased = new EventEmitter<UserCreditStatus>();

  packages: CreditPackage[] = [];
  userCreditStatus: UserCreditStatus | null = null;
  
  isLoadingPackages = false;
  isLoadingStatus = false;
  isPurchasing = false;

  constructor(
    private creditService: CreditService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    this.loadPackages();
    this.loadCreditStatus();
  }

  loadPackages() {
    this.isLoadingPackages = true;
    this.creditService.getCreditPackages().subscribe({
      next: (response) => {
        if (response.success) {
          this.packages = response.data;
        } else {
          this.notificationService.error('Failed to Load Packages', 
            response.error?.message || 'Unable to load credit packages.');
        }
      },
      error: (error) => {
        console.error('Failed to load packages:', error);
        this.notificationService.error('Connection Error', 
          'Unable to connect to the server. Please try again.');
      },
      complete: () => {
        this.isLoadingPackages = false;
      }
    });
  }

  loadCreditStatus() {
    this.isLoadingStatus = true;
    this.creditService.getCreditStatus().subscribe({
      next: (response) => {
        if (response.success) {
          this.userCreditStatus = response.data;
        }
      },
      error: (error) => {
        console.error('Failed to load credit status:', error);
        // Don't show error for status - user might not have credits yet
      },
      complete: () => {
        this.isLoadingStatus = false;
      }
    });
  }


  purchasePackage(pkg: CreditPackage) {
    this.isPurchasing = true;
    
    // For now, simulate purchase - in real implementation, this would integrate with payment processor
    const purchaseRequest = {
      packageId: pkg.id,
      paymentTransactionId: `demo_${Date.now()}` // Demo transaction ID
    };

    this.creditService.purchaseCreditPackage(purchaseRequest).subscribe({
      next: (response) => {
        if (response.success) {
          this.notificationService.success('Credits Purchased!', 
            `Successfully purchased ${pkg.totalCredits} credits for $${pkg.price}.`);
          
          // Reload credit status to get updated credit info
          this.loadCreditStatus();
          
          // Emit purchase event to parent component
          if (response.data.updatedCredits) {
            this.packagePurchased.emit({
              totalCredits: response.data.updatedCredits.totalCredits,
              weeklyCredits: response.data.updatedCredits.weeklyCredits,
              purchasedCredits: response.data.updatedCredits.purchasedCredits,
              lastCreditReset: this.userCreditStatus?.lastCreditReset || new Date().toISOString(),
              nextResetDate: this.userCreditStatus?.nextResetDate || new Date().toISOString()
            });
          }
        } else {
          this.notificationService.error('Purchase Failed', 
            response.error?.message || 'Failed to complete credit purchase.');
        }
      },
      error: (error) => {
        console.error('Purchase failed:', error);
        this.notificationService.error('Purchase Error', 
          'An error occurred during purchase. Please try again.');
      },
      complete: () => {
        this.isPurchasing = false;
      }
    });
  }


  getPackageRecommendation(pkg: CreditPackage): string {
    switch (pkg.name) {
      case 'Starter Pack':
        return 'Perfect for trying premium features';
      case 'Professional Pack':
        return 'Most popular - great value with bonus credits';
      case 'Studio Pack':
        return 'Best value for content creators';
      case 'Enterprise Pack':
        return 'Maximum credits for heavy users';
      default:
        return 'Professional AI-generated photos';
    }
  }

  isPackageRecommended(pkg: CreditPackage): boolean {
    return pkg.name === 'Professional Pack';
  }

  getCreditsPerDollar(pkg: CreditPackage): number {
    return Math.round((pkg.totalCredits / pkg.price) * 10) / 10;
  }

  getStyledGenerations(pkg: CreditPackage): number {
    return Math.floor(pkg.totalCredits / 5);
  }

  canAffordTraining(pkg: CreditPackage): boolean {
    return pkg.totalCredits >= 15;
  }
}