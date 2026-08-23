import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
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
  outcomeCode?: string;
  internalCreditPackageId?: number | null;
  includedCandidateCount?: number;
  includedRefinementCount?: number;
  includedPremiumAugmentationCount?: number;
  includesPlatformExportKit?: boolean;
  includesScoreDelta?: boolean;
  highlights?: string[];
}

export interface UserCreditStatus {
  credits: number;
  lastCreditReset: string;
  nextResetDate: string;
}

export interface PurchaseCreditPackageRequest {
  packageId: number;
  paymentTransactionId?: string;
  previewProcessedImageId?: number;
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
  description: string;
}

export interface CreditCosts {
  photoEnhancement: CreditCost;
  instantHeadshotGeneration?: CreditCost;
  modelTraining: CreditCost;
  styledGeneration: CreditCost;
}

export interface PaymentConfig {
  paymentSimulation: {
    enabled: boolean;
    skipStripeIntegration: boolean;
    reason?: string | null;
  };
}

export interface CreatePaymentIntentResponse {
  paymentIntentId: string;
  clientSecret: string;
  publishableKey?: string;
  paymentTransactionId?: string;
  isSimulation: boolean;
}

export interface CouponValidationPreview {
  isValid: boolean;
  message: string;
  discountAmount: number;
  finalPrice: number;
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
  providedIn: 'root',
})
export class CreditService {
  private _apiUrl: string;
  private _creditCosts: CreditCosts | null = null;

  constructor(
    private _http: HttpClient,
    private _configService: ConfigService
  ) {
    this._apiUrl = this._configService.getApiUrl();
  }

  /**
   * Get current user's credit status
   */
  getCreditStatus(): Observable<ApiResponse<UserCreditStatus>> {
    const endpoint = this._configService.buildApiEndpoint('credit/status');
    return this._http.get<ApiResponse<UserCreditStatus>>(endpoint);
  }

  /**
   * Get all available credit packages
   */
  getCreditPackages(): Observable<ApiResponse<CreditPackage[]>> {
    return this._http.get<ApiResponse<CreditPackage[]>>(
      this._configService.buildApiEndpoint('credit/packages')
    );
  }

  /**
   * Get payment configuration including simulation settings
   */
  getPaymentConfig(): Observable<ApiResponse<PaymentConfig>> {
    return this._http.get<ApiResponse<PaymentConfig>>(
      this._configService.buildApiEndpoint('credit/payment-config')
    );
  }

  /**
   * Create a payment intent for Stripe
   */
  createPaymentIntent(request: {
    packageId: number;
    couponCode?: string;
    previewProcessedImageId?: number;
  }): Observable<{
    success: boolean;
    data: CreatePaymentIntentResponse;
    error?: { code: string; message: string };
  }> {
    return this._http.post<{
      success: boolean;
      data: CreatePaymentIntentResponse;
      error?: { code: string; message: string };
    }>(this._configService.buildApiEndpoint('credit/create-payment-intent'), request);
  }

  validateCoupon(
    code: string,
    originalPrice: number
  ): Observable<ApiResponse<CouponValidationPreview>> {
    return this._http.post<ApiResponse<CouponValidationPreview>>(
      this._configService.buildApiEndpoint('credit/validate-coupon'),
      { code, originalPrice }
    );
  }

  /**
   * Purchase a credit package
   */
  purchaseCreditPackage(
    request: PurchaseCreditPackageRequest
  ): Observable<ApiResponse<{ updatedCredits: UserCreditStatus }>> {
    return this._http.post<ApiResponse<{ updatedCredits: UserCreditStatus }>>(
      this._configService.buildApiEndpoint('credit/purchase'),
      request
    );
  }

  /**
   * Get user's credit purchase history
   */
  getPurchaseHistory(): Observable<ApiResponse<CreditPurchase[]>> {
    return this._http.get<ApiResponse<CreditPurchase[]>>(
      this._configService.buildApiEndpoint('credit/history')
    );
  }

  /**
   * Get credit costs configuration from API
   */
  getCreditCosts(): Observable<ApiResponse<CreditCosts>> {
    return this._http.get<ApiResponse<CreditCosts>>(
      this._configService.buildApiEndpoint('credit/costs')
    );
  }

  /**
   * Get credit cost for a specific operation (async version)
   */
  async getCreditCost(operation: string): Promise<number> {
    if (!this._creditCosts) {
      try {
        const response = await firstValueFrom(this.getCreditCosts());
        if (response?.success) {
          this._creditCosts = response.data;
        }
      } catch (error) {
        console.error('Failed to fetch credit costs:', error);
        // Fallback to hardcoded values
        return this.getFallbackCreditCost(operation);
      }
    }

    switch (operation.toLowerCase()) {
      case 'photo_enhancement':
        return this._creditCosts?.photoEnhancement.cost || 1;
      case 'instant_headshot_generation':
        return this._creditCosts?.instantHeadshotGeneration?.cost || 1;
      case 'model_training':
        return this._creditCosts?.modelTraining.cost || 15;
      case 'styled_generation':
      case 'image_generation':
        return this._creditCosts?.styledGeneration.cost || 5;
      default:
        return 1;
    }
  }

  /**
   * Get credit cost for a specific operation (sync version with cached data)
   */
  getCreditCostSync(operation: string): number {
    if (!this._creditCosts) {
      return this.getFallbackCreditCost(operation);
    }

    switch (operation.toLowerCase()) {
      case 'photo_enhancement':
        return this._creditCosts.photoEnhancement.cost;
      case 'instant_headshot_generation':
        return this._creditCosts.instantHeadshotGeneration?.cost || 1;
      case 'model_training':
        return this._creditCosts.modelTraining.cost;
      case 'styled_generation':
      case 'image_generation':
        return this._creditCosts.styledGeneration.cost;
      default:
        return 1;
    }
  }

  /**
   * Load and cache credit costs
   */
  async loadCreditCosts(): Promise<void> {
    try {
      const response = await firstValueFrom(this.getCreditCosts());
      if (response?.success) {
        this._creditCosts = response.data;
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
      case 'instant_headshot_generation':
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
   * Calculate total available credits
   */
  getTotalAvailableCredits(
    userCreditStatus: UserCreditStatus | null,
    creditsInfo: unknown
  ): number {
    if (userCreditStatus?.credits !== undefined) {
      return userCreditStatus.credits;
    }

    return (
      (creditsInfo as { credits?: number; totalCredits?: number })?.credits ??
      (creditsInfo as { credits?: number; totalCredits?: number })?.totalCredits ??
      0
    );
  }
}
