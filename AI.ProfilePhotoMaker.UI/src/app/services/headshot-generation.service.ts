import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

export interface HeadshotGenerationRequest {
  imageStoragePath: string;
  style?: 'professional' | 'linkedin' | 'creator' | string;
  background?: 'auto' | 'neutral' | 'office' | 'studio' | string;
  packageCode?: 'free_preview' | 'starter_package' | 'pro_package' | string;
  numOutputs?: number;
  turnstileToken?: string;
  clientRequestId?: string;
}

export interface HeadshotCandidate {
  imageUrl: string;
  storagePath: string;
  processedImageId: number;
  provider: string;
  model: string;
  correlationId: string;
}

export interface HeadshotGenerationResponse {
  success: boolean;
  data: {
    success: boolean;
    imageUrl: string;
    storagePath: string;
    processedImageId: number;
    provider: string;
    model: string;
    style: string;
    background: string;
    creditsCost: number;
    remainingCredits: number;
    correlationId: string;
    candidates?: HeadshotCandidate[];
  } | null;
  error: { code: string; message: string } | null;
}

@Injectable({
  providedIn: 'root',
})
export class HeadshotGenerationService {
  constructor(
    private http: HttpClient,
    private config: ConfigService
  ) {}

  generateHeadshot(request: HeadshotGenerationRequest): Observable<HeadshotGenerationResponse> {
    return this.http.post<HeadshotGenerationResponse>(
      this.config.getFullUrl('/headshots/generate'),
      {
        ...request,
        clientRequestId: request.clientRequestId || this.createClientRequestId(),
      }
    );
  }

  private createClientRequestId(): string {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
      return crypto.randomUUID();
    }

    return `headshot-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  }
}
