import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { FallbackOperationsService } from './fallback-operations.service';
import { AuthService } from './auth.service';
import { ConfigService } from './config.service';
import { FileUploadService } from './file-upload.service';
import { LoggingService } from './logging.service';
import { ModelStatusService } from './model-status.service';
import {
  WorkspaceStateForFallback,
  IFallbackOperationsService,
} from '../interfaces/service.interfaces';

// Mock services
class MockAuthService {
  getToken = jasmine.createSpy('getToken').and.returnValue('mock-token');
}

class MockConfigService {
  getFullUrl = jasmine
    .createSpy('getFullUrl')
    .and.returnValue('http://mock-api/test/fix-generated-images');
}

class MockFileUploadService {
  invalidateUserImagesCache = jasmine.createSpy('invalidateUserImagesCache');

  getUserImages = jasmine.createSpy('getUserImages').and.returnValue(
    of({
      success: true,
      data: {
        generatedImages: 5,
        images: [{ isGenerated: true }, { isGenerated: true }, { isGenerated: false }],
        totalImages: 3,
      },
    })
  );
}

class MockLoggingService {
  info = jasmine.createSpy('info');
  debug = jasmine.createSpy('debug');
  warn = jasmine.createSpy('warn');
  error = jasmine.createSpy('error');
}

class MockModelStatusService {
  canGenerate = jasmine.createSpy('canGenerate').and.returnValue(false);
}

