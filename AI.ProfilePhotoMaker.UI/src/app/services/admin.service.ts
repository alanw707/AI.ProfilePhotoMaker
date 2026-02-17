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

  getUserDetail(userId: string): Observable<AdminUserDetailDto> {
    return this.get<AdminUserDetailDto>(`admin/users/${userId}`, { withCredentials: true });
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
}
