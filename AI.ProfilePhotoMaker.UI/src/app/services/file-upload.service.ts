import { Injectable } from '@angular/core';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ConfigService } from './config.service';

export interface UploadResponse {
  profileId: number;
  uploadedFiles: Array<{
    fileName: string;
    size: number;
    url: string;
  }>;
  uploadedImageIds: number[];
  zipCreated: boolean;
  zipPath: string;
  message: string;
}

export interface ProcessedImage {
  id: number;
  originalImageUrl: string;
  processedImageUrl: string;
  style: string;
  createdAt: string;
  isOriginalUpload: boolean;
  isGenerated: boolean;
  fileExists: boolean;
}

export interface UserImagesResponse {
  totalImages: number;
  originalUploads: number;
  generatedImages: number;
  images: ProcessedImage[];
}

export interface TrainingStatusResponse {
  profileId: number;
  hasTrainedModel: boolean;
  trainedModelId: string;
  modelTrainedAt: string;
  totalUploadedImages: number;
  latestZipFile: string;
  canStartTraining: boolean;
  status: string;
}

@Injectable({
  providedIn: 'root'
})
export class FileUploadService {
  constructor(private http: HttpClient, private config: ConfigService) {}

  uploadImages(files: File[], profileData?: {
    firstName?: string;
    lastName?: string;
    gender?: string;
    ethnicity?: string;
  }): Observable<{ progress: number; response?: UploadResponse }> {
    const formData = new FormData();
    
    files.forEach((file, index) => {
      formData.append('images', file, file.name);
    });

    // Add optional profile data
    if (profileData) {
      if (profileData.firstName) formData.append('firstName', profileData.firstName);
      if (profileData.lastName) formData.append('lastName', profileData.lastName);
      if (profileData.gender) formData.append('gender', profileData.gender);
      if (profileData.ethnicity) formData.append('ethnicity', profileData.ethnicity);
    }

    return this.http.post<UploadResponse>(this.config.uploadImagesUrl, formData, {
      reportProgress: true,
      observe: 'events'
    }).pipe(
      map(event => {
        switch (event.type) {
          case HttpEventType.UploadProgress:
            const progress = event.total ? Math.round(100 * event.loaded / event.total) : 0;
            return { progress };
          case HttpEventType.Response:
            return { progress: 100, response: event.body as UploadResponse };
          default:
            return { progress: 0 };
        }
      })
    );
  }

  getUserImages(): Observable<UserImagesResponse> {
    return this.http.get<UserImagesResponse>(this.config.getFullUrl('/profile/images'));
  }

  deleteImage(imageId: number): Observable<{ success: boolean; message: string }> {
    return this.http.delete<{ success: boolean; message: string }>(this.config.getFullUrl(`/profile/images/${imageId}`));
  }

  getTrainingStatus(): Observable<TrainingStatusResponse> {
    return this.http.get<TrainingStatusResponse>(this.config.getFullUrl('/profile/training-status'));
  }

  listTrainingFiles(): Observable<{ success: boolean; data: string[]; error: any }> {
    return this.http.get<{ success: boolean; data: string[]; error: any }>(this.config.getFullUrl('/profile/training-files'));
  }

  deleteTrainingFile(fileName: string): Observable<{ success: boolean; message: string }> {
    return this.http.delete<{ success: boolean; message: string }>(this.config.getFullUrl(`/profile/training-files/${encodeURIComponent(fileName)}`));
  }

  deleteAllTrainingFiles(): Observable<{ success: boolean; message: string }> {
    return this.http.delete<{ success: boolean; message: string }>(this.config.getFullUrl('/profile/training-files'));
  }
}