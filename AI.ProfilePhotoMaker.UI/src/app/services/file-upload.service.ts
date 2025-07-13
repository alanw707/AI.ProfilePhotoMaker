import { Injectable } from '@angular/core';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { map, Observable, of, tap, catchError, mergeMap } from 'rxjs';
import { ConfigService } from './config.service';
import { ImageUrlService } from './image-url.service';

export interface UploadResponse {
  profileId: number;
  uploadedFiles: {
    fileName: string;
    size: number;
    url: string;
  }[];
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
  providedIn: 'root',
})
export class FileUploadService {
  private userImagesCache: UserImagesResponse | null = null;
  private userImagesCacheExpiry = 0;
  private readonly USER_IMAGES_CACHE_DURATION = 60000; // 60 seconds

  constructor(
    private http: HttpClient,
    private config: ConfigService,
    private imageUrlService: ImageUrlService
  ) {}

  uploadImages(
    files: File[],
    profileData?: {
      firstName?: string;
      lastName?: string;
      gender?: string;
      ethnicity?: string;
    },
    forTraining = true
  ): Observable<{ progress: number; response?: UploadResponse }> {
    const formData = new FormData();

    files.forEach((file, index) => {
      formData.append('images', file, file.name);
    });

    // Add optional profile data
    if (profileData) {
      if (profileData.firstName) {
        formData.append('firstName', profileData.firstName);
      }
      if (profileData.lastName) {
        formData.append('lastName', profileData.lastName);
      }
      if (profileData.gender) {
        formData.append('gender', profileData.gender);
      }
      if (profileData.ethnicity) {
        formData.append('ethnicity', profileData.ethnicity);
      }
    }

    // Add forTraining flag
    formData.append('forTraining', forTraining.toString());

    return this.http
      .post<UploadResponse>(
        this.config.getFullUrl(this.config.apiConfig.endpoints.image.upload),
        formData,
        {
          reportProgress: true,
          observe: 'events',
        }
      )
      .pipe(
        map(event => {
          switch (event.type) {
            case HttpEventType.UploadProgress:
              const progress = event.total ? Math.round((100 * event.loaded) / event.total) : 0;
              return { progress };
            case HttpEventType.Response:
              // API returns wrapped response: { success: true, data: {...} }
              const apiResponse = event.body as any;
              if (apiResponse?.success && apiResponse?.data) {
                // Transform API response to match UploadResponse interface
                const data = apiResponse.data;
                const transformedResponse: UploadResponse = {
                  profileId: data.ProfileId || data.profileId,
                  uploadedFiles: data.UploadedFiles || data.uploadedFiles || [],
                  uploadedImageIds: data.UploadedImageIds || data.uploadedImageIds || [],
                  zipCreated: data.ZipCreated || data.zipCreated || false,
                  zipPath: data.ZipPath || data.zipPath || '',
                  message: data.Message || data.message || '',
                };
                return { progress: 100, response: transformedResponse };
              } else {
                // Fallback for unexpected response structure
                console.error('Unexpected upload response structure:', apiResponse);
                return { progress: 100, response: event.body as UploadResponse };
              }
            default:
              return { progress: 0 };
          }
        })
      );
  }

  getUserImages(forceRefresh = false): Observable<{ success: boolean; data: UserImagesResponse }> {
    const now = Date.now();

    // Return cached data if available and not expired
    if (!forceRefresh && this.userImagesCache && now < this.userImagesCacheExpiry) {
      console.log('💾 Using cached user images data');
      return of({ success: true, data: this.userImagesCache });
    }

    console.log('🌐 Fetching fresh user images data from API');
    return this.http
      .get<{
        success: boolean;
        data: UserImagesResponse;
      }>(this.config.getFullUrl(this.config.apiConfig.endpoints.image.images))
      .pipe(
        map(response => {
          if (response.success && response.data) {
            // Normalize image URLs to use proxy
            response.data.images = response.data.images.map(image => ({
              ...image,
              originalImageUrl: this.imageUrlService.normalizeImageUrl(image.originalImageUrl),
              processedImageUrl: this.imageUrlService.normalizeImageUrl(image.processedImageUrl),
            }));
          }
          return response;
        }),
        tap(response => {
          if (response.success && response.data) {
            this.userImagesCache = response.data;
            this.userImagesCacheExpiry = now + this.USER_IMAGES_CACHE_DURATION;
            console.log(
              `📊 Cached user images: ${response.data.totalImages} total, ${response.data.generatedImages} generated`
            );
          }
        })
      );
  }

  deleteImage(
    imageId: number
  ): Observable<{ success: boolean; message: string; repairTriggered?: boolean }> {
    return this.http
      .delete<{
        success: boolean;
        message: string;
      }>(`${this.config.getFullUrl(this.config.apiConfig.endpoints.image.images)}/${imageId}`)
      .pipe(
        tap(() => {
          // Invalidate cache when image is deleted
          this.invalidateUserImagesCache();
        })
        // Removed auto-repair on delete errors - let the UI handle delete errors normally
        // Repair should only be triggered by validation, not by user delete actions
      );
  }

  // Cache management methods
  invalidateUserImagesCache(): void {
    console.log('🗑️ Invalidating user images cache');
    this.userImagesCache = null;
    this.userImagesCacheExpiry = 0;
  }

  refreshUserImagesCache(): Observable<{ success: boolean; data: UserImagesResponse }> {
    return this.getUserImages(true);
  }

  getTrainingStatus(): Observable<TrainingStatusResponse> {
    return this.http.get<TrainingStatusResponse>(
      this.config.getFullUrl('/profile/training-status')
    );
  }

