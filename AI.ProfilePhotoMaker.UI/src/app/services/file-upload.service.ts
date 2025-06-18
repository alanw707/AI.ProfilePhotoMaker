import { Injectable } from '@angular/core';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ConfigService } from './config.service';

export interface UploadResponse {
  success: boolean;
  message: string;
  uploadedFiles: string[];
  totalFiles: number;
  zipUrl?: string;
}

export interface ProcessedImage {
  id: number;
  originalImageUrl: string;
  processedImageUrl: string;
  style: string;
  userProfileId: number;
  createdAt: Date;
}

@Injectable({
  providedIn: 'root'
})
export class FileUploadService {
  constructor(private http: HttpClient, private config: ConfigService) {}

  uploadImages(files: File[]): Observable<{ progress: number; response?: UploadResponse }> {
    const formData = new FormData();
    
    files.forEach((file, index) => {
      formData.append('images', file, file.name);
    });

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

  getUserImages(): Observable<{ success: boolean; data: ProcessedImage[]; error: any }> {
    return this.http.get<{ success: boolean; data: ProcessedImage[]; error: any }>(this.config.getFullUrl('/profile/images'));
  }

  deleteImage(imageId: number): Observable<{ success: boolean; message: string }> {
    return this.http.delete<{ success: boolean; message: string }>(this.config.getFullUrl(`/profile/images/${imageId}`));
  }

  getTrainingStatus(): Observable<{ success: boolean; data: any; error: any }> {
    return this.http.get<{ success: boolean; data: any; error: any }>(this.config.getFullUrl('/profile/training-status'));
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