import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

export type FeedbackCategory = 'Bug' | 'Feature' | 'Question' | 'Other';

export interface SubmitFeedbackRequest {
  category: FeedbackCategory | string;
  message: string;
  pageUrl?: string;
  userAgent?: string;
}

export interface SubmitFeedbackResponse {
  success: boolean;
  data?: { id: string };
  message?: string;
  error?: any;
}

@Injectable({
  providedIn: 'root',
})
export class FeedbackService {
  constructor(
    private _http: HttpClient,
    private _config: ConfigService
  ) {}

  submitFeedback(request: SubmitFeedbackRequest): Observable<SubmitFeedbackResponse> {
    return this._http.post<SubmitFeedbackResponse>(this._config.feedbackUrl, request);
  }
}

