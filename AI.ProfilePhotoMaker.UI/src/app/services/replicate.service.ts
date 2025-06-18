import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

export interface TrainModelRequest {
  userId: string;
  imageZipUrl: string;
}

export interface GenerateImagesRequest {
  trainedModelVersion: string;
  userId: string;
  style: string;
  userInfo?: UserInfo;
}

export interface GenerateFreeImageRequest {
  gender: string;
  userInfo?: UserInfo;
  enhancementType?: string;
  imageData?: string;
}

export interface UserInfo {
  gender?: string;
  ethnicity?: string;
  attributes?: { [key: string]: string };
}

export interface ReplicateTrainingResult {
  id: string;
  status: string;
  created_at: string;
  completed_at?: string;
  version?: string;
  error?: string;
  logs?: string;
}

export interface ReplicatePredictionResult {
  id: string;
  status: string;
  created_at: string;
  completed_at?: string;
  output?: string[];
  error?: string;
  logs?: string;
}

export interface CreditsInfo {
  availableCredits: number;
  subscriptionTier: string;
  lastCreditReset: Date;
  nextResetDate: Date;
}

@Injectable({
  providedIn: 'root'
})
export class ReplicateService {
  constructor(private http: HttpClient, private config: ConfigService) {}

  // Model Training
  trainModel(request: TrainModelRequest): Observable<{ success: boolean; data: ReplicateTrainingResult; error: any }> {
    return this.http.post<{ success: boolean; data: ReplicateTrainingResult; error: any }>(this.config.getFullUrl('/replicate/train'), request);
  }

  getTrainingStatus(trainingId: string): Observable<{ success: boolean; data: ReplicateTrainingResult; error: any }> {
    return this.http.get<{ success: boolean; data: ReplicateTrainingResult; error: any }>(this.config.getFullUrl(`/replicate/train/status/${trainingId}`));
  }

  // Image Generation (Premium)
  generateImages(request: GenerateImagesRequest): Observable<{ success: boolean; data: ReplicatePredictionResult; error: any }> {
    return this.http.post<{ success: boolean; data: ReplicatePredictionResult; error: any }>(this.config.getFullUrl('/replicate/generate'), request);
  }

  getPredictionStatus(predictionId: string): Observable<{ success: boolean; data: ReplicatePredictionResult; error: any }> {
    return this.http.get<{ success: boolean; data: ReplicatePredictionResult; error: any }>(this.config.getFullUrl(`/replicate/generate/status/${predictionId}`));
  }

  // Basic Tier Generation
  generateFreeImage(request: GenerateFreeImageRequest): Observable<{ 
    success: boolean; 
    data: { 
      prediction: ReplicatePredictionResult; 
      creditsRemaining: number 
    }; 
    error: any 
  }> {
    return this.http.post<{ 
      success: boolean; 
      data: { 
        prediction: ReplicatePredictionResult; 
        creditsRemaining: number 
      }; 
      error: any 
    }>(this.config.generateFreeUrl, request);
  }

  // Credits Management
  getCredits(): Observable<{ success: boolean; data: CreditsInfo; error: any }> {
    return this.http.get<{ success: boolean; data: CreditsInfo; error: any }>(this.config.replicateCreditsUrl);
  }

  // Photo Enhancement
  enhancePhoto(request: EnhancePhotoRequest): Observable<{ 
    success: boolean; 
    data: { 
      prediction: ReplicatePredictionResult; 
      creditsRemaining: number;
      enhancementType: string;
    }; 
    error: any 
  }> {
    return this.http.post<{ 
      success: boolean; 
      data: { 
        prediction: ReplicatePredictionResult; 
        creditsRemaining: number;
        enhancementType: string;
      }; 
      error: any 
    }>(this.config.getFullUrl('/replicate/enhance'), request);
  }
}

export interface EnhancePhotoRequest {
  imageUrl: string;
  enhancementType?: string;
}