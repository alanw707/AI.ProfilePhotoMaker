import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse, BaseHttpService } from './base-http.service';
import { ConfigService } from './config.service';

// --- Admin API DTOs ---

export interface AdminUserListDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  credits: number;
  isLockedOut: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface AdminUserDetailDto extends AdminUserListDto {
  gender: string | null;
  ethnicity: string | null;
  subscriptionTier: string;
  emailConfirmed: boolean;
  roles: string[];
}

export interface AdminUserMetricsDto {
  currentCredits: number;
  totalProcessedImages: number;
  originalUploads: number;
  generatedImages: number;
  purchaseCount: number;
  totalCreditsPurchased: number;
  totalCreditsConsumed: number;
  lastPurchaseAt: string | null;
  lastImageUploadAt: string | null;
  lastImageGenerationAt: string | null;
  lastActivityAt: string | null;
  hasUsageHistory: boolean;
}

export interface AdminCreditPurchaseHistoryDto {
  id: number;
  packageName: string;
  creditsAwarded: number;
  amountPaid: number;
  paymentProvider: string;
  status: string;
  purchaseDate: string;
  completedAt: string | null;
}

export interface AdminOutcomePackageDefinitionDto {
  id: number;
  code: string;
  name: string;
  description: string;
  price: number;
  currency: string;
  isActive: boolean;
  internalCreditPackageId: number | null;
  includedCandidateCount: number;
  includedRefinementCount: number;
  includedPremiumAugmentationCount: number;
  includesPlatformExportKit: boolean;
  includesScoreDelta: boolean;
  displayOrder: number;
}

export interface AdminGrantPackageEntitlementDto {
  packageDefinitionId: number;
  expiresAt?: string | null;
  reason: string;
  confirmInactive?: boolean;
}

export interface AdminPackageEntitlementDto {
  id: number;
  packageCode: string;
  packageName: string;
  status: string;
  remainingPackageUses: number;
  remainingCandidates: number;
  remainingRefinements: number;
  remainingPremiumAugmentations: number;
  platformExportKitAvailable: boolean;
  activatedAt: string | null;
  consumedAt: string | null;
  expiresAt: string | null;
}

export interface AdminRecentImageDto {
  id: number;
  imageUrl: string;
  kind: string;
  style: string | null;
  createdAt: string;
  isGenerated: boolean;
  isOriginalUpload: boolean;
  provider: string | null;
  providerModel: string | null;
  generationStatus: string | null;
  failureReason: string | null;
}

export interface AdminUserActivityDto {
  eventType: string;
  title: string;
  description: string | null;
  status: string | null;
  occurredAt: string;
  creditsDelta: number | null;
  creditsRemaining: number | null;
  imageUrl: string | null;
}

export interface AdminUserAdminActionDto {
  id: number;
  adminEmail: string;
  action: string;
  details: string | null;
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
}

export interface AdminUserDiagnosticsDto {
  user: AdminUserDetailDto;
  metrics: AdminUserMetricsDto;
  recentPurchases: AdminCreditPurchaseHistoryDto[];
  packageEntitlements: AdminPackageEntitlementDto[];
  recentImages: AdminRecentImageDto[];
  activityHistory: AdminUserActivityDto[];
  recentAdminActions: AdminUserAdminActionDto[];
}

export interface AdminCreditAdjustmentDto {
  userId: string;
  amount: number;
  reason: string;
}

export interface CouponCreateDto {
  code: string;
  discountType: number;
  discountValue: number;
  maxUsages: number;
  expiresAt?: string | null;
}

export interface CouponUpdateDto {
  maxUsages?: number | null;
  expiresAt?: string | null;
  isActive?: boolean | null;
}

export interface CouponListDto {
  id: number;
  code: string;
  discountType: string;
  discountValue: number;
  maxUsages: number;
  currentUsages: number;
  expiresAt: string | null;
  isActive: boolean;
  isExpired: boolean;
  createdAt: string;
}

export interface AdminAuditLogDto {
  id: number;
  adminEmail: string;
  action: string;
  targetUserEmail: string | null;
  details: string | null;
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
}

export interface AdminDashboardDto {
  totalUsers: number;
  activeUsers: number;
  totalCreditsOutstanding: number;
  totalCreditsPurchased: number;
  activeCoupons: number;
}

export interface AdminProductHealthDto {
  window: string;
  fromUtc: string;
  toUtc: string;
  funnel: AdminProductFunnelDto;
  packageMix: AdminPackageMixDto[];
  packageFulfillment: AdminPackageFulfillmentDto;
  providerHealth: AdminProviderHealthDto[];
  failureQueue: AdminFailureQueueDto;
  replicateRetirement: AdminReplicateRetirementDto;
}

export interface AdminProductFunnelDto {
  uploads: number;
  successfulPreviewGenerations: number;
  paidPackagePurchases: number;
  starterPurchases: number;
  proPurchases: number;
  exportDownloads: number | null;
  exportDownloadsAvailable: boolean;
  medianGenerationTimeMs: number | null;
  p95GenerationTimeMs: number | null;
  generationLatencyAvailable: boolean;
  previewGenerationSuccessRate: number | null;
  previewGenerationSuccessRateAvailable: boolean;
  previewToPaidConversionRate: number | null;
  previewToPaidConversionRateAvailable: boolean;
}

export interface AdminPackageMixDto {
  code: string;
  name: string;
  purchases: number;
  revenueProxy: number;
}

