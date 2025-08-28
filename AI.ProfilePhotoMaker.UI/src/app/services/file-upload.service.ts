import { Injectable } from '@angular/core';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { catchError, map, Observable, of, tap, timeout } from 'rxjs';
import { ConfigService } from './config.service';
import { ImageUrlService } from './image-url.service';
import { AuthService } from './auth.service';

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

export type UnifiedModelStatusCode =
  | 'NotStarted'
  | 'ReadyForTraining'
  | 'Training'
  | 'ModelReady'
  | 'Failed';

export interface UnifiedModelStatusResponse {
  statusCode: UnifiedModelStatusCode;
  hasTrainedModel: boolean;
  trainedModelId?: string | null;
  trainedModelVersion?: string | null;
  totalUploadedImages: number;
  canStartTraining: boolean;
  reason?: string | null;
  lastUpdated?: string;
  currentRequest?: {
    id: string;
    status: string;
    createdAt: string;
    completedAt?: string | null;
    errorMessage?: string | null;
  } | null;
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
    private imageUrlService: ImageUrlService,
    private authService: AuthService
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

    files.forEach(file => {
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

    // Add authentication headers using production-ready AuthService
    const headers: any = {};
    const token = this.authService.getToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    return this.http
      .post<UploadResponse>(
        this.config.getFullUrl(this.config.apiConfig.endpoints.image.upload),
        formData,
        {
          reportProgress: true,
          observe: 'events',
          headers,
        }
      )
      .pipe(
        map(event => {
          switch (event.type) {
            case HttpEventType.UploadProgress: {
              const progress = event.total ? Math.round((100 * event.loaded) / event.total) : 0;
              return { progress };
            }
            case HttpEventType.Response: {
              // API returns wrapped response: { success: true, data: {...} }
              const apiResponse = event.body as unknown;
              if (
                apiResponse &&
                typeof apiResponse === 'object' &&
                'success' in apiResponse &&
                'data' in apiResponse &&
                (apiResponse as any).success &&
                (apiResponse as any).data
              ) {
                // Transform API response to match UploadResponse interface
                const data = (apiResponse as any).data;
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
      return of({ success: true, data: this.userImagesCache });
    }

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
    this.userImagesCache = null;
    this.userImagesCacheExpiry = 0;
  }

  refreshUserImagesCache(): Observable<{ success: boolean; data: UserImagesResponse }> {
    return this.getUserImages(true);
  }

  getTrainingStatus(): Observable<TrainingStatusResponse> {
    return this.http.get<any>(this.config.getFullUrl('/profile/training-status')).pipe(
      // Normalize server casing (API returns PascalCase keys)
      map(raw => {
        const normalized: TrainingStatusResponse = {
          profileId: raw?.profileId ?? raw?.ProfileId ?? 0,
          hasTrainedModel: raw?.hasTrainedModel ?? raw?.HasTrainedModel ?? false,
          trainedModelId: raw?.trainedModelId ?? raw?.TrainedModelId ?? null,
          modelTrainedAt: raw?.modelTrainedAt ?? raw?.ModelTrainedAt ?? null,
          totalUploadedImages: raw?.totalUploadedImages ?? raw?.TotalUploadedImages ?? 0,
          latestZipFile: raw?.latestZipFile ?? raw?.LatestZipFile ?? null,
          canStartTraining: raw?.canStartTraining ?? raw?.CanStartTraining ?? false,
          status: raw?.status ?? raw?.Status ?? 'Not Started',
        } as TrainingStatusResponse;
        return normalized;
      })
    );
  }

  getUnifiedModelStatus(): Observable<UnifiedModelStatusResponse> {
    return this.http.get<any>(this.config.getFullUrl('/model-status')).pipe(
      timeout({ first: 5000 }),
      map(raw => {
        const normalized: UnifiedModelStatusResponse = {
          statusCode: (raw?.statusCode ??
            raw?.StatusCode ??
            'NotStarted') as UnifiedModelStatusCode,
          hasTrainedModel: raw?.hasTrainedModel ?? raw?.HasTrainedModel ?? false,
          trainedModelId: raw?.trainedModelId ?? raw?.TrainedModelId ?? null,
          trainedModelVersion: raw?.trainedModelVersion ?? raw?.TrainedModelVersion ?? null,
          totalUploadedImages: raw?.totalUploadedImages ?? raw?.TotalUploadedImages ?? 0,
          canStartTraining: raw?.canStartTraining ?? raw?.CanStartTraining ?? false,
          reason: raw?.reason ?? raw?.Reason ?? null,
          lastUpdated: raw?.lastUpdated ?? raw?.LastUpdated ?? null,
          currentRequest: raw?.currentRequest ?? raw?.CurrentRequest ?? null,
        };
        return normalized;
      })
    );
  }

  createTrainingZip(): Observable<{
    success: boolean;
    zipCreated: boolean;
    zipPath: string;
    message: string;
    error?: any;
  }> {
    // Attach auth like other protected endpoints
    const headers: any = {};
    const token = this.authService.getToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    // API returns a standard wrapper: { success, data: { ZipCreated, ZipPath, Message }, message?, error? }
    // Normalize to the flattened shape used by orchestrator.
    return this.http
      .post<any>(
        this.config.getFullUrl(this.config.apiConfig.endpoints.image.createTrainingZip),
        {},
        { headers }
      )
      .pipe(
        map(response => {
          const success = !!response?.success;
          const data = response?.data || {};
          const zipCreated = Boolean(data.ZipCreated ?? data.zipCreated ?? false);
          const zipPath = String(data.ZipPath ?? data.zipPath ?? '');
          const message = String(response?.message ?? data.Message ?? data.message ?? '');
          return { success, zipCreated, zipPath, message, error: response?.error } as {
            success: boolean;
            zipCreated: boolean;
            zipPath: string;
            message: string;
            error?: any;
          };
        }),
        catchError(err => {
          // Surface a normalized error shape to callers
          return of({
            success: false,
            zipCreated: false,
            zipPath: '',
            message: err?.error?.error?.message || err?.message || 'Failed to create training ZIP',
            error: err?.error?.error || err?.error || err,
          });
        })
      );
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
  // (Removed obsolete debug endpoints without backend support)

  uploadSingleImage(
    file: File,
    isEnhanced = true
  ): Observable<{
    progress: number;
    response?: { success: boolean; data: { url: string; fileName: string } };
  }> {
    const formData = new FormData();
    formData.append('images', file, file.name);
    formData.append('forTraining', 'false');
    formData.append('isEnhanced', isEnhanced.toString());

    // Add authentication headers using production-ready AuthService
    const headers: any = {};
    const token = this.authService.getToken();
    console.log('Authentication check:', {
      tokenExists: !!token,
      tokenPrefix: token?.substring(0, 20) + '...',
      tokenLength: token?.length,
    });
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    } else {
      console.warn('No authentication token found - upload may fail');
    }

    return this.http
      .post<any>(this.config.getFullUrl(this.config.apiConfig.endpoints.image.upload), formData, {
        reportProgress: true,
        observe: 'events',
        headers,
      })
      .pipe(
        map(event => {
          switch (event.type) {
            case HttpEventType.UploadProgress: {
              const progress = event.total ? Math.round((100 * event.loaded) / event.total) : 0;
              return { progress };
            }
            case HttpEventType.Response: {
              // Extract the first uploaded file URL from the response
              const response = event.body;
              console.log('Upload API response:', response);
              console.log('Response structure validation:', {
                hasSuccess: !!response?.success,
                hasData: !!response?.data,
                hasUploadedFiles: !!response?.data?.UploadedFiles,
                uploadedFilesLength: response?.data?.UploadedFiles?.length || 0,
                responseKeys: Object.keys(response || {}),
                dataKeys: Object.keys(response?.data || {}),
              });

              // Handle standard API response format: { success: true, data: {...} }
              // Check both uppercase and lowercase variations
              const uploadedFiles = response?.data?.UploadedFiles || response?.data?.uploadedFiles;
              if (response?.success && uploadedFiles && uploadedFiles.length > 0) {
                const uploadedFile = uploadedFiles[0];
                console.log('Uploaded file details:', uploadedFile);
                console.log('File URL extraction:', {
                  originalUrl: uploadedFile.Url,
                  fallbackUrl: uploadedFile.url,
                  finalUrl: uploadedFile.Url || uploadedFile.url,
                });
                return {
                  progress: 100,
                  response: {
                    success: true,
                    data: {
                      url: uploadedFile.Url || uploadedFile.url,
                      fileName: uploadedFile.FileName || uploadedFile.fileName,
                    },
                  },
                };
              }

              // Fallback: try legacy format (direct response structure)
              const legacyUploadedFiles = response?.uploadedFiles || response?.UploadedFiles;
              if (legacyUploadedFiles && legacyUploadedFiles.length > 0) {
                const uploadedFile = legacyUploadedFiles[0];
                console.log('Uploaded file details (legacy):', uploadedFile);
                return {
                  progress: 100,
                  response: {
                    success: true,
                    data: {
                      url: uploadedFile.url || uploadedFile.Url,
                      fileName: uploadedFile.fileName || uploadedFile.FileName,
                    },
                  },
                };
              }

              console.error('Upload response parsing failed. Response structure:', {
                fullResponse: JSON.stringify(response, null, 2),
                responseType: typeof response,
                hasSuccess: 'success' in (response || {}),
                successValue: response?.success,
                hasData: 'data' in (response || {}),
                dataType: typeof response?.data,
                possibleUploadedFiles:
                  response?.data?.uploadedFiles ||
                  response?.uploadedFiles ||
                  response?.UploadedFiles,
              });

              // Enhanced fallback - check if response has success=false with error details
              if (response?.success === false) {
                console.error(
                  'API returned success=false:',
                  response?.error || response?.message || 'No error details'
                );
              }

              return {
                progress: 100,
                response: { success: false, data: { url: '', fileName: '' } },
              };
            }
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

  /**
   * Delete temporary enhanced image file after successful enhancement
   * @param fileName - The file name to delete
   */
  deleteTemporaryEnhancedImage(
    fileName: string
  ): Observable<{ success: boolean; message: string }> {
    console.log('🗑️ Attempting to delete enhanced image file:', fileName);

    // Add authentication headers using production-ready AuthService
    const headers: any = {};
    const token = this.authService.getToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    } else {
      console.warn('No authentication token found - delete may fail');
    }

    return this.http
      .delete<{
        success: boolean;
        message: string;
      }>(this.config.getFullUrl(`/api/image/enhanced/${encodeURIComponent(fileName)}`), { headers })
      .pipe(
        tap(response => {
          if (response.success) {
            console.log('✅ Enhanced image file deleted successfully:', fileName);
          } else {
            console.warn('⚠️ Enhanced image deletion failed:', response.message);
          }
        }),
        catchError(error => {
          console.error('❌ Error deleting enhanced image:', error);
          // Return a graceful fallback - cleanup failure shouldn't break the user experience
          return of({
            success: false,
            message: `Failed to delete enhanced image: ${error.message || 'Unknown error'}`,
          });
        })
      );
  }
}
