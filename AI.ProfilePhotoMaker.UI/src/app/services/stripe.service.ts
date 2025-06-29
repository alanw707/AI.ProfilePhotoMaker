import { Injectable } from '@angular/core';
import { loadStripe, Stripe } from '@stripe/stripe-js';
import { CreditService } from './credit.service';

@Injectable({
  providedIn: 'root'
})
export class StripeService {
  private stripePromise: Promise<Stripe | null> | null = null;
  private paymentConfigLoaded = false;

  constructor(private creditService: CreditService) {}

  private async loadStripe(): Promise<Stripe | null> {
    try {
      // Check payment configuration first
      const configResponse = await this.creditService.getPaymentConfig().toPromise();
      
      if (configResponse?.success && configResponse.data.paymentSimulation?.enabled) {
        console.log('Payment simulation enabled - skipping Stripe.js loading');
        return null; // Don't load Stripe in simulation mode
      }
      
      // Load Stripe only if not in simulation mode
      const publishableKey = 'pk_test_51PWTf5Rrx34A3nCfA5lS45Ayj5tS4aL2g3v4w5x6y7z8a9b0c1d2e3f4g5h6i7j8k9l0m';
      return loadStripe(publishableKey);
    } catch (error) {
      console.error('Failed to load payment config, defaulting to Stripe loading:', error);
      // Fallback to loading Stripe if config fails
      const publishableKey = 'pk_test_51PWTf5Rrx34A3nCfA5lS45Ayj5tS4aL2g3v4w5x6y7z8a9b0c1d2e3f4g5h6i7j8k9l0m';
      return loadStripe(publishableKey);
    }
  }

  public getStripe(): Promise<Stripe | null> {
    if (!this.stripePromise) {
      this.stripePromise = this.loadStripe();
    }
    return this.stripePromise;
  }
}
