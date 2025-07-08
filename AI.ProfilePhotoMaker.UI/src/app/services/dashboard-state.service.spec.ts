import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DashboardStateService } from './dashboard-state.service';
import { ProfileService } from './profile.service';
import { ReplicateService } from './replicate.service';
import { CreditService } from './credit.service';
import { FileUploadService } from './file-upload.service';
import { StyleService } from './style.service';
import { NotificationService } from './notification.service';
import { CacheManagerService } from './cache-manager.service';
import { ModelStateService } from './model-state.service';
import { FallbackOperationsService } from './fallback-operations.service';
import { DashboardState, IDashboardStateService } from '../interfaces/service.interfaces';

// Mock services
class MockProfileService {
  getCurrentUserProfile = jasmine.createSpy('getCurrentUserProfile').and.returnValue(
    of({ success: true, data: { id: 1, name: 'Test User' } })
  );
}

class MockReplicateService {
  getCredits = jasmine.createSpy('getCredits').and.returnValue(
    of({ success: true, data: { totalCredits: 100 } })
  );
}

class MockCreditService {
  getCreditStatus = jasmine.createSpy('getCreditStatus').and.returnValue(
    of({ success: true, data: { purchasedCredits: 50, weeklyCredits: 3 } })
  );
}

class MockFileUploadService {
  getUserImages = jasmine.createSpy('getUserImages').and.returnValue(
    of({
      images: [
        { id: 1, isOriginalUpload: true, originalImageUrl: 'url1', isGenerated: false },
        { id: 2, isOriginalUpload: false, isGenerated: true }
      ],
      generatedImages: 5
    })
  );
  
  getTrainingStatus = jasmine.createSpy('getTrainingStatus').and.returnValue(
    of({ status: 'Not Started', totalUploadedImages: 3 })
  );
  
  getUserModelRequests = jasmine.createSpy('getUserModelRequests').and.returnValue(
    of({ success: true, data: { hasTrainedModel: false, latestTrainedModel: null } })
  );
  
  invalidateUserImagesCache = jasmine.createSpy('invalidateUserImagesCache');
}

class MockStyleService {}

class MockNotificationService {
  error = jasmine.createSpy('error');
}

class MockCacheManagerService {
  getCachedData = jasmine.createSpy('getCachedData').and.returnValue(null);
  setCachedData = jasmine.createSpy('setCachedData');
  shouldDebounceRequest = jasmine.createSpy('shouldDebounceRequest').and.returnValue(false);
  forceRefresh = jasmine.createSpy('forceRefresh');
  invalidateCache = jasmine.createSpy('invalidateCache');
  enableGlobalDebug = jasmine.createSpy('enableGlobalDebug');
  
  static DASHBOARD_CACHE_DURATION_MS = 30000;
  static LOAD_DEBOUNCE_MS = 1000;
}

class MockModelStateService {
  getModelStatusFromData = jasmine.createSpy('getModelStatusFromData').and.returnValue({
    modelStatus: 'Not Started',
    hasTrainedModel: false,
    latestTrainedModel: null
  });
  
  runAsyncModelDiscovery = jasmine.createSpy('runAsyncModelDiscovery');
  enableGlobalDebug = jasmine.createSpy('enableGlobalDebug');
}

class MockFallbackOperationsService {
  resetFallbackTracking = jasmine.createSpy('resetFallbackTracking');
  
  checkIfFallbackNeeded = jasmine.createSpy('checkIfFallbackNeeded').and.returnValue({
    shouldCheckFilesystem: false,
    shouldDiscoverModels: false
  });
  
  checkGeneratedImagesFromFilesystem = jasmine.createSpy('checkGeneratedImagesFromFilesystem').and.returnValue(
    of({ actualGeneratedCount: 10 })
  );
  
  enableGlobalDebug = jasmine.createSpy('enableGlobalDebug');
}

