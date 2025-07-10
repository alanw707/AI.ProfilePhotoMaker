import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { ConfigService } from './config.service';

export interface UserProfile {
  id: number;
  userId: string;
  firstName?: string;
  lastName?: string;
  gender?: string;
  ethnicity?: string;
  trainedModelId?: string;
  trainedModelVersionId?: string;
  modelTrainedAt?: Date;
  subscriptionTier: string;
  basicCredits: number;
  lastCreditReset: Date;
  createdAt: Date;
  updatedAt: Date;
}

export interface CreateProfileDto {
  firstName?: string;
  lastName?: string;
  gender?: string;
  ethnicity?: string;
}

export interface UpdateProfileDto {
  firstName?: string;
  lastName?: string;
  gender?: string;
  ethnicity?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  constructor(private http: HttpClient, private config: ConfigService) {}

  getCurrentUserProfile(): Observable<{ success: boolean; data: UserProfile; error: any }> {
    return this.http.get<UserProfile>(this.config.profileUrl).pipe(
      map(profile => ({ success: true, data: profile, error: null })),
      catchError(error => of({ success: false, data: null as any, error }))
    );
  }

  createProfile(profile: CreateProfileDto): Observable<{ success: boolean; data: UserProfile; error: any }> {
    return this.http.post<UserProfile>(this.config.profileUrl, profile).pipe(
      map(profile => ({ success: true, data: profile, error: null })),
      catchError(error => of({ success: false, data: null as any, error }))
    );
  }

  updateProfile(profile: UpdateProfileDto): Observable<{ success: boolean; data: UserProfile; error: any }> {
    return this.http.put<UserProfile>(this.config.profileUrl, profile).pipe(
      map(profile => ({ success: true, data: profile, error: null })),
      catchError(error => of({ success: false, data: null as any, error }))
    );
  }

  deleteProfile(): Observable<{ success: boolean; message: string }> {
    return this.http.delete<{ success: boolean; message: string }>(this.config.profileUrl).pipe(
      catchError(error => of({ success: false, message: error.message || 'Delete failed' }))
    );
  }

  checkModelStatus(): Observable<{ success: boolean; data: { modelExists: boolean; modelStatus: string; modelId?: string; message: string }; error: any }> {
    return this.http.post<{ success: boolean; data: { modelExists: boolean; modelStatus: string; modelId?: string; message: string }; error: any }>(`${this.config.profileUrl}/check-model-status`, {});
  }

  // Data deletion endpoints
  getDataStats(): Observable<{ success: boolean; data: any; error: any }> {
    return this.http.get<{ success: boolean; data: any; error: any }>(`${this.config.profileUrl}/data-stats`);
  }

  deleteInputPhotos(): Observable<{ success: boolean; data: any; error: any }> {
    return this.http.delete<{ success: boolean; data: any; error: any }>(`${this.config.profileUrl}/data/photos`);
  }

  deleteAIModel(): Observable<{ success: boolean; data: any; error: any }> {
    return this.http.delete<{ success: boolean; data: any; error: any }>(`${this.config.profileUrl}/data/model`);
  }

  deleteAllUserData(): Observable<{ success: boolean; data: any; error: any }> {
    return this.http.delete<{ success: boolean; data: any; error: any }>(`${this.config.profileUrl}/data/all`);
  }

  deleteUserAccount(): Observable<{ success: boolean; data: any; error: any }> {
    return this.http.delete<{ success: boolean; data: any; error: any }>(`${this.config.profileUrl}/account`);
  }

  exportUserData(): Observable<Blob> {
    return this.http.get(`${this.config.profileUrl}/data/export`, { 
      responseType: 'blob' 
    });
  }

  // Model discovery endpoints using new SDK integration
  discoverModels(): Observable<{ success: boolean; data: any; message?: string; error?: any }> {
    return this.http.post<{ success: boolean; data: any; message?: string; error?: any }>(`${this.config.baseUrl}/model-discovery/sync`, {});
  }

  getModelSyncStatus(): Observable<{ success: boolean; data: any; error?: any }> {
    return this.http.get<{ success: boolean; data: any; error?: any }>(`${this.config.baseUrl}/model-discovery/status`);
  }

  syncSpecificModel(modelId: string, versionId: string): Observable<{ success: boolean; message?: string; error?: any }> {
    return this.http.post<{ success: boolean; message?: string; error?: any }>(`${this.config.baseUrl}/model-discovery/sync-specific`, {
      modelId,
      versionId
    });
  }
}