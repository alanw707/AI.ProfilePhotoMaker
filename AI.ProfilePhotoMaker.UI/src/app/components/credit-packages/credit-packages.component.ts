import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  OnDestroy,
  OnInit,
  Output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  CreditPackage,
  CreditService,
  CreatePaymentIntentResponse,
  PaymentConfig,
  UserCreditStatus,
} from '../../services/credit.service';
import { NotificationService } from '../../services/notification.service';
import { ThemeService } from '../../services/theme.service';
import { Subscription } from 'rxjs';
import { FormsModule } from '@angular/forms';
import {
  loadStripe,
  Stripe,
  StripeElements,
  StripeCardElement,
  StripeCardElementChangeEvent,
  StripeCardElementOptions,
  PaymentMethodCreateParams,
} from '@stripe/stripe-js';

interface BillingDetailsForm {
  name: string;
  email: string;
  addressLine1: string;
  addressLine2: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
}

@Component({
  selector: 'app-credit-packages',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './credit-packages.component.html',
  styleUrls: ['./credit-packages.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreditPackagesComponent implements OnInit, OnDestroy {
  @Output() packagePurchased = new EventEmitter<UserCreditStatus>();

  packages: CreditPackage[] = [];
  userCreditStatus: UserCreditStatus | null = null;
  paymentConfig: PaymentConfig | null = null;

  isLoadingPackages = true;
  isLoadingStatus = false;
  isPurchasing = false; // legacy flag used by simulation flow

  selectedPackage: CreditPackage | null = null;
  isStripePaymentActive = false;
  isLoadingIntent = false;
  isConfirmingPayment = false;
  stripeErrorMessage: string | null = null;
  stripeTransactionId: string | null = null;
  billingDetails: BillingDetailsForm = this._createEmptyBillingDetails();
  billingDetailsTouched = false;
  isCardElementFocused = false;
  isAwaitingWebhookConfirmation = false;
  private _pendingPaymentTransactionId: string | null = null;
  private _pendingPaymentPackageId: number | null = null;
  private _pendingPaymentAttempts = 0;
  private _pendingPaymentTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly _maxPendingPaymentAttempts = 6;
  private readonly _initialPendingDelayMs = 3000;
  private readonly _pendingRetryDelayMs = 5000;
  private _stripeClientSecret: string | null = null;
  private _stripePublishableKey: string | null = null;
  private readonly _emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  private readonly _countryCodePattern = /^[A-Za-z]{2}$/;

  private _themeSubscription: Subscription | null = null;
  private _stripe: Stripe | null = null;
  private _stripeElements: StripeElements | null = null;
  private _stripeCardElement: StripeCardElement | null = null;

  constructor(
    private _creditService: CreditService,
    private _notificationService: NotificationService,
    private _themeService: ThemeService,
    private _cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this._themeSubscription = this._themeService.theme$.subscribe(() => {
      setTimeout(() => {
        this._cdr.markForCheck();
        this._cdr.detectChanges();
      });
    });

    this.loadPackages();
    this.loadCreditStatus();
    this.loadPaymentConfig();
  }

  ngOnDestroy(): void {
    if (this._themeSubscription) {
      this._themeSubscription.unsubscribe();
    }

    this._clearPendingPaymentWatch();
    this._teardownStripeElements();
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

  private _handlePackagesResponse(response: {
    success?: boolean;
    data?: unknown;
    error?: unknown;
  }): void {
    if (response?.success) {
      this.packages = Array.isArray(response.data) ? response.data : [];
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
        (response?.error as any)?.message || 'Unable to load credit packages.'
      );
    }
  }

  private _handlePackagesError(error: {
    status?: number;
    message?: string;
    error?: { message?: string };
  }): void {
    console.error('Error loading packages:', error);
    this.packages = [];

    const errorMessage = this._getErrorMessage(error);
    this._notificationService.error(errorMessage.title, errorMessage.message);
  }

  private _getErrorMessage(error: {
    status?: number;
    message?: string;
    error?: { message?: string };
  }): { title: string; message: string } {
    if (error.status === 0) {
      return {
        title: 'Connection Error',
        message: 'Unable to connect to the server. Please check if the server is running.',
      };
    }

    if (error.status === 401) {
      console.warn('Unexpected 401 error - API endpoint should allow anonymous access');
      return {
        title: 'Access Error',
        message: 'Unable to load packages. Please try again later.',
      };
    }

    if (error.status === 500) {
      return {
        title: 'Server Error',
        message: 'Server error while loading packages. Please try again later.',
      };
    }

    return {
      title: 'Network Error',
      message: `Error ${error.status}: ${error.message || 'Please try again.'}`,
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
    this.selectedPackage = pkg;
    this.stripeErrorMessage = null;
    this.isPurchasing = false;
    this.isAwaitingWebhookConfirmation = false;
    this._clearPendingPaymentWatch();

    if (this._shouldUsePaymentSimulation()) {
      this.isPurchasing = true;
      this.simulatePayment(pkg);
      return;
    }

    this.isStripePaymentActive = true;
    this.isLoadingIntent = true;
    this._cdr.markForCheck();

    this._creditService.createPaymentIntent({ packageId: pkg.id }).subscribe({
      next: async response => {
        if (!response.success) {
          this._handlePaymentError(
            response.error?.message || 'Unable to start payment with Stripe.'
          );
          return;
        }

        try {
          this.isLoadingIntent = false;
          this._cdr.detectChanges();
          await this._initializeStripe(response.data);
          this._cdr.detectChanges();
        } catch (error) {
          console.error('Stripe initialization failed', error);
          this._handlePaymentError(
            error instanceof Error ? error.message : 'Unable to initialize Stripe payment.'
          );
        }
      },
      error: error => {
        console.error('createPaymentIntent error', error);
        this._handlePaymentError('Unable to initialize payment. Please try again.');
      },
    });
  }

  private _shouldUsePaymentSimulation(): boolean {
    return !!(
      this.paymentConfig?.paymentSimulation?.enabled &&
      this.paymentConfig?.paymentSimulation?.skipStripeIntegration
    );
  }

  private async _initializeStripe(intent: CreatePaymentIntentResponse): Promise<void> {
    if (!intent.publishableKey || !intent.clientSecret) {
      throw new Error('Stripe configuration missing publishable key or client secret.');
    }

    if (!this._stripe || this._stripePublishableKey !== intent.publishableKey) {
      this._stripe = await loadStripe(intent.publishableKey);
      if (!this._stripe) {
        throw new Error('Failed to load Stripe.js.');
      }

      this._stripePublishableKey = intent.publishableKey;
    }

    if (!this._stripe) {
      throw new Error('Unable to initialize Stripe.');
    }

    const isDarkTheme = this._themeService.isDark();

    this._stripeElements = this._stripe.elements({
      appearance: {
        theme: isDarkTheme ? 'night' : 'flat',
        variables: {
          colorPrimary: '#55d0ff',
          colorText: isDarkTheme ? '#f4f8ff' : '#0f172a',
          colorTextSecondary: isDarkTheme ? '#a9b7d1' : '#475569',
          colorDanger: '#ff6b6b',
          borderRadius: '12px',
          fontFamily: 'Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
        },
      },
    });

    if (!this._stripeElements) {
      throw new Error('Unable to initialize Stripe elements.');
    }

    this._teardownStripeCardElement();

    const baseTextColor = isDarkTheme ? '#f4f8ff' : '#0f172a';
    const placeholderColor = isDarkTheme ? 'rgba(175, 192, 220, 0.7)' : 'rgba(105, 125, 155, 0.7)';
    const completionColor = isDarkTheme ? '#7ef29d' : '#1ea672';

    const cardElementOptions: StripeCardElementOptions = {
      hidePostalCode: true,
      style: {
        base: {
          color: baseTextColor,
          fontSize: '16px',
          fontFamily: 'Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
          fontSmoothing: 'antialiased',
          letterSpacing: '0.4px',
          lineHeight: '24px',
          iconColor: '#55d0ff',
          backgroundColor: 'transparent',
          // eslint-disable-next-line @typescript-eslint/naming-convention -- Stripe style token uses CSS syntax
          '::placeholder': {
            color: placeholderColor,
          },
        },
        empty: {
          color: baseTextColor,
          // eslint-disable-next-line @typescript-eslint/naming-convention -- Stripe style token uses CSS syntax
          '::placeholder': {
            color: placeholderColor,
          },
        },
        invalid: {
          color: '#ff8080',
          iconColor: '#ff8080',
        },
        complete: {
          color: completionColor,
          iconColor: completionColor,
        },
      },
    };

    this._stripeCardElement = this._stripeElements.create('card', cardElementOptions);

    const mountTarget = await this._waitForCardElementContainer();

    this.isCardElementFocused = false;
    this._stripeCardElement.mount(mountTarget);
    this._stripeCardElement.on('focus', () => {
      this.isCardElementFocused = true;
      this._cdr.markForCheck();
    });
    this._stripeCardElement.on('blur', () => {
      this.isCardElementFocused = false;
      this._cdr.markForCheck();
    });
    this._stripeCardElement.on('change', (event: StripeCardElementChangeEvent) => {
      this.stripeErrorMessage = event.error?.message || null;
      this._cdr.markForCheck();
    });

    this.billingDetailsTouched = false;
    this.stripeErrorMessage = null;
    this._stripeClientSecret = intent.clientSecret;
    this.stripeTransactionId = intent.paymentTransactionId ?? null;
    this._cdr.detectChanges();
  }

  async confirmStripePayment(): Promise<void> {
    this.billingDetailsTouched = true;

    if (!this.isBillingDetailsValid) {
      this.stripeErrorMessage = 'Please complete the required billing details before submitting.';
      this._cdr.markForCheck();
      return;
    }

    if (
      !this._stripe ||
      !this._stripeCardElement ||
      !this._stripeClientSecret ||
      !this.selectedPackage
    ) {
      this._handlePaymentError('Stripe is not initialized. Please refresh and try again.');
      return;
    }

    this.isConfirmingPayment = true;
    this.stripeErrorMessage = null;
    this._cdr.markForCheck();

    const billingDetails = this._buildBillingDetails();

    const { error, paymentIntent } = await this._stripe.confirmCardPayment(
      this._stripeClientSecret,
      {
        // eslint-disable-next-line @typescript-eslint/naming-convention
        payment_method: {
          card: this._stripeCardElement,
          // eslint-disable-next-line @typescript-eslint/naming-convention -- Stripe requires snake_case keys
          billing_details: billingDetails,
        },
      }
    );

    if (error) {
      console.error('Stripe confirmCardPayment error', error);
      this.stripeErrorMessage = error.message || 'Payment failed, please try again.';
      this.isConfirmingPayment = false;
      this._cdr.detectChanges();
      return;
    }

    this._notificationService.success(
      'Payment Submitted',
      'Payment received. Finalizing your credits now.'
    );

    const paymentTransactionId = this.stripeTransactionId || paymentIntent?.id || undefined;

    this._completePurchase(paymentTransactionId);
  }

  private _completePurchase(paymentTransactionId?: string): void {
    if (!this.selectedPackage) {
      return;
    }

    this._pendingPaymentTransactionId = paymentTransactionId ?? this.stripeTransactionId;
    this._pendingPaymentPackageId = this.selectedPackage.id;
    this._pendingPaymentAttempts = 0;

    this._creditService
      .purchaseCreditPackage({
        packageId: this.selectedPackage.id,
        paymentTransactionId: this._pendingPaymentTransactionId ?? paymentTransactionId,
      })
      .subscribe({
        next: response => {
          this.isConfirmingPayment = false;

          if (response.success) {
            this._clearPendingPaymentWatch();
            this.isAwaitingWebhookConfirmation = false;
            this._notificationService.success(
              'Credits Added',
              `${this.selectedPackage?.totalCredits ?? ''} credits added to your account!`
            );
            this._finalizeSuccess(response.data.updatedCredits ?? null);
            return;
          }

          const errorCode = (response as any)?.error?.code;
          if (errorCode === 'PaymentPending') {
            this.isAwaitingWebhookConfirmation = true;
            this._notificationService.info(
              'Payment Processing',
              "Stripe is still finalizing your payment. We'll retry automatically in the background."
            );
            this._startPendingPaymentWatch();
            this._cdr.detectChanges();
            return;
          }

          this._clearPendingPaymentWatch();
          this._handlePaymentError(response.error?.message || 'Payment failed. Please try again.');
        },
        error: error => {
          console.error('purchaseCreditPackage error', error);
          this._clearPendingPaymentWatch();
          this._handlePaymentError('Unable to finalize purchase. Please try again.');
        },
      });
  }

  private _startPendingPaymentWatch(): void {
    if (!this._pendingPaymentTransactionId || !this._pendingPaymentPackageId) {
      return;
    }

    this._cancelPendingPaymentTimer();
    this._pendingPaymentAttempts = 0;
    this._schedulePendingPaymentCheck();
  }

  private _schedulePendingPaymentCheck(): void {
    if (!this._pendingPaymentTransactionId || !this._pendingPaymentPackageId) {
      return;
    }

    const delay =
      this._pendingPaymentAttempts === 0 ? this._initialPendingDelayMs : this._pendingRetryDelayMs;

    this._pendingPaymentTimer = setTimeout(() => {
      this._pendingPaymentAttempts += 1;
      console.debug('Polling Stripe payment status', {
        attempt: this._pendingPaymentAttempts,
        transactionId: this._pendingPaymentTransactionId,
      });

      this._creditService
        .purchaseCreditPackage({
          packageId: this._pendingPaymentPackageId!,
          paymentTransactionId: this._pendingPaymentTransactionId!,
        })
        .subscribe({
          next: response => {
            if (response.success) {
              this._notificationService.success(
                'Credits Added',
                `${this.selectedPackage?.totalCredits ?? ''} credits added to your account!`
              );
              this._finalizeSuccess(response.data.updatedCredits ?? null);
              this._clearPendingPaymentWatch();
              return;
            }

            const errorCode = (response as any)?.error?.code;
            if (
              errorCode === 'PaymentPending' &&
              this._pendingPaymentAttempts < this._maxPendingPaymentAttempts
            ) {
              this._schedulePendingPaymentCheck();
              return;
            }

            if (errorCode === 'PaymentPending') {
              this._notificationService.info(
                'Payment Processing',
                'Stripe is still confirming your payment. Credits will update shortly.'
              );
              this.isAwaitingWebhookConfirmation = false;
              this._resetStripeState();
              this.selectedPackage = null;
              this._cdr.detectChanges();
              return;
            }

            this._clearPendingPaymentWatch();
            this._handlePaymentError(
              response.error?.message || 'Unable to finalize purchase. Please contact support.'
            );
          },
          error: error => {
            console.error('purchaseCreditPackage polling error', error);

            if (this._pendingPaymentAttempts < this._maxPendingPaymentAttempts) {
              this._schedulePendingPaymentCheck();
              return;
            }

            this._clearPendingPaymentWatch();
            this._handlePaymentError('Unable to confirm payment status. Please try again.');
          },
        });
    }, delay);
  }

  private _cancelPendingPaymentTimer(): void {
    if (this._pendingPaymentTimer) {
      clearTimeout(this._pendingPaymentTimer);
      this._pendingPaymentTimer = null;
    }
  }

  private _clearPendingPaymentWatch(): void {
    this._cancelPendingPaymentTimer();
    this._pendingPaymentAttempts = 0;
    this._pendingPaymentTransactionId = null;
    this._pendingPaymentPackageId = null;
  }

  private _finalizeSuccess(updatedCredits: UserCreditStatus | null): void {
    this.loadCreditStatus();

    if (updatedCredits) {
      this.packagePurchased.emit(updatedCredits);
    }

    this._resetStripeState();
    this.selectedPackage = null;
    this._cdr.detectChanges();
  }

  private _resetStripeState(): void {
    this.isStripePaymentActive = false;
    this.isLoadingIntent = false;
    this.isConfirmingPayment = false;
    this.isPurchasing = false;
    this.isAwaitingWebhookConfirmation = false;
    this.stripeErrorMessage = null;
    this.stripeTransactionId = null;
    this._stripeClientSecret = null;
    this.isCardElementFocused = false;
    this._clearPendingPaymentWatch();
    this._resetBillingDetails();
    this._teardownStripeCardElement();
  }

  private _handlePaymentError(message: string): void {
    this._notificationService.error('Payment Error', message);
    this.isConfirmingPayment = false;
    this.isLoadingIntent = false;
    this.isPurchasing = false;
    this.isAwaitingWebhookConfirmation = false;
    this._clearPendingPaymentWatch();
    this._cdr.detectChanges();
  }

  simulatePayment(pkg: CreditPackage): void {
    setTimeout(() => {
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
                `${pkg.totalCredits} credits added to your account!`
              );
              this.isPurchasing = false;
              this.selectedPackage = null;
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
              return;
            }

            if ((response as any)?.error?.code === 'PaymentPending') {
              this._notificationService.info(
                'Payment Processing',
                'Payment is still processing. We will refresh your credits once it completes.'
              );
              this.isPurchasing = false;
              return;
            }

            this._notificationService.error(
              'Simulation Failed',
              response.error?.message || 'Payment simulation failed.'
            );
            this.isPurchasing = false;
          },
          error: _error => {
            this._notificationService.error('Simulation Error', 'Payment simulation failed.');
            this.isPurchasing = false;
          },
        });
    }, 2000);
  }

  cancelPurchase(): void {
    this.selectedPackage = null;
    this._resetStripeState();
  }

  onCountryCodeChange(value: string): void {
    this.billingDetails.country = (value || '').toUpperCase();
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
    return Math.floor(pkg.totalCredits / 5);
  }

  canAffordTraining(pkg: CreditPackage): boolean {
    return pkg.totalCredits >= 15;
  }

  get isBillingDetailsValid(): boolean {
    return this._isBillingDetailsValid();
  }

  get billingEmailIsValid(): boolean {
    const email = this.billingDetails.email.trim();
    return !!email && this._emailRegex.test(email);
  }

  get billingCountryIsValid(): boolean {
    const country = this.billingDetails.country.trim();
    return !!country && this._countryCodePattern.test(country);
  }

  get isSimulationMode(): boolean {
    return this._shouldUsePaymentSimulation();
  }

  trackByPackageId(_index: number, pkg: CreditPackage): number {
    return pkg.id;
  }

  private _isBillingDetailsValid(): boolean {
    const { name, email, addressLine1, city, postalCode, country } = this.billingDetails;

    if (
      !name.trim() ||
      !email.trim() ||
      !addressLine1.trim() ||
      !city.trim() ||
      !postalCode.trim() ||
      !country.trim()
    ) {
      return false;
    }

    if (!this._emailRegex.test(email.trim())) {
      return false;
    }

    if (!this._countryCodePattern.test(country.trim())) {
      return false;
    }

    return true;
  }

  private _buildBillingDetails(): PaymentMethodCreateParams.BillingDetails {
    const countryCode = this.billingDetails.country.trim().toUpperCase();

    return {
      name: this.billingDetails.name.trim(),
      email: this.billingDetails.email.trim(),
      address: {
        line1: this.billingDetails.addressLine1.trim(),
        line2: this.billingDetails.addressLine2.trim() || undefined,
        city: this.billingDetails.city.trim(),
        state: this.billingDetails.state.trim() || undefined,
        // eslint-disable-next-line @typescript-eslint/naming-convention -- Stripe expects snake_case address keys
        postal_code: this.billingDetails.postalCode.trim(),
        country: countryCode,
      },
    };
  }

  private _resetBillingDetails(): void {
    this.billingDetails = this._createEmptyBillingDetails();
    this.billingDetailsTouched = false;
  }

  private _createEmptyBillingDetails(): BillingDetailsForm {
    return {
      name: '',
      email: '',
      addressLine1: '',
      addressLine2: '',
      city: '',
      state: '',
      postalCode: '',
      country: 'US',
    };
  }

  private async _waitForCardElementContainer(retries = 10, delayMs = 50): Promise<HTMLElement> {
    for (let attempt = 0; attempt < retries; attempt++) {
      const element = document.getElementById('card-element');
      if (element) {
        return element;
      }
      await new Promise(resolve => setTimeout(resolve, delayMs));
    }

    throw new Error(
      'Unable to locate the Stripe card element container. Please retry the purchase.'
    );
  }

  private _teardownStripeCardElement(): void {
    if (this._stripeCardElement) {
      this._stripeCardElement.destroy();
      this._stripeCardElement = null;
    }
  }

  private _teardownStripeElements(): void {
    this._teardownStripeCardElement();
    this._stripeElements = null;
    this._stripe = null;
    this._stripePublishableKey = null;
  }
}
