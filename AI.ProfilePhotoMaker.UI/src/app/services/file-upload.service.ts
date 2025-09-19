import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { catchError, map, Observable, of, tap, timeout } from 'rxjs';
import { ConfigService } from './config.service';
import { ImageUrlService } from './image-url.service';
import { AuthService } from './auth.service';
import { LoggingService } from './logging.service';

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

export interface SaveEnhancedImageResponse {
  success: boolean;
  data?: {
    id: number;
    fileName: string;
    url: string;
    enhancementType: string;
    createdAt: string;
    message: string;
  };
  error?: {
    code: string;
    message: string;
  };
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
  private readonly logger = inject(LoggingService);

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
                this.logger.error('Unexpected upload response structure', apiResponse);
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
            this.logger.fileDebug('cached user images snapshot', 'user-images', {
              totalImages: response.data.totalImages,
              generatedImages: response.data.generatedImages,
            });
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
    this.logger.authDebug('Resolved upload token for image upload', {
      tokenExists: !!token,
      tokenPreview: token ? `${token.slice(0, 8)}…` : null,
      tokenLength: token?.length ?? 0,
    });
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    } else {
      this.logger.warn('No authentication token found before upload request; upload may fail');
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
              this.logger.fileDebug('upload-single-image response received', file.name, {
                success: response?.success,
                hasData: !!response?.data,
                uploadedFilesCount:
                  response?.data?.UploadedFiles?.length ??
                  response?.data?.uploadedFiles?.length ??
                  0,
                responseKeys: Object.keys(response || {}),
                dataKeys: Object.keys(response?.data || {}),
              });

              // Handle standard API response format: { success: true, data: {...} }
              // Check both uppercase and lowercase variations
              const uploadedFiles = response?.data?.UploadedFiles || response?.data?.uploadedFiles;
              if (response?.success && uploadedFiles && uploadedFiles.length > 0) {
                const uploadedFile = uploadedFiles[0];
                this.logger.fileDebug(
                  'upload-single-image parsed file',
                  uploadedFile.FileName || uploadedFile.fileName || file.name,
                  {
                    originalUrl: uploadedFile.Url,
                    fallbackUrl: uploadedFile.url,
                    finalUrl: uploadedFile.Url || uploadedFile.url,
                  }
                );
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
                this.logger.fileDebug(
                  'upload-single-image legacy response path',
                  uploadedFile.FileName || uploadedFile.fileName || file.name,
                  uploadedFile
                );
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

              this.logger.error('Upload response parsing failed for upload response', {
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
                this.logger.error(
                  'API returned success=false for upload',
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

  /**
   * Saves enhanced image from base64 data to storage and gallery
   */
  async saveEnhancedImage(
    base64ImageData: string,
    enhancementType: string
  ): Promise<SaveEnhancedImageResponse> {
    try {
      const url = this.config.getFullUrl('/image/save-enhanced');
      const headers = this.getAuthHeaders();

      const response = await this.http
        .post<SaveEnhancedImageResponse>(
          url,
          {
            base64ImageData,
            enhancementType,
          },
          {
            headers,
            withCredentials: true, // Include cookies for authentication
          }
        )
        .pipe(
          timeout(30000), // 30 second timeout
          catchError(error => {
            this.logger.error('Error saving enhanced image', error);
            return of({
              success: false,
              error: {
                code: 'SAVE_FAILED',
                message: error?.error?.message || error?.message || 'Failed to save enhanced image',
              },
            });
          })
        )
        .toPromise();

      return (
        response || {
          success: false,
          error: {
            code: 'NO_RESPONSE',
            message: 'No response received from server',
          },
        }
      );
    } catch (error: any) {
      this.logger.error('Error in saveEnhancedImage', error);
      return {
        success: false,
        error: {
          code: 'NETWORK_ERROR',
          message: error?.message || 'Network error occurred',
        },
      };
    }
  }

  private getAuthHeaders() {
    const headers: any = {
      'Content-Type': 'application/json',
    };

    // Note: AuthService uses cookies for authentication, not Authorization headers
    // The HTTP client will automatically include cookies with withCredentials: true
    return headers;
  }

  // UI-initiated image database repair removed to avoid unintended deletions.
}
