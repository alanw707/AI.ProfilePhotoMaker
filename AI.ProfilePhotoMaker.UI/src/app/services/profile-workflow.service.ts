import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

export interface OutcomePackageDefinition {
  id: number;
  code: 'free_preview' | 'starter_package' | 'pro_package' | string;
  name: string;
  description: string;
  price: number;
  currency: string;
  internalCreditPackageId?: number | null;
  includedCandidateCount: number;
  includedRefinementCount: number;
  includedPremiumAugmentationCount: number;
  includesPlatformExportKit: boolean;
  includesScoreDelta: boolean;
  displayOrder: number;
  highlights: string[];
}

export interface PackageEntitlement {
  id: number;
  packageCode: string;
  packageName: string;
  status: string;
  remainingPackageUses: number;
  remainingCandidates: number;
  remainingRefinements: number;
  remainingPremiumAugmentations: number;
  platformExportKitAvailable: boolean;
  activatedAt?: string | null;
  expiresAt?: string | null;
}

export interface ProfilePhotoSubscore {
  code: string;
  label: string;
  score: number;
  feedback: string;
}

export interface PhotoQualityGate {
  status: 'pass' | 'warning' | 'blocked';
  reasons: string[];
  recommendations: string[];
}

export interface ProfilePhotoScore {
  overallScore: number;
  ratingLabel: string;
  subscores: ProfilePhotoSubscore[];
  strengths: string[];
  improvements: string[];
  guidance: string;
  qualityGate?: PhotoQualityGate;
}

export interface StudioImageSource {
  processedImageId: number;
  storagePath: string;
  imageUrl: string;
  style?: string | null;
}

export interface PlatformExportOption {
  code: string;
  label: string;
  width: number;
  height: number;
  fileNameSuffix: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string | null;
  error?: { code: string; message: string } | null;
}

@Injectable({ providedIn: 'root' })
export class ProfileWorkflowService {
  constructor(
    private http: HttpClient,
    private config: ConfigService
  ) {}

  getOutcomePackages(): Observable<ApiResponse<OutcomePackageDefinition[]>> {
    return this.http.get<ApiResponse<OutcomePackageDefinition[]>>(
      this.config.getFullUrl('/profilephotoworkflow/packages')
    );
  }

  getEntitlements(): Observable<ApiResponse<PackageEntitlement[]>> {
    return this.http.get<ApiResponse<PackageEntitlement[]>>(
      this.config.getFullUrl('/profilephotoworkflow/entitlements')
    );
  }

  getExportOptions(): Observable<ApiResponse<PlatformExportOption[]>> {
    return this.http.get<ApiResponse<PlatformExportOption[]>>(
      this.config.getFullUrl('/profilephotoworkflow/export-options')
    );
  }

  scorePhoto(file: File): Observable<ApiResponse<ProfilePhotoScore>> {
    const formData = new FormData();
    formData.append('image', file);
    return this.http.post<ApiResponse<ProfilePhotoScore>>(
      this.config.getFullUrl('/profilephotoworkflow/score'),
      formData
    );
  }

  getStudioImageSource(processedImageId: number): Observable<ApiResponse<StudioImageSource>> {
    return this.http.get<ApiResponse<StudioImageSource>>(
      this.config.getFullUrl(`/profilephotoworkflow/images/${processedImageId}/studio-source`)
    );
  }

  scoreProcessedImage(processedImageId: number): Observable<ApiResponse<ProfilePhotoScore>> {
    return this.http.get<ApiResponse<ProfilePhotoScore>>(
      this.config.getFullUrl(`/profilephotoworkflow/score-image/${processedImageId}`)
    );
  }

  createExportPackage(
    processedImageId: number,
    exportCodes: string[],
    adjustments?: {
      zoomPercent: number;
      rotateDegrees: number;
      brightnessPercent: number;
      contrastPercent: number;
      sharpnessPercent: number;
    }
  ): Observable<Blob> {
    return this.http.post(
      this.config.getFullUrl('/profilephotoworkflow/export-package'),
      { processedImageId, exportCodes, ...adjustments },
      { responseType: 'blob' }
    );
  }
}
