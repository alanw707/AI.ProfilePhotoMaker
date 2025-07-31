import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
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
  changeDetection: ChangeDetectionStrategy.OnPush,
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

  private _themeSubscription: Subscription | null = null;

  constructor(
    private _creditService: CreditService,
    private _notificationService: NotificationService,
    private _stripeService: StripeService,
    private _themeService: ThemeService,
    private _cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    // Subscribe to theme changes to ensure proper re-rendering
    this._themeSubscription = this._themeService.theme$.subscribe(() => {
      // Force change detection to ensure styles are updated
      setTimeout(() => {
        this._cdr.markForCheck();
        this._cdr.detectChanges();
      });
    });

    this.loadPackages();
    this.loadCreditStatus();
    this.loadPaymentConfig();
    this._stripeService.getStripe().then(stripe => {
      this.stripe = stripe;
      if (!stripe && this.paymentConfig?.paymentSimulation?.enabled) {
        console.warn('Stripe.js not loaded - using payment simulation mode');
      }
    });
  }

  ngOnDestroy(): void {
    if (this._themeSubscription) {
      this._themeSubscription.unsubscribe();
    }
  }

  loadPackages(): void {
    this.isLoadingPackages = true;

    this._creditService.getCreditPackages().subscribe({
      next: response => this._handlePackagesResponse(response),
      error: error => this._handlePackagesError(error),
      complete: () => {
        this.isLoadingPackages = false;
        this._cdr.detectChanges();
      },
    });
  }

  private _handlePackagesResponse(response: any): void {
    if (response?.success) {
      this.packages = response.data || [];
      if (this.packages.length === 0) {
        this._notificationService.warning(
          'No Packages Available',
          'No credit packages are currently available.'
        );
      }
    } else {
      console.error('Failed to load packages:', response?.error);
      this.packages = [];
      this._notificationService.error(
        'Failed to Load Packages',
        response?.error?.message || 'Unable to load credit packages.'
      );
    }
  }

  private _handlePackagesError(error: any): void {
    console.error('Error loading packages:', error);
    this.packages = [];

    const errorMessage = this._getErrorMessage(error);
    this._notificationService.error(errorMessage.title, errorMessage.message);
  }

  private _getErrorMessage(error: any): { title: string; message: string } {
    if (error.status === 0) {
      return {
        title: 'Connection Error',
        message: 'Unable to connect to the server. Please check if the server is running.'
      };
    }
    
    if (error.status === 401) {
      console.warn('Unexpected 401 error - API endpoint should allow anonymous access');
      return {
        title: 'Access Error',
        message: 'Unable to load packages. Please try again later.'
      };
    }
    
    if (error.status === 500) {
      return {
        title: 'Server Error',
        message: 'Server error while loading packages. Please try again later.'
      };
    }
    
    return {
      title: 'Network Error',
      message: `Error ${error.status}: ${error.message || 'Please try again.'}`
    };
  }

  loadCreditStatus(): void {
    this.isLoadingStatus = true;
    this._creditService.getCreditStatus().subscribe({
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

  loadPaymentConfig(): void {
    this._creditService.getPaymentConfig().subscribe({
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

  async purchasePackage(pkg: CreditPackage): Promise<void> {
    this.isPurchasing = true;
    this.selectedPackage = pkg;

    if (this._shouldUsePaymentSimulation()) {
      this.simulatePayment(pkg);
      return;
    }

    if (!this._isStripeReady()) {
      this._notificationService.error('Payment Error', 'Stripe is not loaded yet.');
      this.isPurchasing = false;
      return;
    }

    this._processStripePayment(pkg);
  }

  private _shouldUsePaymentSimulation(): boolean {
    return !!(
      this.paymentConfig?.paymentSimulation?.enabled &&
      this.paymentConfig?.paymentSimulation?.skipStripeIntegration
    );
  }

  private _isStripeReady(): boolean {
    return !!(this.stripe || this.paymentConfig?.paymentSimulation?.enabled);
  }

  private _processStripePayment(pkg: CreditPackage): void {
    this._creditService.createPaymentIntent({ packageId: pkg.id }).subscribe({
      next: async (response: { success: boolean; data: { isSimulation: boolean; clientSecret: string } }) => {
        if (response.success) {
          if (response.data.isSimulation) {
            this.simulatePayment(pkg);
          } else {
            this._setupStripeElements(response.data.clientSecret);
          }
        } else {
          this._handlePaymentError('Could not create payment intent.');
        }
      },
      error: (_error: Error) => this._handlePaymentError('Could not create payment intent.'),
    });
  }

  private _setupStripeElements(clientSecret: string): void {
    this.elements = this.stripe?.elements({ clientSecret });
    const paymentElement = this.elements?.create('payment');
    paymentElement?.mount(this.paymentElementRef.nativeElement);
  }

  private _handlePaymentError(message: string): void {
    this._notificationService.error('Payment Error', message);
    this.isPurchasing = false;
  }

  async confirmPurchase(): Promise<void> {
    if (!this.stripe || !this.elements) {
      return;
    }

    this.isPurchasing = true;
    const { error } = await this.stripe.confirmPayment({
      elements: this.elements,
      redirect: 'if_required',
    });

    if (error) {
      this._notificationService.error(
        'Payment Failed',
        error.message || 'An unknown error occurred.'
      );
      this.isPurchasing = false;
    } else {
      this._notificationService.success(
        'Payment Successful!',
        'Your payment was successful. Updating your credits...'
      );
      this.isPurchasing = false;
      this.selectedPackage = null;
      // The backend will handle the credit update via webhooks, so we just need to reload the status.
      this.loadCreditStatus();
    }
  }

  simulatePayment(pkg: CreditPackage): void {
    // Simulate payment processing delay
    setTimeout(() => {
      // Call the purchase endpoint directly with a simulated transaction ID
      const mockTransactionId = `sim_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

      this._creditService
        .purchaseCreditPackage({
          packageId: pkg.id,
          paymentTransactionId: mockTransactionId,
        })
        .subscribe({
          next: response => {
            if (response.success) {
              this._notificationService.success(
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
              this._notificationService.error(
                'Simulation Failed',
                response.error?.message || 'Payment simulation failed.'
              );
              this.isPurchasing = false;
            }
          },
          error: _error => {
            this._notificationService.error('Simulation Error', 'Payment simulation failed.');
            this.isPurchasing = false;
          },
        });
    }, 2000); // 2 second delay to simulate processing
  }

  cancelPurchase(): void {
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

  trackByPackageId(_index: number, pkg: CreditPackage): string {
    return pkg.id;
  }
}
