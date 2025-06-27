import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

export interface CreditPackage {
  id: number;
  name: string;
  credits: number;
  bonusCredits: number;
  totalCredits: number;
  price: number;
  description: string;
  displayOrder: number;
}

export interface UserCreditStatus {
  totalCredits: number;
  weeklyCredits: number;
  purchasedCredits: number;
  lastCreditReset: string;
  nextResetDate: string;
}

export interface PurchaseCreditPackageRequest {
  packageId: number;
  paymentTransactionId?: string;
}

export interface CreditPurchase {
  id: number;
  purchaseDate: string;
  creditsAwarded: number;
  amountPaid: number;
  status: string;
  packageName: string;
}

export interface CreditCost {
  cost: number;
  canUseWeeklyCredits: boolean;
  description: string;
}

export interface CreditCosts {
  photoEnhancement: CreditCost;
  modelTraining: CreditCost;
  styledGeneration: CreditCost;
}

export interface PaymentConfig {
  paymentSimulation: {
    enabled: boolean;
    skipStripeIntegration: boolean;
  };
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error: {
    code: string;
    message: string;
  } | null;
}

@Injectable({
  providedIn: 'root'
})
export class CreditService {
  private apiUrl: string;
  private creditCosts: CreditCosts | null = null;

  constructor(private http: HttpClient, private configService: ConfigService) {
    this.apiUrl = this.configService.getApiUrl();
  }

  /**
   * Get current user's credit status
   */
  getCreditStatus(): Observable<ApiResponse<UserCreditStatus>> {
    return this.http.get<ApiResponse<UserCreditStatus>>(`${this.apiUrl}/api/credit/status`);
  }

  /**
   * Get all available credit packages
   */
  getCreditPackages(): Observable<ApiResponse<CreditPackage[]>> {
    return this.http.get<ApiResponse<CreditPackage[]>>(`${this.apiUrl}/api/credit/packages`);
  }

  /**
   * Get payment configuration including simulation settings
   */
  getPaymentConfig(): Observable<ApiResponse<PaymentConfig>> {
    return this.http.get<ApiResponse<PaymentConfig>>(`${this.apiUrl}/api/credit/payment-config`);
  }

  /**
   * Create a payment intent for Stripe
   */
  createPaymentIntent(request: { packageId: number }): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/api/credit/create-payment-intent`, request);
  }

  /**
   * Purchase a credit package
   */
  purchaseCreditPackage(request: PurchaseCreditPackageRequest): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.apiUrl}/api/credit/purchase`, request);
  }

  /**
   * Get user's credit purchase history
   */
  getPurchaseHistory(): Observable<ApiResponse<CreditPurchase[]>> {
    return this.http.get<ApiResponse<CreditPurchase[]>>(`${this.apiUrl}/api/credit/history`);
  }

  /**
   * Get credit costs configuration from API
   */
  getCreditCosts(): Observable<ApiResponse<CreditCosts>> {
    return this.http.get<ApiResponse<CreditCosts>>(`${this.apiUrl}/api/credit/costs`);
  }

  /**
   * Get credit cost for a specific operation (async version)
   */
  async getCreditCost(operation: string): Promise<number> {
    if (!this.creditCosts) {
      try {
        const response = await this.getCreditCosts().toPromise();
        if (response?.success) {
          this.creditCosts = response.data;
        }
      } catch (error) {
        console.error('Failed to fetch credit costs:', error);
        // Fallback to hardcoded values
        return this.getFallbackCreditCost(operation);
      }
    }

    switch (operation.toLowerCase()) {
      case 'photo_enhancement':
        return this.creditCosts?.photoEnhancement.cost || 1;
      case 'model_training':
        return this.creditCosts?.modelTraining.cost || 15;
      case 'styled_generation':
      case 'image_generation':
        return this.creditCosts?.styledGeneration.cost || 5;
      default:
        return 1;
    }
  }

  /**
   * Get credit cost for a specific operation (sync version with cached data)
   */
  getCreditCostSync(operation: string): number {
    if (!this.creditCosts) {
      return this.getFallbackCreditCost(operation);
    }

    switch (operation.toLowerCase()) {
      case 'photo_enhancement':
        return this.creditCosts.photoEnhancement.cost;
      case 'model_training':
        return this.creditCosts.modelTraining.cost;
      case 'styled_generation':
      case 'image_generation':
        return this.creditCosts.styledGeneration.cost;
      default:
        return 1;
    }
  }

  /**
   * Check if an operation can use weekly credits (async version)
   */
  async canUseWeeklyCredits(operation: string): Promise<boolean> {
    if (!this.creditCosts) {
      try {
        const response = await this.getCreditCosts().toPromise();
        if (response?.success) {
          this.creditCosts = response.data;
        }
      } catch (error) {
        console.error('Failed to fetch credit costs:', error);
        return this.getFallbackWeeklyCreditsUsage(operation);
      }
    }

    switch (operation.toLowerCase()) {
      case 'photo_enhancement':
        return this.creditCosts?.photoEnhancement.canUseWeeklyCredits || true;
      case 'model_training':
        return this.creditCosts?.modelTraining.canUseWeeklyCredits || false;
      case 'styled_generation':
      case 'image_generation':
        return this.creditCosts?.styledGeneration.canUseWeeklyCredits || false;
      default:
        return false;
    }
  }

  /**
   * Check if an operation can use weekly credits (sync version with cached data)
   */
  canUseWeeklyCreditSync(operation: string): boolean {
    if (!this.creditCosts) {
      return this.getFallbackWeeklyCreditsUsage(operation);
    }

    switch (operation.toLowerCase()) {
      case 'photo_enhancement':
        return this.creditCosts.photoEnhancement.canUseWeeklyCredits;
      case 'model_training':
        return this.creditCosts.modelTraining.canUseWeeklyCredits;
      case 'styled_generation':
      case 'image_generation':
        return this.creditCosts.styledGeneration.canUseWeeklyCredits;
      default:
        return false;
    }
  }

  /**
   * Load and cache credit costs
   */
  async loadCreditCosts(): Promise<void> {
    try {
      const response = await this.getCreditCosts().toPromise();
      if (response?.success) {
        this.creditCosts = response.data;
      }
    } catch (error) {
      console.error('Failed to load credit costs:', error);
    }
  }

  /**
   * Fallback credit costs (in case API fails)
   */
  private getFallbackCreditCost(operation: string): number {
    switch (operation.toLowerCase()) {
      case 'photo_enhancement':
        return 1;
      case 'model_training':
        return 15;
      case 'styled_generation':
      case 'image_generation':
        return 5;
      default:
        return 1;
    }
  }

  /**
   * Fallback weekly credits usage (in case API fails)
   */
  private getFallbackWeeklyCreditsUsage(operation: string): boolean {
    switch (operation.toLowerCase()) {
      case 'photo_enhancement':
        return true;
      case 'model_training':
      case 'styled_generation':
      case 'image_generation':
        return false;
      default:
        return false;
    }
  }
}