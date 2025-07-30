import { Observable } from 'rxjs';
import * as faceapi from 'face-api.js';

// Quality and Face Detection Interfaces
export interface QualityScore {
  overall: number; // 1-100
  breakdown: {
    faceQuality: number;
    technical: number;
    composition: number;
    fluxCompatibility: number;
    lighting: number;
  };
  suggestions: string[];
}

export interface FaceValidationResult {
  isValid: boolean;
  faceCount: number;
  bodyType: 'headshot' | 'upper-body' | 'full-body' | 'invalid';
  qualityScore: QualityScore;
  errors: string[];
  warnings: string[];
}

// Model Loader Interface
export interface IModelLoaderService {
  loadModels(): Promise<void>;
  areModelsLoaded(): boolean;
}

// Image Quality Service Interface
export interface IImageQualityService {
  calculateQualityScore(
    img: HTMLImageElement,
    detections: faceapi.WithFaceLandmarks<any>[],
    file: File
  ): Promise<QualityScore>;
  getDefaultQualityScore(): QualityScore;
  loadImageElement(file: File): Promise<HTMLImageElement>;
}

// Face Detection Service Interface
export interface IFaceDetectionService {
  validateImage(file: File): Promise<FaceValidationResult>;
}

// Cache Manager Interfaces
export interface CacheEntry<T> {
  data: T;
  expiry: number;
  lastAccessed: number;
}

export interface CacheStats {
  totalEntries: number;
  validEntries: number;
  expiredEntries: number;
  cacheHitRatio: number;
}

export interface ICacheManagerService {
  isCacheValid(cacheKey: string): boolean;
  getCachedData<T>(cacheKey: string): T | null;
  setCachedData<T>(cacheKey: string, data: T, durationMs: number): void;
  invalidateCache(cacheKey: string): void;
  invalidateAllCache(): void;
  shouldDebounceRequest(requestKey: string, debounceMs: number): boolean;
  forceRefresh(cacheKey?: string): void;
  getCacheStats(): CacheStats;
  enableGlobalDebug(): void;
}

// Model State Service Interface
export interface ModelStatusInfo {
  modelStatus: string;
  hasTrainedModel: boolean;
  latestTrainedModel: any;
}

export interface IModelStateService {
  runAsyncModelDiscovery(): void;
  updateModelStatus(): void;
  debugModelStatus(): Promise<any>;
  triggerModelDiscovery(): Promise<any>;
  isModelDiscoveryNeeded(
    hasTrainedModel: boolean,
    modelStatus: string,
    uploadedImages: number
  ): boolean;
  getModelStatusFromData(modelRequestsData: any, trainingStatus: any): ModelStatusInfo;
  enableGlobalDebug(): void;
}

// Fallback Operations Service Interfaces
export interface FallbackTracker {
  filesystemCheck: boolean;
  modelDiscovery: boolean;
  lastReset: number;
}

export interface DataDiscrepancyResult {
  dashboardCount: number;
  apiGeneratedField: number;
  filteredCount: number;
  totalImages: number;
  allImages: any[];
}

export interface FallbackCheckResult {
  shouldCheckFilesystem: boolean;
  shouldDiscoverModels: boolean;
}

export interface DashboardStateForFallback {
  generatedPhotosCount: number;
  modelStatus: string;
  hasLatestTrainedModel: boolean;
  uploadedImages: number;
  hasUserProfile: boolean;
  latestTrainedModel: any;
}

export interface IFallbackOperationsService {
  checkGeneratedImagesFromFilesystem(): Observable<any>;
  isFilesystemCheckNeeded(
    generatedPhotosCount: number,
    hasTrainedModel: boolean,
    userProfile: any
  ): boolean;
  isModelDiscoveryNeeded(
    modelStatus: string,
    uploadedImages: number,
    hasTrainedModel: boolean
  ): boolean;
  checkIfFallbackNeeded(state: DashboardStateForFallback): FallbackCheckResult;
  debugDataDiscrepancy(): Promise<DataDiscrepancyResult>;
  resetFallbackTracking(): void;
  getFallbackTracker(): FallbackTracker;
  markFilesystemCheckCompleted(): void;
  markModelDiscoveryCompleted(): void;
  enableGlobalDebug(): void;
}

// Dashboard State Service Interface
export interface UploadedImageThumbnail {
  id: number;
  url: string;
  fileName: string;
}

export interface DashboardState {
  userProfile: any | null;
  creditsInfo: any | null;
  userCreditStatus: any | null;
  uploadedImages: number;
  uploadedImageThumbnails: UploadedImageThumbnail[];
  generatedPhotosCount: number;
  modelStatus: string;
  isPremiumWorkflow: boolean;
  isLoading: boolean;
  latestTrainedModel?: any;
  totalCredits?: number;
  imagesValidated?: boolean; // Track if images have been validated
  lastValidationTime?: number; // Timestamp of last validation
}

export interface IDashboardStateService {
  getState(): DashboardState;
  setState(newState: Partial<DashboardState>): void;
  loadInitialDashboardData(): void;
  resetState(): void;
  forceRefresh(): void;
  invalidateAndRefreshImages(): void;
  refreshGeneratedPhotosCount(): void;
}

// Service Configuration Interface
export interface ServiceConfiguration {
  cacheEnabled: boolean;
  cacheDurationMs: number;
  debugMode: boolean;
  fallbackEnabled: boolean;
  modelAutoDiscovery: boolean;
}

// Error Handling Interfaces
export interface ServiceError {
  code: string;
  message: string;
  details?: any;
  timestamp: Date;
}

export interface ServiceResult<T> {
  success: boolean;
  data?: T;
  error?: ServiceError;
}

// Common service operation interfaces
export interface IServiceWithDebug {
  enableGlobalDebug(): void;
}

export interface IServiceWithCache {
  invalidateCache(): void;
  getCacheInfo(): any;
}

export interface IServiceWithConfiguration {
  configure(config: Partial<ServiceConfiguration>): void;
  getConfiguration(): ServiceConfiguration;
}

// Union types for service identification
export type RefactoredServices =
  | IModelLoaderService
  | IImageQualityService
  | IFaceDetectionService
  | ICacheManagerService
  | IModelStateService
  | IFallbackOperationsService
  | IDashboardStateService;

export type ServiceIdentifier =
  | 'ModelLoaderService'
  | 'ImageQualityService'
  | 'FaceDetectionService'
  | 'CacheManagerService'
  | 'ModelStateService'
  | 'FallbackOperationsService'
  | 'DashboardStateService';
