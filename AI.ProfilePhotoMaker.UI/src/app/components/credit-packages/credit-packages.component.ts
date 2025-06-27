import { Component, OnInit, Output, EventEmitter, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CreditService, CreditPackage, UserCreditStatus, PaymentConfig } from '../../services/credit.service';
import { NotificationService } from '../../services/notification.service';
import { StripeService } from '../../services/stripe.service';
import { Stripe, StripeElements } from '@stripe/stripe-js';

@Component({
  selector: 'app-credit-packages',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="credit-packages-container">
      <div class="header">
        <h2>Purchase Credits</h2>
        <p class="subtitle">Choose a credit package to unlock premium features</p>
        <div class="simulation-notice" *ngIf="paymentConfig?.paymentSimulation?.enabled">
          <small><strong>Development Mode:</strong> Payment simulation is enabled - no real charges will be made.</small>
        </div>
      </div>

      <!-- Payment Element -->
      <div class="payment-container" *ngIf="selectedPackage">
        <h3>{{paymentConfig?.paymentSimulation?.enabled ? 'Simulating Payment' : 'Completing Purchase'}} for {{selectedPackage.name}}</h3>
        <div class="simulation-payment" *ngIf="paymentConfig?.paymentSimulation?.enabled">
          <p>Payment simulation in progress...</p>
          <p><small>This is a simulated payment - no real money will be charged.</small></p>
        </div>
        <div #paymentElement></div>
        <div class="payment-actions">
          <button (click)="confirmPurchase()" [disabled]="isPurchasing">
            {{ isPurchasing ? 'Processing...' : 'Pay 
 + selectedPackage.price }}
          </button>
          <button (click)="cancelPurchase()" [disabled]="isPurchasing">Cancel</button>
        </div>
      </div>

      <!-- Credit Packages -->
      <div class="packages-grid" *ngIf="!isLoadingPackages && !selectedPackage">
        <div 
          class="package-card" 
          [class.recommended]="isPackageRecommended(pkg)"
          *ngFor="let pkg of packages"
        >
          <div class="recommended-badge" *ngIf="isPackageRecommended(pkg)">
            Most Popular
          </div>
          <div class="package-header">
            <h3 class="package-name">{{ pkg.name }}</h3>
            <div class="package-price">
              <span class="currency">$</span>
              <span class="amount">{{ pkg.price }}</span>
            </div>
            <p class="package-description">{{ pkg.description }}</p>
          </div>
          <div class="credit-info">
            <div class="credit-highlight">
              <span class="total-credits">{{ pkg.totalCredits }}</span>
              <span class="credit-label">Total Credits</span>
            </div>
            <div class="value-proposition" *ngIf="pkg.bonusCredits > 0">
              <div class="bonus-highlight">
                <span>+ {{ pkg.bonusCredits }} bonus credits</span>
              </div>
            </div>
          </div>
          <div class="package-actions">
            <button 
              class="purchase-btn"
              [disabled]="isPurchasing"
              (click)="purchasePackage(pkg)"
            >
              {{ isPurchasing ? 'Processing...' : 'Purchase Credits' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styleUrls: ['./credit-packages.component.sass']
})
export class CreditPackagesComponent implements OnInit {
  @Output() packagePurchased = new EventEmitter<UserCreditStatus>();
  @ViewChild('paymentElement') paymentElementRef!: ElementRef;

  packages: CreditPackage[] = [];
  userCreditStatus: UserCreditStatus | null = null;
  paymentConfig: PaymentConfig | null = null;
  
  isLoadingPackages = false;
  isLoadingStatus = false;
  isPurchasing = false;

  stripe: Stripe | null = null;
  elements: StripeElements | undefined;
  selectedPackage: CreditPackage | null = null;

  constructor(
    private creditService: CreditService,
    private notificationService: NotificationService,
    private stripeService: StripeService
  ) {}

  ngOnInit() {
    this.loadPackages();
    this.loadCreditStatus();
    this.loadPaymentConfig();
    this.stripeService.getStripe().then(stripe => {
      this.stripe = stripe;
      if (!stripe && this.paymentConfig?.paymentSimulation?.enabled) {
        console.log('Stripe.js not loaded - using payment simulation mode');
      }
    });
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
      },
      complete: () => {
        this.isLoadingStatus = false;
      }
    });
  }

  loadPaymentConfig() {
    this.creditService.getPaymentConfig().subscribe({
      next: (response) => {
        if (response.success) {
          this.paymentConfig = response.data;
        }
      },
      error: (error) => {
        console.error('Failed to load payment config:', error);
        // Set default to not break the flow
        this.paymentConfig = {
          paymentSimulation: {
            enabled: false,
            skipStripeIntegration: false
          }
        };
      }
    });
  }

  async purchasePackage(pkg: CreditPackage) {
    this.isPurchasing = true;
    this.selectedPackage = pkg;

    // Check if payment simulation is enabled
    if (this.paymentConfig?.paymentSimulation?.enabled && this.paymentConfig?.paymentSimulation?.skipStripeIntegration) {
      // Skip Stripe integration and simulate payment
      this.simulatePayment(pkg);
      return;
    }

    // Original Stripe flow
    if (!this.stripe && !this.paymentConfig?.paymentSimulation?.enabled) {
      this.notificationService.error('Payment Error', 'Stripe is not loaded yet.');
      this.isPurchasing = false;
      return;
    }

    this.creditService.createPaymentIntent({ packageId: pkg.id }).subscribe({
      next: async (response: any) => {
        if (response.success) {
          if (response.data.isSimulation) {
            // Backend is in simulation mode
            this.simulatePayment(pkg);
          } else {
            // Real Stripe integration
            this.elements = this.stripe?.elements({ clientSecret: response.data.clientSecret });
            const paymentElement = this.elements?.create('payment');
            paymentElement?.mount(this.paymentElementRef.nativeElement);
          }
        } else {
          this.notificationService.error('Payment Error', 'Could not create payment intent.');
          this.isPurchasing = false;
        }
      },
      error: (error: any) => {
        this.notificationService.error('Payment Error', 'Could not create payment intent.');
        this.isPurchasing = false;
      }
    });
  }

  async confirmPurchase() {
    if (!this.stripe || !this.elements) {
      return;
    }

    this.isPurchasing = true;
    const { error } = await this.stripe.confirmPayment({
      elements: this.elements,
      redirect: 'if_required'
    });

    if (error) {
      this.notificationService.error('Payment Failed', error.message || 'An unknown error occurred.');
      this.isPurchasing = false;
    } else {
      this.notificationService.success('Payment Successful!', 'Your payment was successful. Updating your credits...');
      this.isPurchasing = false;
      this.selectedPackage = null;
      // The backend will handle the credit update via webhooks, so we just need to reload the status.
      this.loadCreditStatus();
    }
  }

  simulatePayment(pkg: CreditPackage) {
    // Simulate payment processing delay
    setTimeout(() => {
      // Call the purchase endpoint directly with a simulated transaction ID
      const mockTransactionId = `sim_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
      
      this.creditService.purchaseCreditPackage({
        packageId: pkg.id,
        paymentTransactionId: mockTransactionId
      }).subscribe({
        next: (response) => {
          if (response.success) {
            this.notificationService.success('Payment Simulated Successfully!', 
              `Your payment simulation was successful. ${pkg.totalCredits} credits added to your account!`);
            this.isPurchasing = false;
            this.selectedPackage = null;
            // Reload credit status and emit the event
            this.loadCreditStatus();
            if (response.data.updatedCredits) {
              this.packagePurchased.emit({
                totalCredits: response.data.updatedCredits.totalCredits,
                weeklyCredits: response.data.updatedCredits.weeklyCredits,
                purchasedCredits: response.data.updatedCredits.purchasedCredits,
                lastCreditReset: new Date().toISOString(),
                nextResetDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString()
              });
            }
          } else {
            this.notificationService.error('Simulation Failed', response.error?.message || 'Payment simulation failed.');
            this.isPurchasing = false;
          }
        },
        error: (error) => {
          this.notificationService.error('Simulation Error', 'Payment simulation failed.');
          this.isPurchasing = false;
        }
      });
    }, 2000); // 2 second delay to simulate processing
  }

  cancelPurchase() {
    this.selectedPackage = null;
  }

  isPackageRecommended(pkg: CreditPackage): boolean {
    return pkg.name === 'Professional Pack';
  }
}
