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
  numOutputs?: number; // Number of images to generate (1-4)
}

export interface GenerateBatchImagesRequest {
  trainedModelVersion: string;
  userId: string;
  styles: string[];
  userInfo?: UserInfo;
  numOutputsPerStyle?: number; // Number of images to generate per style (1-4)
}

export interface GenerateBasicImageRequest {
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
  // Add dataUrl for enhanced image (base64)
  dataUrl?: string;
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

  // Batch Image Generation (Premium) - Consolidated multiple styles in one request
  generateBatchImages(request: GenerateBatchImagesRequest): Observable<{ 
    success: boolean; 
    data: { 
      predictions: { style: string; result: ReplicatePredictionResult }[];
      creditsRemaining: number;
      creditsCost: number;
      successfulStyles: number;
      failedStyles: number;
      failures: { style: string; error: string }[];
    }; 
    error: any 
  }> {
    return this.http.post<{ 
      success: boolean; 
      data: { 
        predictions: { style: string; result: ReplicatePredictionResult }[];
        creditsRemaining: number;
        creditsCost: number;
        successfulStyles: number;
        failedStyles: number;
        failures: { style: string; error: string }[];
      }; 
      error: any 
    }>(this.config.getFullUrl('/replicate/generate/batch'), request);
  }

  getPredictionStatus(predictionId: string): Observable<{ success: boolean; data: ReplicatePredictionResult; error: any }> {
    return this.http.get<{ success: boolean; data: ReplicatePredictionResult; error: any }>(this.config.getFullUrl(`/replicate/generate/status/${predictionId}`));
  }

  // Basic Tier Generation
  generateBasicImage(request: GenerateBasicImageRequest): Observable<{ 
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
    }>(this.config.generateBasicUrl, request);
  }

  // Credits Management
  getCredits(): Observable<{ success: boolean; data: CreditsInfo; error: any }> {
    return this.http.get<{ success: boolean; data: CreditsInfo; error: any }>(this.config.replicateCreditsUrl);
  }

  // Model Availability Check
  checkModelAvailability(modelId: string): Observable<{ success: boolean; data: { available: boolean }; error: any }> {
    return this.http.get<{ success: boolean; data: { available: boolean }; error: any }>(
      this.config.getFullUrl(`/replicate/model/availability/${encodeURIComponent(modelId)}`)
    );
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