describe('FallbackOperationsService', () => {
  let service: FallbackOperationsService;
  let httpMock: HttpTestingController;
  let mockAuthService: MockAuthService;
  let mockConfigService: MockConfigService;
  let mockFileUploadService: MockFileUploadService;
  let mockLoggingService: MockLoggingService;
  let mockModelStatusService: MockModelStatusService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        FallbackOperationsService,
        { provide: AuthService, useClass: MockAuthService },
        { provide: ConfigService, useClass: MockConfigService },
        { provide: FileUploadService, useClass: MockFileUploadService },
        { provide: LoggingService, useClass: MockLoggingService },
        { provide: ModelStatusService, useClass: MockModelStatusService },
      ],
    });

    service = TestBed.inject(FallbackOperationsService);
    httpMock = TestBed.inject(HttpTestingController);
    mockAuthService = TestBed.inject(AuthService) as any;
    mockConfigService = TestBed.inject(ConfigService) as any;
    mockFileUploadService = TestBed.inject(FileUploadService) as any;
    mockLoggingService = TestBed.inject(LoggingService) as any;
    mockModelStatusService = TestBed.inject(ModelStatusService) as any;
  });

  afterEach(() => {
    httpMock.verify();
    // Reset fallback tracker
    service.resetFallbackTracking();
  });

  describe('Service Creation', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should implement IFallbackOperationsService interface', () => {
      expect(service.checkGeneratedImagesFromFilesystem).toBeDefined();
      expect(service.isFilesystemCheckNeeded).toBeDefined();
      expect(service.isModelDiscoveryNeeded).toBeDefined();
      expect(service.checkIfFallbackNeeded).toBeDefined();
      expect(service.debugDataDiscrepancy).toBeDefined();
      expect(service.resetFallbackTracking).toBeDefined();
      expect(service.getFallbackTracker).toBeDefined();
      expect(service.markFilesystemCheckCompleted).toBeDefined();
      expect(service.markModelDiscoveryCompleted).toBeDefined();
    });

    it('should initialize with default fallback tracker', () => {
      const tracker = service.getFallbackTracker();
      expect(tracker.filesystemCheck).toBeFalse();
      expect(tracker.modelDiscovery).toBeFalse();
      expect(tracker.lastReset).toBe(0);
    });
  });

  describe('checkGeneratedImagesFromFilesystem()', () => {
    it('should make HTTP request to fix endpoint', () => {
      const mockResponse = { success: true, data: { addedCount: 3 } };

      service.checkGeneratedImagesFromFilesystem().subscribe();

      const req = httpMock.expectOne('http://mock-api/test/fix-generated-images');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('Authorization')).toBe('Bearer mock-token');
      expect(req.request.headers.get('Content-Type')).toBe('application/json');

      req.flush(mockResponse);
    });

    it('should invalidate cache and refresh data when images are added', () => {
      const mockResponse = { success: true, data: { addedCount: 5 } };

      service.checkGeneratedImagesFromFilesystem().subscribe();

      const req = httpMock.expectOne('http://mock-api/test/fix-generated-images');
      req.flush(mockResponse);

      expect(mockFileUploadService.invalidateUserImagesCache).toHaveBeenCalled();
      expect(mockFileUploadService.getUserImages).toHaveBeenCalledWith(true);
    });

    it('should return actual generated count after refresh', done => {
      const mockResponse = { success: true, data: { addedCount: 3 } };

      service.checkGeneratedImagesFromFilesystem().subscribe(result => {
        expect(result.success).toBeTrue();
        expect(result.addedCount).toBe(3);
        expect(result.actualGeneratedCount).toBe(5); // From getUserImages mock
        done();
      });

      const req = httpMock.expectOne('http://mock-api/test/fix-generated-images');
      req.flush(mockResponse);
    });

    it('should handle case when no images need fixing', done => {
      const mockResponse = { success: true, data: { addedCount: 0 } };

      service.checkGeneratedImagesFromFilesystem().subscribe(result => {
        expect(result.success).toBeTrue();
        expect(result.addedCount).toBe(0);
        done();
      });

      const req = httpMock.expectOne('http://mock-api/test/fix-generated-images');
      req.flush(mockResponse);
    });

    it('should throw error when no auth token available', () => {
      mockAuthService.getToken.and.returnValue(null);

      expect(() => {
        service.checkGeneratedImagesFromFilesystem().subscribe();
      }).toThrowError('No authentication token available for fix endpoint');
    });

    it('should handle HTTP errors', done => {
      service.checkGeneratedImagesFromFilesystem().subscribe({
        next: () => fail('Should have errored'),
        error: error => {
          expect(error).toBeDefined();
          done();
        },
      });

      const req = httpMock.expectOne('http://mock-api/test/fix-generated-images');
      req.error(new ErrorEvent('Network error'));
    });

    it('should log appropriate messages', () => {
      const mockResponse = { success: true, data: { addedCount: 2 } };

      service.checkGeneratedImagesFromFilesystem().subscribe();

      expect(mockLoggingService.info).toHaveBeenCalledWith(
        '📁 Checking filesystem for missing generated images...'
      );

      const req = httpMock.expectOne('http://mock-api/test/fix-generated-images');
      req.flush(mockResponse);
    });
  });

  describe('isFilesystemCheckNeeded()', () => {
    it('should return true when conditions are met', () => {
      const result = service.isFilesystemCheckNeeded(0, true, { id: 1 });
      expect(result).toBeTrue();
    });

    it('should return false when generatedPhotosCount > 0', () => {
      const result = service.isFilesystemCheckNeeded(5, true, { id: 1 });
      expect(result).toBeFalse();
    });

    it('should return false when no trained model', () => {
      const result = service.isFilesystemCheckNeeded(0, false, { id: 1 });
      expect(result).toBeFalse();
    });

    it('should return false when no user profile', () => {
      const result = service.isFilesystemCheckNeeded(0, true, null);
      expect(result).toBeFalse();
    });

    it('should return false when filesystem check already completed', () => {
      service.markFilesystemCheckCompleted();
      const result = service.isFilesystemCheckNeeded(0, true, { id: 1 });
      expect(result).toBeFalse();
    });
  });

  describe('isModelDiscoveryNeeded()', () => {
    it('should return true when conditions are met', () => {
      const result = service.isModelDiscoveryNeeded('Not Started', 5, false);
      expect(result).toBeTrue();
    });

    it('should return false when model status is not "Not Started"', () => {
      const result = service.isModelDiscoveryNeeded('Model Ready', 5, false);
      expect(result).toBeFalse();
    });

    it('should return false when no uploaded images', () => {
      const result = service.isModelDiscoveryNeeded('Not Started', 0, false);
      expect(result).toBeFalse();
    });

    it('should return false when trained model exists', () => {
      const result = service.isModelDiscoveryNeeded('Not Started', 5, true);
      expect(result).toBeFalse();
    });

    it('should return false when model discovery already completed', () => {
      service.markModelDiscoveryCompleted();
      const result = service.isModelDiscoveryNeeded('Not Started', 5, false);
      expect(result).toBeFalse();
    });
  });

  describe('checkIfFallbackNeeded()', () => {
    let mockState: WorkspaceStateForFallback;

    beforeEach(() => {
      mockState = {
        generatedPhotosCount: 0,
        modelStatus: 'Not Started',
        hasLatestTrainedModel: false,
        uploadedImages: 5,
        hasUserProfile: true,
        latestTrainedModel: null,
      };
    });

    it('should identify when filesystem check is needed', () => {
      mockState.generatedPhotosCount = 0;
      mockState.latestTrainedModel = { id: 'model-123' };

      const result = service.checkIfFallbackNeeded(mockState);

      expect(result.shouldCheckFilesystem).toBeTrue();
      expect(result.shouldDiscoverModels).toBeFalse();
    });

    it('should identify when model discovery is needed', () => {
      mockState.modelStatus = 'Not Started';
      mockState.uploadedImages = 3;
      mockState.latestTrainedModel = null;

      const result = service.checkIfFallbackNeeded(mockState);

      expect(result.shouldCheckFilesystem).toBeFalse();
      expect(result.shouldDiscoverModels).toBeTrue();
    });

    it('should identify when both operations are needed', () => {
      mockState.generatedPhotosCount = 0;
      mockState.modelStatus = 'Not Started';
      mockState.latestTrainedModel = null;
      mockState.hasLatestTrainedModel = false;
      mockState.uploadedImages = 3;
      mockModelStatusService.canGenerate.and.returnValue(true);

      const result = service.checkIfFallbackNeeded(mockState);

      expect(result.shouldCheckFilesystem).toBeTrue();
      expect(result.shouldDiscoverModels).toBeTrue();
    });

    it('should identify when no operations are needed', () => {
      mockState.generatedPhotosCount = 5;
      mockState.modelStatus = 'Model Ready';
      mockState.latestTrainedModel = { id: 'model-123' };

      const result = service.checkIfFallbackNeeded(mockState);

      expect(result.shouldCheckFilesystem).toBeFalse();
      expect(result.shouldDiscoverModels).toBeFalse();
    });

    it('should mark operations as completed when identified', () => {
      mockState.generatedPhotosCount = 0;
      mockState.latestTrainedModel = { id: 'model-123' };

      service.checkIfFallbackNeeded(mockState);

      const tracker = service.getFallbackTracker();
      expect(tracker.filesystemCheck).toBeTrue();
    });

    it('should log appropriate messages', () => {
      service.checkIfFallbackNeeded(mockState);

      expect(mockLoggingService.debug).toHaveBeenCalledWith(
        '🔍 Checking if fallback needed with state:',
        mockState
      );
    });
  });

  describe('debugDataDiscrepancy()', () => {
    it('should call getUserImages with fresh data flag', async () => {
      await service.debugDataDiscrepancy();
      expect(mockFileUploadService.getUserImages).toHaveBeenCalledWith(true);
    });

    it('should return comprehensive discrepancy data', async () => {
      const result = await service.debugDataDiscrepancy();

      expect(result.workspaceCount).toBe(0); // Default value
      expect(result.apiGeneratedField).toBe(5);
      expect(result.filteredCount).toBe(2); // Two isGenerated: true items
      expect(result.totalImages).toBe(3);
      expect(result.allImages.length).toBe(3);
    });

    it('should handle getUserImages errors', async () => {
      mockFileUploadService.getUserImages.and.returnValue(throwError(() => new Error('API error')));

      await expectAsync(service.debugDataDiscrepancy()).toBeRejectedWithError('API error');
    });

    it('should log debug information', async () => {
      await service.debugDataDiscrepancy();

      expect(mockLoggingService.debug).toHaveBeenCalledWith('🔍 Debugging data discrepancy...');
      expect(mockLoggingService.debug).toHaveBeenCalledWith(
        '📊 Data Discrepancy Analysis:',
        jasmine.any(Object)
      );
    });
  });

  describe('resetFallbackTracking()', () => {
    it('should reset tracker when interval has passed', () => {
      // Set an old timestamp
      const tracker = service.getFallbackTracker();
      (tracker as any).lastReset = Date.now() - 400000; // 6+ minutes ago

      service.resetFallbackTracking();

      const newTracker = service.getFallbackTracker();
      expect(newTracker.filesystemCheck).toBeFalse();
      expect(newTracker.modelDiscovery).toBeFalse();
      expect(newTracker.lastReset).toBeGreaterThan(tracker.lastReset);
    });

    it('should not reset tracker when interval has not passed', () => {
      service.markFilesystemCheckCompleted();
      const trackerBefore = service.getFallbackTracker();
      (service as any).fallbackOperationsRun.lastReset = Date.now();

      service.resetFallbackTracking();

      const trackerAfter = service.getFallbackTracker();
      expect(trackerAfter.filesystemCheck).toBe(trackerBefore.filesystemCheck);
    });

    it('should log when reset occurs', () => {
      const tracker = service.getFallbackTracker();
      (tracker as any).lastReset = Date.now() - 400000;

      service.resetFallbackTracking();

      expect(mockLoggingService.info).toHaveBeenCalledWith('🔄 Reset fallback operation tracking');
    });
  });

  describe('getFallbackTracker()', () => {
    it('should return copy of tracker state', () => {
      const tracker1 = service.getFallbackTracker();
      const tracker2 = service.getFallbackTracker();

      expect(tracker1).toEqual(tracker2);
      expect(tracker1).not.toBe(tracker2); // Should be different objects
    });
  });

  describe('markFilesystemCheckCompleted() and markModelDiscoveryCompleted()', () => {
    it('should mark filesystem check as completed', () => {
      service.markFilesystemCheckCompleted();
      const tracker = service.getFallbackTracker();
      expect(tracker.filesystemCheck).toBeTrue();
    });

    it('should mark model discovery as completed', () => {
      service.markModelDiscoveryCompleted();
      const tracker = service.getFallbackTracker();
      expect(tracker.modelDiscovery).toBeTrue();
    });
  });

  // Debug helpers are not part of current service API; skipping related tests

  describe('Error Handling', () => {
    it('should handle missing auth token gracefully', () => {
      mockAuthService.getToken.and.returnValue('');

      expect(() => {
        service.checkGeneratedImagesFromFilesystem().subscribe();
      }).toThrowError();
    });

    it('should handle config service errors', () => {
      mockConfigService.getFullUrl.and.throwError('Config error');

      expect(() => {
        service.checkGeneratedImagesFromFilesystem().subscribe();
      }).toThrowError();
    });

    it('should handle HTTP request failures', done => {
      service.checkGeneratedImagesFromFilesystem().subscribe({
        next: () => fail('Should have errored'),
        error: error => {
          expect(error).toBeDefined();
          done();
        },
      });

      const req = httpMock.expectOne('http://mock-api/test/fix-generated-images');
      req.error(new ErrorEvent('HTTP Error'));
    });

    it('should handle getUserImages failures during refresh', done => {
      mockFileUploadService.getUserImages.and.returnValue(
        throwError(() => new Error('Refresh failed'))
      );

      service.checkGeneratedImagesFromFilesystem().subscribe({
        next: () => fail('Should have errored'),
        error: error => {
          expect(error.message).toBe('Refresh failed');
          done();
        },
      });

      const req = httpMock.expectOne('http://mock-api/test/fix-generated-images');
      req.flush({ success: true, data: { addedCount: 1 } });
    });
  });

  describe('Integration with Interface', () => {
    it('should satisfy IFallbackOperationsService contract', () => {
      const interfaceService: IFallbackOperationsService = service;

      expect(interfaceService.checkGeneratedImagesFromFilesystem).toBeDefined();
      expect(interfaceService.isFilesystemCheckNeeded).toBeDefined();
      expect(interfaceService.isModelDiscoveryNeeded).toBeDefined();
      expect(interfaceService.checkIfFallbackNeeded).toBeDefined();
      expect(interfaceService.debugDataDiscrepancy).toBeDefined();
      expect(interfaceService.resetFallbackTracking).toBeDefined();
      expect(interfaceService.getFallbackTracker).toBeDefined();
      expect(interfaceService.markFilesystemCheckCompleted).toBeDefined();
      expect(interfaceService.markModelDiscoveryCompleted).toBeDefined();
      // Debug API isn't part of the interface in current design
    });

    it('should return correct types from interface methods', async () => {
      const tracker = service.getFallbackTracker();
      expect(tracker.filesystemCheck).toBeDefined();
      expect(tracker.modelDiscovery).toBeDefined();
      expect(tracker.lastReset).toBeDefined();

      const isNeeded = service.isFilesystemCheckNeeded(0, true, {});
      expect(typeof isNeeded).toBe('boolean');

      const discrepancy = await service.debugDataDiscrepancy();
      expect(discrepancy.workspaceCount).toBeDefined();
      expect(discrepancy.apiGeneratedField).toBeDefined();
    });
  });

  describe('Performance', () => {
    it('should handle multiple filesystem checks efficiently', () => {
      service.markFilesystemCheckCompleted(); // Prevent actual operations

      const calls = Array(10)
        .fill(0)
        .map(() => {
          const state: WorkspaceStateForFallback = {
            generatedPhotosCount: 0,
            modelStatus: 'Model Ready',
            hasLatestTrainedModel: true,
            uploadedImages: 5,
            hasUserProfile: true,
            latestTrainedModel: { id: 'test' },
          };
          return service.checkIfFallbackNeeded(state);
        });

      // All calls should complete quickly
      expect(calls.length).toBe(10);
      expect(calls.every(result => typeof result === 'object')).toBeTrue();
    });

    it('should complete debug operations within reasonable time', async () => {
      const startTime = performance.now();
      await service.debugDataDiscrepancy();
      const endTime = performance.now();

      expect(endTime - startTime).toBeLessThan(1000); // 1 second max
    });
  });
});
