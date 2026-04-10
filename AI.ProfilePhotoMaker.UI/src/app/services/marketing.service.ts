import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BaseHttpService } from './base-http.service';
import { ConfigService } from './config.service';

export interface CampaignDto {
  id: string;
  name: string;
  subject: string;
  segmentFilter: string;
  scheduledAt: string | null;
  status: 'Draft' | 'Scheduled' | 'Sending' | 'Sent' | 'Cancelled';
  recipientCount: number;
  sentCount: number;
  failedCount: number;
  createdBy: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface CampaignDetailDto extends CampaignDto {
  htmlBody: string;
}

export interface EmailLogDto {
  id: string;
  userId: string;
  email: string;
  status: string;
  errorMessage: string | null;
  sentAt: string | null;
  createdAt: string;
}

export interface CreateCampaignRequest {
  name: string;
  subject: string;
  htmlBody: string;
  segmentFilter: string;
  scheduledAt?: string | null;
}

export interface UpdateCampaignRequest {
  name?: string;
  subject?: string;
  htmlBody?: string;
  segmentFilter?: string;
}

export interface CampaignListResult {
  campaigns: CampaignDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface LogListResult {
  logs: EmailLogDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class MarketingService extends BaseHttpService {
  constructor(
    protected override http: HttpClient,
    protected override config: ConfigService
  ) {
    super(http, config);
  }

  getCampaigns(page = 1, pageSize = 20): Observable<CampaignListResult> {
    return this.get<CampaignListResult>(`admin/campaigns?page=${page}&pageSize=${pageSize}`, {
      withCredentials: true,
    });
  }

  getCampaign(id: string): Observable<CampaignDetailDto> {
    return this.get<CampaignDetailDto>(`admin/campaigns/${id}`, { withCredentials: true });
  }

  createCampaign(req: CreateCampaignRequest): Observable<CampaignDetailDto> {
    return this.post<CampaignDetailDto>('admin/campaigns', req, { withCredentials: true });
  }

  updateCampaign(id: string, req: UpdateCampaignRequest): Observable<CampaignDetailDto> {
    return this.patch<CampaignDetailDto>(`admin/campaigns/${id}`, req, { withCredentials: true });
  }

  scheduleCampaign(id: string, scheduledAt: string): Observable<{ scheduled: boolean }> {
    return this.post<{ scheduled: boolean }>(
      `admin/campaigns/${id}/schedule`,
      { scheduledAt },
      { withCredentials: true }
    );
  }

  cancelCampaign(id: string): Observable<{ cancelled: boolean }> {
    return this.post<{ cancelled: boolean }>(
      `admin/campaigns/${id}/cancel`,
      {},
      { withCredentials: true }
    );
  }

  sendTest(id: string, testEmail: string): Observable<{ sent: boolean }> {
    return this.post<{ sent: boolean }>(
      `admin/campaigns/${id}/test`,
      { testEmail },
      { withCredentials: true }
    );
  }

  duplicateCampaign(id: string): Observable<CampaignDetailDto> {
    return this.post<CampaignDetailDto>(
      `admin/campaigns/${id}/duplicate`,
      {},
      { withCredentials: true }
    );
  }

  getLogs(id: string, page = 1, pageSize = 50): Observable<LogListResult> {
    return this.get<LogListResult>(`admin/campaigns/${id}/logs?page=${page}&pageSize=${pageSize}`, {
      withCredentials: true,
    });
  }

  previewSegment(segmentFilter: string): Observable<{ segmentFilter: string; count: number }> {
    return this.post<{ segmentFilter: string; count: number }>(
      'admin/campaigns/segments/preview',
      { segmentFilter },
      { withCredentials: true }
    );
  }

  getPreviewUrl(id: string): string {
    return this.buildApiUrl(`admin/campaigns/${id}/preview`);
  }

  unsubscribe(token: string): Observable<{ unsubscribed: boolean }> {
    return this.post<{ unsubscribed: boolean }>(
      `user/marketing/unsubscribe?token=${encodeURIComponent(token)}`,
      {}
    );
  }
}
