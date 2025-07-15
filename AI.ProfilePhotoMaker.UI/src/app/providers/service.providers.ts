import { InjectionToken, NgZone, Provider } from '@angular/core';
import { 
  ICacheManagerService,
  IDashboardStateService,
  IFaceDetectionService,
  IFallbackOperationsService,
  IImageQualityService,
  IModelLoaderService,
  IModelStateService
} from '../interfaces/service.interfaces';

import { ModelLoaderService } from '../services/model-loader.service';
import { ImageQualityService } from '../services/image-quality.service';
import { FaceDetectionService } from '../services/face-detection.service';
import { CacheManagerService } from '../services/cache-manager.service';
import { ModelStateService } from '../services/model-state.service';
import { FallbackOperationsService } from '../services/fallback-operations.service';
import { DashboardStateService } from '../services/dashboard-state.service';

// Injection tokens for interfaces
export const MODEL_LOADER_SERVICE = new InjectionToken<IModelLoaderService>('ModelLoaderService');
export const IMAGE_QUALITY_SERVICE = new InjectionToken<IImageQualityService>('ImageQualityService');
export const FACE_DETECTION_SERVICE = new InjectionToken<IFaceDetectionService>('FaceDetectionService');
export const CACHE_MANAGER_SERVICE = new InjectionToken<ICacheManagerService>('CacheManagerService');
export const MODEL_STATE_SERVICE = new InjectionToken<IModelStateService>('ModelStateService');
export const FALLBACK_OPERATIONS_SERVICE = new InjectionToken<IFallbackOperationsService>('FallbackOperationsService');
export const DASHBOARD_STATE_SERVICE = new InjectionToken<IDashboardStateService>('DashboardStateService');

// Service providers configuration
export const SERVICE_PROVIDERS: Provider[] = [
  // Model Loader Service
  {
    provide: MODEL_LOADER_SERVICE,
    useClass: ModelLoaderService
  },
  ModelLoaderService, // Also provide the concrete class for direct injection

  // Image Quality Service
  {
    provide: IMAGE_QUALITY_SERVICE,
    useClass: ImageQualityService
  },
  ImageQualityService, // Also provide the concrete class for direct injection

  // Face Detection Service
  {
    provide: FACE_DETECTION_SERVICE,
    useClass: FaceDetectionService
  },
  FaceDetectionService, // Also provide the concrete class for direct injection

  // Cache Manager Service
  {
    provide: CACHE_MANAGER_SERVICE,
    useClass: CacheManagerService
  },
  CacheManagerService, // Also provide the concrete class for direct injection

  // Model State Service
  {
    provide: MODEL_STATE_SERVICE,
    useClass: ModelStateService
  },
  ModelStateService, // Also provide the concrete class for direct injection

  // Fallback Operations Service
  {
    provide: FALLBACK_OPERATIONS_SERVICE,
    useClass: FallbackOperationsService
  },
  FallbackOperationsService, // Also provide the concrete class for direct injection

  // Dashboard State Service
  {
    provide: DASHBOARD_STATE_SERVICE,
    useClass: DashboardStateService
  },
  DashboardStateService // Also provide the concrete class for direct injection
];

// Factory functions for creating service instances with specific configurations
export function createModelLoaderService(ngZone: NgZone): IModelLoaderService {
  // Could add configuration here if needed
  return new ModelLoaderService(ngZone);
}

export function createImageQualityService(ngZone: NgZone): IImageQualityService {
  // Could add configuration here if needed
  return new ImageQualityService(ngZone);
}

export function createCacheManagerService(): ICacheManagerService {
  // Could add configuration here if needed
  return new CacheManagerService();
}

// Service configuration helper
export class ServiceConfiguration {
  static configure(config: {
    enableDebugMode?: boolean;
    enableGlobalDebug?: boolean;
    cacheEnabled?: boolean;
    fallbackEnabled?: boolean;
  }) {
    // This could be used to configure services globally
    return {
      ...config,
      enableDebugMode: config.enableDebugMode ?? false,
      enableGlobalDebug: config.enableGlobalDebug ?? false,
      cacheEnabled: config.cacheEnabled ?? true,
      fallbackEnabled: config.fallbackEnabled ?? true
    };
  }
}

// Helper function to inject services by interface - removed invalid implementation

// Service registry for debugging and testing
export const SERVICE_REGISTRY = {
  MODEL_LOADER_SERVICE,
  IMAGE_QUALITY_SERVICE,
  FACE_DETECTION_SERVICE,
  CACHE_MANAGER_SERVICE,
  MODEL_STATE_SERVICE,
  FALLBACK_OPERATIONS_SERVICE,
  DASHBOARD_STATE_SERVICE
} as const;

// Type guard functions for service identification
export function isModelLoaderService(service: any): service is IModelLoaderService {
  return service && typeof service.loadModels === 'function' && typeof service.areModelsLoaded === 'function';
}

export function isImageQualityService(service: any): service is IImageQualityService {
  return service && typeof service.calculateQualityScore === 'function' && typeof service.getDefaultQualityScore === 'function';
}

export function isFaceDetectionService(service: any): service is IFaceDetectionService {
  return service && typeof service.validateImage === 'function';
}

export function isCacheManagerService(service: any): service is ICacheManagerService {
  return service && typeof service.getCachedData === 'function' && typeof service.setCachedData === 'function';
}

export function isModelStateService(service: any): service is IModelStateService {
  return service && typeof service.runAsyncModelDiscovery === 'function' && typeof service.updateModelStatus === 'function';
}

export function isFallbackOperationsService(service: any): service is IFallbackOperationsService {
  return service && typeof service.checkGeneratedImagesFromFilesystem === 'function' && typeof service.checkIfFallbackNeeded === 'function';
}

export function isDashboardStateService(service: any): service is IDashboardStateService {
  return service && typeof service.getState === 'function' && typeof service.setState === 'function';
}