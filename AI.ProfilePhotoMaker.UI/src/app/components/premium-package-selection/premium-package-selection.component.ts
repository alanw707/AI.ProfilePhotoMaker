import { Component, OnInit, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PremiumPackageService, PremiumPackage, UserPackageStatus } from '../../services/premium-package.service';
import { NotificationService } from '../../services/notification.service';

@Component({
  selector: 'app-premium-package-selection',
  imports: [CommonModule],
  templateUrl: './premium-package-selection.component.html',
  styleUrl: './premium-package-selection.component.sass'
})
export class PremiumPackageSelectionComponent implements OnInit {
  @Output() packageSelected = new EventEmitter<PremiumPackage>();
  @Output() packagePurchased = new EventEmitter<UserPackageStatus>();

  packages: PremiumPackage[] = [];
  userPackageStatus: UserPackageStatus | null = null;
  selectedPackage: PremiumPackage | null = null;
  
  isLoadingPackages = false;
  isLoadingStatus = false;
  isPurchasing = false;

  constructor(
    private premiumPackageService: PremiumPackageService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    this.loadPackages();
    this.loadUserStatus();
  }

  loadPackages() {
    this.isLoadingPackages = true;
    this.premiumPackageService.getActivePackages().subscribe({
      next: (response) => {
        if (response.success) {
          this.packages = response.data;
        } else {
          this.notificationService.error('Failed to Load Packages', 
            response.error?.message || 'Unable to load premium packages.');
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

  loadUserStatus() {
    this.isLoadingStatus = true;
    this.premiumPackageService.getUserPackageStatus().subscribe({
      next: (response) => {
        if (response.success) {
          this.userPackageStatus = response.data;
        }
      },
      error: (error) => {
        console.error('Failed to load user status:', error);
        // Don't show error for status - user might not have a package yet
      },
      complete: () => {
        this.isLoadingStatus = false;
      }
    });
  }

  selectPackage(pkg: PremiumPackage) {
    this.selectedPackage = pkg;
    this.packageSelected.emit(pkg);
  }

  purchasePackage(pkg: PremiumPackage) {
    if (this.userPackageStatus?.hasActivePackage) {
      this.notificationService.warning('Active Package Found', 
        'You already have an active premium package. Please use your existing credits first.');
      return;
    }

    this.isPurchasing = true;
    
    // For now, simulate purchase - in real implementation, this would integrate with payment processor
    const purchaseRequest = {
      packageId: pkg.id,
      paymentTransactionId: `demo_${Date.now()}` // Demo transaction ID
    };

    this.premiumPackageService.purchasePackage(purchaseRequest).subscribe({
      next: (response) => {
        if (response.success) {
          this.notificationService.success('Package Purchased!', 
            `Successfully purchased ${pkg.name} package with ${pkg.credits} credits.`);
          
          // Reload user status to get updated package info
          this.loadUserStatus();
          
          // Emit purchase event to parent component
          this.packagePurchased.emit({
            hasActivePackage: true,
            packageId: pkg.id,
            packageName: pkg.name,
            creditsRemaining: pkg.credits,
            modelExpired: false,
            daysUntilExpiration: 7
          });
        } else {
          this.notificationService.error('Purchase Failed', 
            response.error?.message || 'Failed to complete package purchase.');
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

  getPackageFeatures(pkg: PremiumPackage): string[] {
    const totalImages = pkg.maxStyles * pkg.maxImagesPerStyle;
    const features = [
      `${pkg.credits} total credits`,
      `${pkg.maxStyles} professional photo styles`,
      `${totalImages} total images`,
      '1 custom AI model training',
      '7-day model access',
      '1024x1024 photo resolution',
      'Get your Photos Generated in Minutes'
    ];
    
    return features;
  }

  getPackageRecommendation(pkg: PremiumPackage): string {
    switch (pkg.name) {
      case 'Quick Shot':
        return 'Perfect for LinkedIn profile updates';
      case 'Professional':
        return 'Most popular - great for job seekers';
      case 'Premium Studio':
        return 'Best value for content creators';
      case 'Ultimate':
        return 'Complete professional photo package';
      default:
        return 'Professional AI-generated photos';
    }
  }

  isPackageRecommended(pkg: PremiumPackage): boolean {
    return pkg.name === 'Professional';
  }
}