describe('DashboardStateService', () => {
  let service: DashboardStateService;
  let mockCacheManager: MockCacheManagerService;
  let mockModelState: MockModelStateService;
  let mockFallbackOps: MockFallbackOperationsService;
  let mockNotificationService: MockNotificationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        DashboardStateService,
        { provide: ProfileService, useClass: MockProfileService },
        { provide: ReplicateService, useClass: MockReplicateService },
        { provide: CreditService, useClass: MockCreditService },
        { provide: FileUploadService, useClass: MockFileUploadService },
        { provide: StyleService, useClass: MockStyleService },
        { provide: NotificationService, useClass: MockNotificationService },
        { provide: CacheManagerService, useClass: MockCacheManagerService },
        { provide: ModelStateService, useClass: MockModelStateService },
        { provide: FallbackOperationsService, useClass: MockFallbackOperationsService }
      ]
    });
    
    service = TestBed.inject(DashboardStateService);
    mockCacheManager = TestBed.inject(CacheManagerService) as any;
    mockModelState = TestBed.inject(ModelStateService) as any;
    mockFallbackOps = TestBed.inject(FallbackOperationsService) as any;
    mockNotificationService = TestBed.inject(NotificationService) as any;
  });

  afterEach(() => {
    // Reset all spies
    jasmine.clock().uninstall();
  });

  describe('Service Creation', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should implement IDashboardStateService interface', () => {
      expect(service.getState).toBeDefined();
      expect(service.setState).toBeDefined();
      expect(service.loadInitialDashboardData).toBeDefined();
      expect(service.resetState).toBeDefined();
      expect(service.forceRefresh).toBeDefined();
      expect(service.invalidateAndRefreshImages).toBeDefined();
      expect(service.refreshGeneratedPhotosCount).toBeDefined();
      expect(service.enableGlobalDebug).toBeDefined();
    });

    it('should start with initial state', () => {
      const state = service.getState();
      
      expect(state.userProfile).toBeNull();
      expect(state.creditsInfo).toBeNull();
      expect(state.userCreditStatus).toBeNull();
      expect(state.uploadedImages).toBe(0);
      expect(state.uploadedImageThumbnails).toEqual([]);
      expect(state.generatedPhotosCount).toBe(0);
      expect(state.modelStatus).toBe('Not Started');
      expect(state.isPremiumWorkflow).toBeFalse();
      expect(state.isLoading).toBeFalse();
      expect(state.latestTrainedModel).toBeNull();
    });
  });

  describe('getState() and setState()', () => {
    it('should get current state', () => {
      const state = service.getState();
      expect(state).toBeDefined();
      expect(state.isLoading).toBeFalse();
    });

    it('should set partial state', () => {
      const partialState = { isLoading: true, generatedPhotosCount: 5 };
      
      service.setState(partialState);
      
      const newState = service.getState();
      expect(newState.isLoading).toBeTrue();
      expect(newState.generatedPhotosCount).toBe(5);
      // Other properties should remain unchanged
      expect(newState.userProfile).toBeNull();
    });

    it('should emit state changes via observable', (done) => {
      service.state$.subscribe(state => {
        if (state.isLoading) {
          expect(state.isLoading).toBeTrue();
          done();
        }
      });
      
      service.setState({ isLoading: true });
    });

    it('should merge new state with existing state', () => {
      service.setState({ generatedPhotosCount: 3 });
      service.setState({ uploadedImages: 5 });
      
      const state = service.getState();
      expect(state.generatedPhotosCount).toBe(3);
      expect(state.uploadedImages).toBe(5);
    });
  });

  describe('loadInitialDashboardData()', () => {
    it('should use cached data when available', () => {
      const cachedState = { creditsInfo: { totalCredits: 100 } };
      mockCacheManager.getCachedData.and.returnValue(cachedState);
      
      service.loadInitialDashboardData();
      
      expect(mockCacheManager.getCachedData).toHaveBeenCalledWith('dashboard_data');
      // Should not make API calls if cached data is used
    });

    it('should skip loading when debounced', () => {
      mockCacheManager.shouldDebounceRequest.and.returnValue(true);
      
      service.loadInitialDashboardData();
      
      expect(mockCacheManager.shouldDebounceRequest).toHaveBeenCalledWith('dashboard_load', MockCacheManagerService.LOAD_DEBOUNCE_MS);
      // Should return early without loading
    });

    it('should load data from all services when not cached', () => {
      service.loadInitialDashboardData();
      
      // Should set loading state
      expect(service.getState().isLoading).toBeTrue();
      
      // Verify all service calls are made (these will complete asynchronously)
      expect(TestBed.inject(ProfileService).getCurrentUserProfile).toHaveBeenCalled();
      expect(TestBed.inject(CreditService).getCreditStatus).toHaveBeenCalled();
      expect(TestBed.inject(FileUploadService).getUserImages).toHaveBeenCalled();
      expect(TestBed.inject(ReplicateService).getCredits).toHaveBeenCalled();
    });

    it('should process uploaded images into thumbnails', (done) => {
      service.state$.subscribe(state => {
        if (state.uploadedImageThumbnails.length > 0) {
          expect(state.uploadedImageThumbnails).toHaveLength(1);
          expect(state.uploadedImageThumbnails[0]).toEqual({
            id: 1,
            url: 'url1',
            fileName: 'Image 1'
          });
          done();
        }
      });
      
      service.loadInitialDashboardData();
    });

    it('should set premium workflow flag based on purchased credits', (done) => {
      service.state$.subscribe(state => {
        if (state.userCreditStatus) {
          expect(state.isPremiumWorkflow).toBeTrue(); // purchasedCredits: 50 > 0
          done();
        }
      });
      
      service.loadInitialDashboardData();
    });

    it('should cache loaded data', (done) => {
      service.state$.subscribe(state => {
        if (!state.isLoading && state.creditsInfo) {
          expect(mockCacheManager.setCachedData).toHaveBeenCalledWith(
            'dashboard_data',
            jasmine.any(Object),
            MockCacheManagerService.DASHBOARD_CACHE_DURATION_MS
          );
          done();
        }
      });
      
      service.loadInitialDashboardData();
    });

    it('should handle API errors gracefully', () => {
      spyOn(TestBed.inject(ProfileService), 'getCurrentUserProfile').and.returnValue(
        throwError(() => new Error('API Error'))
      );
      
      service.loadInitialDashboardData();
      
      expect(mockNotificationService.error).toHaveBeenCalledWith(
        'Dashboard Load Failed',
        'Could not load dashboard data. Please try again.'
      );
    });
  });

  describe('resetState()', () => {
    it('should reset to initial state', () => {
      service.setState({ generatedPhotosCount: 10, isLoading: true });
      
      service.resetState();
      
      const state = service.getState();
      expect(state.generatedPhotosCount).toBe(0);
      expect(state.isLoading).toBeFalse();
      expect(mockCacheManager.invalidateCache).toHaveBeenCalledWith('dashboard_data');
    });
  });

  describe('forceRefresh()', () => {
    it('should clear cache and reload data', () => {
      service.forceRefresh();
      
      expect(mockCacheManager.forceRefresh).toHaveBeenCalledWith('dashboard_data');
      expect(mockFallbackOps.resetFallbackTracking).toHaveBeenCalled();
      expect(TestBed.inject(FileUploadService).invalidateUserImagesCache).toHaveBeenCalled();
    });
  });

  describe('invalidateAndRefreshImages()', () => {
    it('should invalidate caches and refresh photos count', () => {
      service.invalidateAndRefreshImages();
      
      expect(TestBed.inject(FileUploadService).invalidateUserImagesCache).toHaveBeenCalled();
    });
  });

  describe('refreshGeneratedPhotosCount()', () => {
    it('should update generated photos count', (done) => {
      service.refreshGeneratedPhotosCount();
      
      service.state$.subscribe(state => {
        if (state.generatedPhotosCount > 0) {
          expect(state.generatedPhotosCount).toBe(5); // From mock getUserImages
          done();
        }
      });
    });

    it('should handle refresh errors', () => {
      spyOn(TestBed.inject(FileUploadService), 'getUserImages').and.returnValue(
        throwError(() => new Error('Refresh failed'))
      );
      spyOn(console, 'error');
      
      service.refreshGeneratedPhotosCount();
      
      expect(console.error).toHaveBeenCalledWith('Failed to refresh generated photos count:', jasmine.any(Error));
    });
  });

  describe('Fallback Operations Integration', () => {
    beforeEach(() => {
      // Set up state to trigger fallback checks
      service.setState({
        userProfile: { id: 1 },
        generatedPhotosCount: 0,
        uploadedImages: 5,
        modelStatus: 'Not Started'
      });
    });

    it('should check for fallback operations during data loading', (done) => {
      mockFallbackOps.checkIfFallbackNeeded.and.returnValue({
        shouldCheckFilesystem: true,
        shouldDiscoverModels: false
      });
      
      // Trigger the async data loading
      service.loadInitialDashboardData();
      
      // Wait for async operations to complete
      setTimeout(() => {
        expect(mockFallbackOps.checkIfFallbackNeeded).toHaveBeenCalled();
        expect(mockFallbackOps.checkGeneratedImagesFromFilesystem).toHaveBeenCalled();
        done();
      }, 100);
    });

    it('should trigger model discovery when needed', (done) => {
      mockFallbackOps.checkIfFallbackNeeded.and.returnValue({
        shouldCheckFilesystem: false,
        shouldDiscoverModels: true
      });
      
      service.loadInitialDashboardData();
      
      setTimeout(() => {
        expect(mockModelState.runAsyncModelDiscovery).toHaveBeenCalled();
        done();
      }, 100);
    });

    it('should update state when filesystem check returns count', (done) => {
      mockFallbackOps.checkIfFallbackNeeded.and.returnValue({
        shouldCheckFilesystem: true,
        shouldDiscoverModels: false
      });
      
      service.loadInitialDashboardData();
      
      setTimeout(() => {
        service.state$.subscribe(state => {
          if (state.generatedPhotosCount === 10) {
            expect(state.generatedPhotosCount).toBe(10);
            done();
          }
        });
      }, 100);
    });
  });

  describe('Model Status Integration', () => {
    it('should use ModelStateService for status determination', (done) => {
      mockModelState.getModelStatusFromData.and.returnValue({
        modelStatus: 'Model Ready',
        hasTrainedModel: true,
        latestTrainedModel: { id: 'model-123' }
      });
      
      service.loadInitialDashboardData();
      
      setTimeout(() => {
        expect(mockModelState.getModelStatusFromData).toHaveBeenCalled();
        
        service.state$.subscribe(state => {
          if (state.modelStatus === 'Model Ready') {
            expect(state.modelStatus).toBe('Model Ready');
            expect(state.latestTrainedModel).toEqual({ id: 'model-123' });
            done();
          }
        });
      }, 100);
    });
  });

  describe('enableGlobalDebug()', () => {
    it('should enable debug on all specialized services', () => {
      service.enableGlobalDebug();
      
      expect(mockModelState.enableGlobalDebug).toHaveBeenCalled();
      expect(mockCacheManager.enableGlobalDebug).toHaveBeenCalled();
      expect(mockFallbackOps.enableGlobalDebug).toHaveBeenCalled();
    });

    it('should add dashboard-specific debug methods', () => {
      service.enableGlobalDebug();
      
      expect((window as any).forceRefresh).toBeDefined();
      expect((window as any).invalidateImages).toBeDefined();
      expect((window as any).dashboardState).toBeDefined();
    });

    it('should make global debug methods work', () => {
      service.enableGlobalDebug();
      
      spyOn(service, 'forceRefresh');
      spyOn(service, 'getState').and.returnValue({} as DashboardState);
      
      (window as any).forceRefresh();
      (window as any).dashboardState();
      
      expect(service.forceRefresh).toHaveBeenCalled();
      expect(service.getState).toHaveBeenCalled();
    });

    it('should log debug instructions', () => {
      spyOn(console, 'log');
      
      service.enableGlobalDebug();
      
      expect(console.log).toHaveBeenCalledWith(jasmine.stringMatching(/Dashboard debug enabled/));
    });
  });

  describe('Error Handling', () => {
    it('should handle profile service errors', () => {
      spyOn(TestBed.inject(ProfileService), 'getCurrentUserProfile').and.returnValue(
        throwError(() => new Error('Profile error'))
      );
      
      service.loadInitialDashboardData();
      
      expect(service.getState().isLoading).toBeFalse();
      expect(mockNotificationService.error).toHaveBeenCalled();
    });

    it('should handle credit service errors', () => {
      spyOn(TestBed.inject(CreditService), 'getCreditStatus').and.returnValue(
        throwError(() => new Error('Credit error'))
      );
      
      service.loadInitialDashboardData();
      
      expect(mockNotificationService.error).toHaveBeenCalled();
    });

    it('should handle secondary data loading errors gracefully', (done) => {
      spyOn(TestBed.inject(FileUploadService), 'getTrainingStatus').and.returnValue(
        throwError(() => new Error('Training status error'))
      );
      spyOn(console, 'error');
      
      service.loadInitialDashboardData();
      
      // Wait for secondary data loading to complete
      setTimeout(() => {
        expect(console.error).toHaveBeenCalledWith('Failed to load additional dashboard data:', jasmine.any(Error));
        done();
      }, 100);
    });

    it('should handle fallback operation errors', (done) => {
      mockFallbackOps.checkGeneratedImagesFromFilesystem.and.returnValue(
        throwError(() => new Error('Fallback error'))
      );
      mockFallbackOps.checkIfFallbackNeeded.and.returnValue({
        shouldCheckFilesystem: true,
        shouldDiscoverModels: false
      });
      spyOn(console, 'error');
      
      service.loadInitialDashboardData();
      
      setTimeout(() => {
        expect(console.error).toHaveBeenCalledWith('Filesystem check failed:', jasmine.any(Error));
        done();
      }, 100);
    });
  });

  describe('Integration with Interface', () => {
    it('should satisfy IDashboardStateService contract', () => {
      const interfaceService: IDashboardStateService = service;
      
      expect(interfaceService.getState).toBeDefined();
      expect(interfaceService.setState).toBeDefined();
      expect(interfaceService.loadInitialDashboardData).toBeDefined();
      expect(interfaceService.resetState).toBeDefined();
      expect(interfaceService.forceRefresh).toBeDefined();
      expect(interfaceService.invalidateAndRefreshImages).toBeDefined();
      expect(interfaceService.refreshGeneratedPhotosCount).toBeDefined();
      expect(interfaceService.enableGlobalDebug).toBeDefined();
    });

    it('should return correct types from interface methods', () => {
      const state = service.getState();
      expect(typeof state).toBe('object');
      expect(state.userProfile !== undefined).toBeTrue();
      expect(typeof state.isLoading).toBe('boolean');
      expect(typeof state.generatedPhotosCount).toBe('number');
      
      expect(() => service.setState({ isLoading: true })).not.toThrow();
      expect(() => service.resetState()).not.toThrow();
      expect(() => service.forceRefresh()).not.toThrow();
    });
  });

  describe('Performance', () => {
    it('should handle rapid state updates efficiently', () => {
      const updates = Array(100).fill(0).map((_, i) => ({ generatedPhotosCount: i }));
      
      const startTime = performance.now();
      updates.forEach(update => service.setState(update));
      const endTime = performance.now();
      
      expect(endTime - startTime).toBeLessThan(100); // Should be very fast
      expect(service.getState().generatedPhotosCount).toBe(99);
    });

    it('should handle multiple concurrent load requests', () => {
      // Make cache return null and debounce return false for all calls
      mockCacheManager.getCachedData.and.returnValue(null);
      mockCacheManager.shouldDebounceRequest.and.returnValue(false);
      
      // Should not throw even with multiple concurrent calls
      expect(() => {
        Array(5).fill(0).forEach(() => service.loadInitialDashboardData());
      }).not.toThrow();
    });

    it('should cache data to improve subsequent loads', () => {
      service.loadInitialDashboardData();
      
      // First load should cache data
      expect(mockCacheManager.setCachedData).toHaveBeenCalled();
      
      // Reset cache spy
      mockCacheManager.setCachedData.calls.reset();
      mockCacheManager.getCachedData.and.returnValue({ creditsInfo: { cached: true } });
      
      service.loadInitialDashboardData();
      
      // Second load should use cache and not call setCachedData
      expect(mockCacheManager.setCachedData).not.toHaveBeenCalled();
    });
  });
});