import {
  Component,
  ElementRef,
  EventEmitter,
  OnInit,
  Output,
  ViewChild,
  OnDestroy,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  CreditPackage,
  CreditService,
  PaymentConfig,
  UserCreditStatus,
} from '../../services/credit.service';
import { NotificationService } from '../../services/notification.service';
import { StripeService } from '../../services/stripe.service';
import { ThemeService } from '../../services/theme.service';
import { Stripe, StripeElements } from '@stripe/stripe-js';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-credit-packages',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './credit-packages.component.html',
  styleUrls: ['./credit-packages.component.sass'],
})
export class CreditPackagesComponent implements OnInit, OnDestroy {
  @Output() packagePurchased = new EventEmitter<UserCreditStatus>();
  @ViewChild('paymentElement') paymentElementRef!: ElementRef;

  packages: CreditPackage[] = [];
  userCreditStatus: UserCreditStatus | null = null;
  paymentConfig: PaymentConfig | null = null;

  isLoadingPackages = true; // Start with loading state
  isLoadingStatus = false;
  isPurchasing = false;

  stripe: Stripe | null = null;
  elements: StripeElements | undefined;
  selectedPackage: CreditPackage | null = null;

  private themeSubscription: Subscription | null = null;

  constructor(
    private creditService: CreditService,
    private notificationService: NotificationService,
    private stripeService: StripeService,
    private themeService: ThemeService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    // Subscribe to theme changes to ensure proper re-rendering
    this.themeSubscription = this.themeService.theme$.subscribe(() => {
      // Force change detection to ensure styles are updated
      setTimeout(() => {
        this.cdr.markForCheck();
        this.cdr.detectChanges();
      });
    });

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

  ngOnDestroy() {
    if (this.themeSubscription) {
      this.themeSubscription.unsubscribe();
    }
  }

  loadPackages() {
    this.isLoadingPackages = true;

    this.creditService.getCreditPackages().subscribe({
      next: response => {
        console.log('Credit packages response:', response);
        if (response && response.success) {
          this.packages = response.data || [];
          console.log('Loaded packages:', this.packages);
          if (this.packages.length === 0) {
            console.warn('No packages returned from API');
            this.notificationService.warning(
              'No Packages Available',
              'No credit packages are currently available.'
            );
          }
        } else {
          console.error('Failed to load packages:', response?.error);
          this.packages = [];
          this.notificationService.error(
            'Failed to Load Packages',
            response?.error?.message || 'Unable to load credit packages.'
          );
        }
      },
      error: error => {
        console.error('Error loading packages:', error);
        console.error('Error status:', error.status);
        console.error('Error message:', error.message);
        this.packages = [];

        if (error.status === 0) {
          this.notificationService.error(
            'Connection Error',
            'Unable to connect to the server. Please check if the server is running.'
          );
        } else if (error.status === 401) {
          this.notificationService.error('Authentication Error', 'Please log in to view packages.');
        } else if (error.status === 500) {
          this.notificationService.error(
            'Server Error',
            'Server error while loading packages. Please try again later.'
          );
        } else {
          this.notificationService.error(
            'Network Error',
            `Error ${error.status}: ${error.message || 'Please try again.'}`
          );
        }
      },
      complete: () => {
        this.isLoadingPackages = false;
        // Force change detection to ensure UI updates
        this.cdr.detectChanges();
      },
    });
  }

  loadCreditStatus() {
    this.isLoadingStatus = true;
    this.creditService.getCreditStatus().subscribe({
      next: response => {
        if (response.success) {
          this.userCreditStatus = response.data;
        }
      },
      error: error => {
        console.error('Failed to load credit status:', error);
      },
      complete: () => {
        this.isLoadingStatus = false;
      },
    });
  }

  loadPaymentConfig() {
    this.creditService.getPaymentConfig().subscribe({
      next: response => {
        if (response.success) {
          this.paymentConfig = response.data;
        }
      },
      error: error => {
        console.error('Failed to load payment config:', error);
        // Set default to not break the flow
        this.paymentConfig = {
          paymentSimulation: {
            enabled: false,
            skipStripeIntegration: false,
          },
        };
      },
    });
  }

  async purchasePackage(pkg: CreditPackage) {
    this.isPurchasing = true;
    this.selectedPackage = pkg;

    // Check if payment simulation is enabled
    if (
      this.paymentConfig?.paymentSimulation?.enabled &&
      this.paymentConfig?.paymentSimulation?.skipStripeIntegration
    ) {
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
      },
    });
  }

  async confirmPurchase() {
    if (!this.stripe || !this.elements) {
      return;
    }

    this.isPurchasing = true;
    const { error } = await this.stripe.confirmPayment({
      elements: this.elements,
      redirect: 'if_required',
    });

    if (error) {
      this.notificationService.error(
        'Payment Failed',
        error.message || 'An unknown error occurred.'
      );
      this.isPurchasing = false;
    } else {
      this.notificationService.success(
        'Payment Successful!',
        'Your payment was successful. Updating your credits...'
      );
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

      this.creditService
        .purchaseCreditPackage({
          packageId: pkg.id,
          paymentTransactionId: mockTransactionId,
        })
        .subscribe({
          next: response => {
            if (response.success) {
              this.notificationService.success(
                'Payment Simulated Successfully!',
                `Your payment simulation was successful. ${pkg.totalCredits} credits added to your account!`
              );
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
                  nextResetDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
                });
              }
            } else {
              this.notificationService.error(
                'Simulation Failed',
                response.error?.message || 'Payment simulation failed.'
              );
              this.isPurchasing = false;
            }
          },
          error: error => {
            this.notificationService.error('Simulation Error', 'Payment simulation failed.');
            this.isPurchasing = false;
          },
        });
    }, 2000); // 2 second delay to simulate processing
  }

  cancelPurchase() {
    this.selectedPackage = null;
  }

  isPackageRecommended(pkg: CreditPackage): boolean {
    return pkg.name === 'Professional Pack';
  }

  getPackageRecommendation(pkg: CreditPackage): string {
    if (pkg.name === 'Professional Pack') {
      return 'Most popular - great for professionals';
    }
    if (pkg.name === 'Starter Pack') {
      return 'Perfect for trying out custom training and styled generations';
    }
    if (pkg.name === 'Studio Pack') {
      return 'Best value for content creators and businesses';
    }
    if (pkg.name === 'Enterprise Pack') {
      return 'Maximum credits for agencies and enterprises';
    }
    return '';
  }

  getCreditsPerDollar(pkg: CreditPackage): number {
    return Math.round(pkg.totalCredits / pkg.price);
  }

  getStyledGenerations(pkg: CreditPackage): number {
    // Each styled generation costs 5 credits
    return Math.floor(pkg.totalCredits / 5);
  }

  canAffordTraining(pkg: CreditPackage): boolean {
    // Model training costs 15 credits
    return pkg.totalCredits >= 15;
  }
}
