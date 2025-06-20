import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

export interface PremiumPackage {
  id: number;
  name: string;
  credits: number;
  price: number;
  maxStyles: number;
  maxImagesPerStyle: number;
  description?: string;
}

export interface UserPackageStatus {
  hasActivePackage: boolean;
  packageId?: number;
  packageName?: string;
  creditsRemaining: number;
  expirationDate?: Date;
  trainedModelId?: string;
  modelTrainedAt?: Date;
  modelExpired: boolean;
  daysUntilExpiration: number;
}

export interface PurchasePackageRequest {
  packageId: number;
  paymentTransactionId?: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: {
    code: string;
    message: string;
  };
}

@Injectable({
  providedIn: 'root'
})
export class PremiumPackageService {
  private baseUrl: string;

  constructor(
    private http: HttpClient,
    private configService: ConfigService
  ) {
    this.baseUrl = `${this.configService.baseUrl}/premium-package`;
  }

  /**
   * Get all available premium packages
   */
  getActivePackages(): Observable<ApiResponse<PremiumPackage[]>> {
    return this.http.get<ApiResponse<PremiumPackage[]>>(`${this.baseUrl}/packages`);
  }

  /**
   * Get current user's package status
   */
  getUserPackageStatus(): Observable<ApiResponse<UserPackageStatus>> {
    return this.http.get<ApiResponse<UserPackageStatus>>(`${this.baseUrl}/status`);
  }

  /**
   * Purchase a premium package
   */
  purchasePackage(request: PurchasePackageRequest): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.baseUrl}/purchase`, request);
  }

  /**
   * Check if user can select a specific number of styles
   */
  canSelectStyles(styleCount: number): Observable<ApiResponse<{ canSelect: boolean; maxStyles: number }>> {
    return this.http.get<ApiResponse<{ canSelect: boolean; maxStyles: number }>>(
      `${this.baseUrl}/can-select-styles/${styleCount}`
    );
  }

  /**
   * Check if user can generate a specific number of images
   */
  canGenerateImages(imageCount: number): Observable<ApiResponse<{ canGenerate: boolean; imageCount: number }>> {
    return this.http.get<ApiResponse<{ canGenerate: boolean; imageCount: number }>>(
      `${this.baseUrl}/can-generate-images/${imageCount}`
    );
  }
}