  createTrainingZip(): Observable<{
    success: boolean;
    zipCreated: boolean;
    zipPath: string;
    message: string;
    error?: any;
  }> {
    return this.http.post<{
      success: boolean;
      zipCreated: boolean;
      zipPath: string;
      message: string;
      error?: any;
    }>(this.config.getFullUrl(this.config.apiConfig.endpoints.image.createTrainingZip), {});
  }

  listTrainingFiles(): Observable<{ success: boolean; data: string[]; error: any }> {
    return this.http.get<{ success: boolean; data: string[]; error: any }>(
      this.config.getFullUrl(this.config.apiConfig.endpoints.image.trainingZips)
    );
  }

  deleteTrainingFile(fileName: string): Observable<{ success: boolean; message: string }> {
    return this.http.delete<{ success: boolean; message: string }>(
      this.config.getFullUrl(
        `${this.config.apiConfig.endpoints.image.trainingZips}/${encodeURIComponent(fileName)}`
      )
    );
  }

  deleteAllTrainingFiles(): Observable<{ success: boolean; message: string }> {
    return this.http.delete<{ success: boolean; message: string }>(
      this.config.getFullUrl(this.config.apiConfig.endpoints.image.trainingZips)
    );
  }

  getLatestTrainingZip(): Observable<{
    success: boolean;
    data: { fileName: string; publicUrl: string; createdAt: string; sizeBytes: number };
    error?: any;
  }> {
    return this.http.get<{
      success: boolean;
      data: { fileName: string; publicUrl: string; createdAt: string; sizeBytes: number };
      error?: any;
    }>(this.config.getFullUrl(this.config.apiConfig.endpoints.image.latestTrainingZip));
  }

  setTrainedModel(
    modelId: string,
    versionId?: string,
    verifyExists = true
  ): Observable<{ success: boolean; data?: any; error?: any }> {
    return this.http.post<{ success: boolean; data?: any; error?: any }>(
      this.config.getFullUrl('/profile/set-model'),
      { modelId, versionId, verifyExists }
    );
  }

  checkModelStatus(): Observable<{ success: boolean; data?: any; error?: any }> {
    return this.http.post<{ success: boolean; data?: any; error?: any }>(
      this.config.getFullUrl('/profile/check-model-status'),
      {}
    );
  }

  getUserModelRequests(): Observable<{
    success: boolean;
    data?: {
      totalRequests: number;
      hasTrainedModel: boolean;
      latestTrainedModel: any;
      allRequests: any[];
    };
    error?: any;
  }> {
    return this.http.get<{
      success: boolean;
      data?: {
        totalRequests: number;
        hasTrainedModel: boolean;
        latestTrainedModel: any;
        allRequests: any[];
      };
      error?: any;
    }>(this.config.getFullUrl('/model-creation/user/current'));
  }

  // Debug methods
  getDebugModelStatus(): Observable<{ success: boolean; data?: any; error?: any }> {
    return this.http.get<{ success: boolean; data?: any; error?: any }>(
      this.config.getFullUrl('/debug/user-model-status')
    );
  }

  testModelCreationEndpoint(): Observable<{ success: boolean; data?: any; error?: any }> {
    return this.http.get<{ success: boolean; data?: any; error?: any }>(
      this.config.getFullUrl('/debug/test-model-creation-endpoint')
    );
  }

  discoverUserModels(): Observable<{ success: boolean; data?: any; error?: any }> {
    return this.http.get<{ success: boolean; data?: any; error?: any }>(
      this.config.getFullUrl('/debug/discover-user-models')
    );
  }

  testSpecificModel(): Observable<{ success: boolean; data?: any; error?: any }> {
    return this.http.get<{ success: boolean; data?: any; error?: any }>(
      this.config.getFullUrl('/debug/test-specific-model')
    );
  }

  uploadSingleImage(file: File): Observable<{
    progress: number;
    response?: { success: boolean; data: { url: string; fileName: string } };
  }> {
    const formData = new FormData();
    formData.append('images', file, file.name);
    formData.append('forTraining', 'false');

    return this.http
      .post<any>(this.config.getFullUrl(this.config.apiConfig.endpoints.image.upload), formData, {
        reportProgress: true,
        observe: 'events',
      })
      .pipe(
        map(event => {
          switch (event.type) {
            case HttpEventType.UploadProgress:
              const progress = event.total ? Math.round((100 * event.loaded) / event.total) : 0;
              return { progress };
            case HttpEventType.Response:
              // Extract the first uploaded file URL from the response
              const response = event.body;
              console.log('Upload API response:', response);

              if (response?.uploadedFiles && response.uploadedFiles.length > 0) {
                const uploadedFile = response.uploadedFiles[0];
                console.log('Uploaded file details:', uploadedFile);
                return {
                  progress: 100,
                  response: {
                    success: true,
                    data: {
                      url: uploadedFile.url,
                      fileName: uploadedFile.fileName,
                    },
                  },
                };
              }
              console.log(
                'Upload response parsing failed. Response structure:',
                JSON.stringify(response, null, 2)
              );
              return {
                progress: 100,
                response: { success: false, data: { url: '', fileName: '' } },
              };
            default:
              return { progress: 0 };
          }
        })
      );
  }

  repairImageDatabase(): Observable<{ success: boolean; message: string; data?: any }> {
    return this.http
      .post<{
        success: boolean;
        message: string;
        data?: any;
      }>(this.config.getFullUrl('/api/image/reconcile-database?dryRun=false'), {})
      .pipe(
        tap(response => {
          if (response.success) {
            // Invalidate cache after repair to reload fresh data
            this.invalidateUserImagesCache();
            console.log('🔧 Image database repair completed:', response.message);
          }
        })
      );
  }
}