export interface AdminPackageFulfillmentDto {
  activeEntitlements: number;
  consumedEntitlements: number;
  expiredEntitlements: number;
  refundedEntitlements: number;
  revokedEntitlements: number;
  remainingCandidates: number;
  remainingRefinements: number;
  remainingPremiumAugmentations: number;
  exportKitsAvailable: number;
}

export interface AdminProviderHealthDto {
  provider: string;
  model: string;
  generatedImages: number;
  failedImages: number;
  failureRate: number;
}

export interface AdminFailureQueueDto {
  failedGenerations: number;
  topReasons: AdminFailureReasonDto[];
}

export interface AdminFailureReasonDto {
  reason: string;
  count: number;
}

export interface AdminReplicateRetirementDto {
  openAiGeneratedImages: number;
  replicateGeneratedImages: number;
  replicateUsageShare: number;
  latencySignalAvailable: boolean;
  fallbackGeneratedImages: number;
  fallbackUseSignalAvailable: boolean;
  advancedPhotoshootRequests: number;
  advancedPhotoshootUsageSignalAvailable: boolean;
  qualityComplaintSignalAvailable: boolean;
  recommendation: string;
}

export interface PaginatedResult<T> {
  users?: T[];
  logs?: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({
  providedIn: 'root',
})
export class AdminService extends BaseHttpService {
  constructor(
    protected override http: HttpClient,
    protected override config: ConfigService
  ) {
    super(http, config);
  }

  getUsers(page = 1, pageSize = 20, search = ''): Observable<PaginatedResult<AdminUserListDto>> {
    return this.get<PaginatedResult<AdminUserListDto>>(
      `admin/users?page=${page}&pageSize=${pageSize}&search=${encodeURIComponent(search)}`,
      { withCredentials: true }
    );
  }

  getUserDetail(userId: string): Observable<AdminUserDiagnosticsDto> {
    return this.get<AdminUserDiagnosticsDto>(`admin/users/${userId}`, { withCredentials: true });
  }

  getPackageDefinitions(): Observable<AdminOutcomePackageDefinitionDto[]> {
    return this.get<AdminOutcomePackageDefinitionDto[]>('admin/package-definitions', {
      withCredentials: true,
    });
  }

  grantPackageEntitlement(
    userId: string,
    dto: AdminGrantPackageEntitlementDto
  ): Observable<{ entitlement: AdminPackageEntitlementDto; creditBalance: number | null }> {
    return this.post<{ entitlement: AdminPackageEntitlementDto; creditBalance: number | null }>(
      `admin/users/${userId}/package-entitlements`,
      dto,
      { withCredentials: true }
    );
  }

  revokePackageEntitlement(
    userId: string,
    entitlementId: number,
    reason: string
  ): Observable<{ entitlement: AdminPackageEntitlementDto }> {
    return this.post<{ entitlement: AdminPackageEntitlementDto }>(
      `admin/users/${userId}/package-entitlements/${entitlementId}/revoke`,
      { reason },
      { withCredentials: true }
    );
  }

  deactivateUser(userId: string, reason: string): Observable<{ userId: string }> {
    return this.post<{ userId: string }>(
      `admin/users/${userId}/deactivate`,
      { reason },
      { withCredentials: true }
    );
  }

  reactivateUser(userId: string, reason: string): Observable<{ userId: string }> {
    return this.post<{ userId: string }>(
      `admin/users/${userId}/reactivate`,
      { reason },
      { withCredentials: true }
    );
  }

  deleteUser(userId: string, reason: string): Observable<{ userId: string }> {
    return this.http.request<{ userId: string }>(
      'DELETE',
      this.buildApiUrl(`admin/users/${userId}`),
      {
        body: { reason },
        withCredentials: true,
      }
    );
  }

  adjustCredits(dto: AdminCreditAdjustmentDto): Observable<{ userId: string; newBalance: number }> {
    return this.post<{ userId: string; newBalance: number }>('admin/credits/adjust', dto, {
      withCredentials: true,
    });
  }

  getCoupons(): Observable<CouponListDto[]> {
    return this.get<CouponListDto[]>('admin/coupons', { withCredentials: true });
  }

  createCoupon(
    dto: CouponCreateDto
  ): Observable<ApiResponse<CouponListDto> & { warning?: string | null }> {
    return this.rawPost<ApiResponse<CouponListDto> & { warning?: string | null }>(
      this.buildApiUrl('admin/coupons'),
      dto,
      { withCredentials: true }
    );
  }

  updateCoupon(id: number, dto: CouponUpdateDto): Observable<{ id: number }> {
    return this.put<{ id: number }>(`admin/coupons/${id}`, dto, { withCredentials: true });
  }

  deleteCoupon(id: number): Observable<{ id: number }> {
    return this.delete<{ id: number }>(`admin/coupons/${id}`, { withCredentials: true });
  }

  getAuditLogs(
    page = 1,
    pageSize = 50,
    action = ''
  ): Observable<PaginatedResult<AdminAuditLogDto>> {
    return this.get<PaginatedResult<AdminAuditLogDto>>(
      `admin/audit-logs?page=${page}&pageSize=${pageSize}&action=${encodeURIComponent(action)}`,
      { withCredentials: true }
    );
  }

  getDashboard(): Observable<AdminDashboardDto> {
    return this.get<AdminDashboardDto>('admin/dashboard', { withCredentials: true });
  }

  getProductHealth(window = '7d'): Observable<AdminProductHealthDto> {
    return this.get<AdminProductHealthDto>(
      `admin/product-health?window=${encodeURIComponent(window)}`,
      { withCredentials: true }
    );
  }
}
