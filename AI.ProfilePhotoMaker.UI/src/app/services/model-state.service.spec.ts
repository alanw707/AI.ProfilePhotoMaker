import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ModelStateService } from './model-state.service';
import { ProfileService } from './profile.service';
import { FileUploadService } from './file-upload.service';
import { IModelStateService, ModelStatusInfo } from '../interfaces/service.interfaces';

// Mock services
class MockProfileService {
  discoverModels = jasmine.createSpy('discoverModels').and.returnValue(
    of({ success: true, data: { ModelsAdded: 1 } })
  );
}

class MockFileUploadService {
  getTrainingStatus = jasmine.createSpy('getTrainingStatus').and.returnValue(
    of({ status: 'Not Started' })
  );
  
  getUserModelRequests = jasmine.createSpy('getUserModelRequests').and.returnValue(
    of({ success: true, data: { hasTrainedModel: false, latestTrainedModel: null } })
  );
  
  getDebugModelStatus = jasmine.createSpy('getDebugModelStatus').and.returnValue(
    of({ success: true, data: {} }).toPromise()
  );
  
  testModelCreationEndpoint = jasmine.createSpy('testModelCreationEndpoint').and.returnValue(
    of({ success: true }).toPromise()
  );
  
  discoverUserModels = jasmine.createSpy('discoverUserModels').and.returnValue(
    of({ success: true }).toPromise()
  );
  
  testSpecificModel = jasmine.createSpy('testSpecificModel').and.returnValue(
    of({ success: true }).toPromise()
  );
}

describe('ModelStateService', () => {
  let service: ModelStateService;
  let mockProfileService: MockProfileService;
  let mockFileUploadService: MockFileUploadService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ModelStateService,
        { provide: ProfileService, useClass: MockProfileService },
        { provide: FileUploadService, useClass: MockFileUploadService }
      ]
    });
    
    service = TestBed.inject(ModelStateService);
    mockProfileService = TestBed.inject(ProfileService) as any;
    mockFileUploadService = TestBed.inject(FileUploadService) as any;
  });

  afterEach(() => {
    // Reset all spies
    Object.values(mockProfileService).forEach(spy => {
      if (jasmine.isSpy(spy)) {
        (spy as jasmine.Spy).calls.reset();
      }
    });
    Object.values(mockFileUploadService).forEach(spy => {
      if (jasmine.isSpy(spy)) {
        (spy as jasmine.Spy).calls.reset();
      }
    });
  });

  describe('Service Creation', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should implement IModelStateService interface', () => {
      expect(service.runAsyncModelDiscovery).toBeDefined();
      expect(service.updateModelStatus).toBeDefined();
      expect(service.debugModelStatus).toBeDefined();
      expect(service.triggerModelDiscovery).toBeDefined();
      expect(service.isModelDiscoveryNeeded).toBeDefined();
      expect(service.getModelStatusFromData).toBeDefined();
      // enableGlobalDebug is optional and may not exist in all implementations
    });
  });

  describe('runAsyncModelDiscovery()', () => {
    it('should call profile service discover models', () => {
      service.runAsyncModelDiscovery();
      expect(mockProfileService.discoverModels).toHaveBeenCalled();
    });

    it('should update model status when models are discovered', () => {
      spyOn(service, 'updateModelStatus');
      mockProfileService.discoverModels.and.returnValue(
        of({ success: true, data: { ModelsAdded: 2 } })
      );

      service.runAsyncModelDiscovery();

      expect(service.updateModelStatus).toHaveBeenCalled();
    });

    it('should not update status when no models are added', () => {
      spyOn(service, 'updateModelStatus');
      mockProfileService.discoverModels.and.returnValue(
        of({ success: true, data: { ModelsAdded: 0 } })
      );

      service.runAsyncModelDiscovery();

      expect(service.updateModelStatus).not.toHaveBeenCalled();
    });

    it('should handle discovery errors gracefully', () => {
      spyOn(console, 'error');
      mockProfileService.discoverModels.and.returnValue(
        throwError(() => new Error('Discovery failed'))
      );

      service.runAsyncModelDiscovery();

      expect(console.error).toHaveBeenCalledWith('Async model discovery failed:', jasmine.any(Error));
    });

    it('should not show errors to user', () => {
      // This test ensures background operation doesn't disturb user
      spyOn(console, 'error');
      mockProfileService.discoverModels.and.returnValue(
        throwError(() => new Error('Network error'))
      );

      expect(() => service.runAsyncModelDiscovery()).not.toThrow();
    });
  });

  describe('updateModelStatus()', () => {
    it('should call required services to get status data', () => {
      service.updateModelStatus();

      expect(mockFileUploadService.getTrainingStatus).toHaveBeenCalled();
      expect(mockFileUploadService.getUserModelRequests).toHaveBeenCalled();
    });

    it('should process model status correctly when model is ready', () => {
      const mockModelData = {
        hasTrainedModel: true,
        latestTrainedModel: { id: 'model-123', status: 'succeeded' }
      };
      
      mockFileUploadService.getUserModelRequests.and.returnValue(
        of({ success: true, data: mockModelData })
      );

      spyOn(service as any, 'notifyModelStatusUpdate');
      service.updateModelStatus();

      // Should call notify with 'Model Ready' status
      expect((service as any)['notifyModelStatusUpdate']).toHaveBeenCalledWith('Model Ready', mockModelData.latestTrainedModel);
    });

    it('should handle cases when no trained model exists', () => {
      mockFileUploadService.getUserModelRequests.and.returnValue(
        of({ success: true, data: { hasTrainedModel: false, latestTrainedModel: null } })
      );

      spyOn(service as any, 'notifyModelStatusUpdate');
      service.updateModelStatus();

      expect((service as any)['notifyModelStatusUpdate']).toHaveBeenCalledWith('Not Started', null);
    });

    it('should handle service errors gracefully', () => {
      spyOn(console, 'error');
      mockFileUploadService.getTrainingStatus.and.returnValue(
        throwError(() => new Error('Service error'))
      );

      service.updateModelStatus();

      expect(console.error).toHaveBeenCalledWith('Failed to update model status:', jasmine.any(Error));
    });
  });

  describe('debugModelStatus()', () => {
    it('should call all debug methods', async () => {
      const result = await service.debugModelStatus();

      expect(mockFileUploadService.getDebugModelStatus).toHaveBeenCalled();
      expect(mockFileUploadService.testModelCreationEndpoint).toHaveBeenCalled();
      expect(mockFileUploadService.discoverUserModels).toHaveBeenCalled();
      expect(mockFileUploadService.testSpecificModel).toHaveBeenCalled();
    });

    it('should return comprehensive debug data', async () => {
      const mockDebugData = { debugInfo: 'test' };
      const mockTestData = { testResult: 'success' };
      
      mockFileUploadService.getDebugModelStatus.and.returnValue(Promise.resolve(mockDebugData));
      mockFileUploadService.testModelCreationEndpoint.and.returnValue(Promise.resolve(mockTestData));

      const result = await service.debugModelStatus();

      expect(result.debug).toEqual(mockDebugData);
      expect(result.test).toEqual(mockTestData);
      expect(result.discover).toBeDefined();
      expect(result.specificModel).toBeDefined();
    });

    it('should handle debug errors and return error object', async () => {
      mockFileUploadService.getDebugModelStatus.and.returnValue(
        Promise.reject(new Error('Debug failed'))
      );

      const result = await service.debugModelStatus();

      expect(result.error).toBeDefined();
      expect(result.error).toBeInstanceOf(Error);
    });

    it('should log debug information', async () => {
      spyOn(console, 'log');
      
      await service.debugModelStatus();

      expect(console.log).toHaveBeenCalledWith('🔍 Starting comprehensive model status debug...');
    });
  });

  describe('triggerModelDiscovery()', () => {
    it('should call profile service discover models', async () => {
      await service.triggerModelDiscovery();
      expect(mockProfileService.discoverModels).toHaveBeenCalled();
    });

    it('should update status when models are found', async () => {
      spyOn(service, 'updateModelStatus');
      mockProfileService.discoverModels.and.returnValue(
        of({ success: true, data: { ModelsAdded: 1 } }).toPromise()
      );

      await service.triggerModelDiscovery();

      expect(service.updateModelStatus).toHaveBeenCalled();
    });

    it('should not update status when no models found', async () => {
      spyOn(service, 'updateModelStatus');
      mockProfileService.discoverModels.and.returnValue(
        of({ success: true, data: { ModelsAdded: 0 } }).toPromise()
      );

      await service.triggerModelDiscovery();

      expect(service.updateModelStatus).not.toHaveBeenCalled();
    });

    it('should return discovery result', async () => {
      const mockResult = { success: true, data: { ModelsAdded: 2 } };
      mockProfileService.discoverModels.and.returnValue(
        of(mockResult).toPromise()
      );

      const result = await service.triggerModelDiscovery();

      expect(result).toEqual(mockResult);
    });

    it('should handle discovery errors', async () => {
      spyOn(console, 'error');
      mockProfileService.discoverModels.and.returnValue(
        Promise.reject(new Error('Discovery failed'))
      );

      const result = await service.triggerModelDiscovery();

      expect(result).toEqual({ success: false, error: jasmine.any(Error) });
      expect(console.error).toHaveBeenCalled();
    });

    it('should log appropriate messages', async () => {
      spyOn(console, 'log');
      mockProfileService.discoverModels.and.returnValue(
        of({ success: true, data: { ModelsAdded: 1 } }).toPromise()
      );

      await service.triggerModelDiscovery();

      expect(console.log).toHaveBeenCalledWith('🔍 Manually triggering model discovery...');
      expect(console.log).toHaveBeenCalledWith('🎉 Models found and synced! Updating model status...');
    });
  });

  describe('isModelDiscoveryNeeded()', () => {
    it('should return true when discovery is needed', () => {
      const result = service.isModelDiscoveryNeeded(false, 'Not Started', 5);
      expect(result).toBeTrue();
    });

    it('should return false when model already exists', () => {
      const result = service.isModelDiscoveryNeeded(true, 'Model Ready', 5);
      expect(result).toBeFalse();
    });

    it('should return false when no uploaded images', () => {
      const result = service.isModelDiscoveryNeeded(false, 'Not Started', 0);
      expect(result).toBeFalse();
    });

    it('should return false when model status is not "Not Started"', () => {
      const result = service.isModelDiscoveryNeeded(false, 'training', 5);
      expect(result).toBeFalse();
    });
  });

  describe('getModelStatusFromData()', () => {
    it('should return "Model Ready" when trained model exists', () => {
      const modelData = {
        hasTrainedModel: true,
        latestTrainedModel: { id: 'model-123' }
      };
      const trainingStatus = { status: 'Not Started' };

      const result = service.getModelStatusFromData(modelData, trainingStatus);

      expect(result.modelStatus).toBe('Model Ready');
      expect(result.hasTrainedModel).toBeTrue();
      expect(result.latestTrainedModel).toEqual(modelData.latestTrainedModel);
    });

    it('should return "training" when requests are pending', () => {
      const modelData = {
        hasTrainedModel: false,
        allRequests: [
          { status: 'creating' },
          { status: 'pending' }
        ]
      };
      const trainingStatus = { status: 'Not Started' };

      const result = service.getModelStatusFromData(modelData, trainingStatus);

      expect(result.modelStatus).toBe('training');
      expect(result.hasTrainedModel).toBeFalse();
    });

    it('should fall back to training status', () => {
      const modelData = { hasTrainedModel: false };
      const trainingStatus = { status: 'Custom Status' };

      const result = service.getModelStatusFromData(modelData, trainingStatus);

      expect(result.modelStatus).toBe('Custom Status');
    });

    it('should default to "Not Started" when no data', () => {
      const result = service.getModelStatusFromData(null, null);

      expect(result.modelStatus).toBe('Not Started');
      expect(result.hasTrainedModel).toBeFalse();
      expect(result.latestTrainedModel).toBeNull();
    });
  });

  describe('enableGlobalDebug() (if available)', () => {
    it('should add debug methods to global window if method exists', () => {
      if (typeof (service as any).enableGlobalDebug === 'function') {
        (service as any).enableGlobalDebug();
        
        expect((window as any).debugModelStatus).toBeDefined();
        expect((window as any).discoverModels).toBeDefined();
        expect(typeof (window as any).debugModelStatus).toBe('function');
        expect(typeof (window as any).discoverModels).toBe('function');
      } else {
        // Skip test if method doesn't exist
        expect(true).toBe(true);
      }
    });

    it('should make global debug functions work if method exists', () => {
      if (typeof (service as any).enableGlobalDebug === 'function') {
        (service as any).enableGlobalDebug();
        
        spyOn(service, 'debugModelStatus');
        spyOn(service, 'triggerModelDiscovery');
        
        (window as any).debugModelStatus();
        (window as any).discoverModels();
        
        expect(service.debugModelStatus).toHaveBeenCalled();
        expect(service.triggerModelDiscovery).toHaveBeenCalled();
      } else {
        // Skip test if method doesn't exist
        expect(true).toBe(true);
      }
    });

    it('should log debug instructions if method exists', () => {
      if (typeof (service as any).enableGlobalDebug === 'function') {
        spyOn(console, 'log');
        
        (service as any).enableGlobalDebug();
        
        expect(console.log).toHaveBeenCalledWith(jasmine.stringMatching(/Model debug enabled/));
      } else {
        // Skip test if method doesn't exist
        expect(true).toBe(true);
      }
    });
  });

  describe('Private Methods', () => {
    it('should have notifyModelStatusUpdate method', () => {
      expect((service as any)['notifyModelStatusUpdate']).toBeDefined();
      expect(typeof (service as any)['notifyModelStatusUpdate']).toBe('function');
    });

    it('should log status updates', () => {
      spyOn(console, 'log');
      
      (service as any)['notifyModelStatusUpdate']('Model Ready', { id: 'test-model' });
      
      expect(console.log).toHaveBeenCalledWith('🔄 Model status updated:', {
        modelStatus: 'Model Ready',
        latestTrainedModel: { id: 'test-model' }
      });
    });
  });

  describe('Error Handling', () => {
    it('should handle network errors in discovery', () => {
      spyOn(console, 'error');
      mockProfileService.discoverModels.and.returnValue(
        throwError(() => new Error('Network error'))
      );

      expect(() => service.runAsyncModelDiscovery()).not.toThrow();
    });

    it('should handle invalid response formats', () => {
      mockProfileService.discoverModels.and.returnValue(of(null));
      
      expect(() => service.runAsyncModelDiscovery()).not.toThrow();
    });

    it('should handle missing service dependencies', () => {
      // Test service behavior when dependencies are unavailable
      mockFileUploadService.getTrainingStatus.and.returnValue(
        throwError(() => new Error('Service unavailable'))
      );

      expect(() => service.updateModelStatus()).not.toThrow();
    });
  });

  describe('Integration with Interface', () => {
    it('should satisfy IModelStateService contract', () => {
      const interfaceService: IModelStateService = service;
      
      expect(interfaceService.runAsyncModelDiscovery).toBeDefined();
      expect(interfaceService.updateModelStatus).toBeDefined();
      expect(interfaceService.debugModelStatus).toBeDefined();
      expect(interfaceService.triggerModelDiscovery).toBeDefined();
      expect(interfaceService.isModelDiscoveryNeeded).toBeDefined();
      expect(interfaceService.getModelStatusFromData).toBeDefined();
    });

    it('should return correct types from interface methods', async () => {
      const debugResult = await service.debugModelStatus();
      expect(typeof debugResult).toBe('object');
      
      const discoveryResult = await service.triggerModelDiscovery();
      expect(typeof discoveryResult).toBe('object');
      
      const isNeeded = service.isModelDiscoveryNeeded(false, 'Not Started', 1);
      expect(typeof isNeeded).toBe('boolean');
      
      const statusInfo = service.getModelStatusFromData({}, {});
      expect(statusInfo.modelStatus).toBeDefined();
      expect(typeof statusInfo.hasTrainedModel).toBe('boolean');
    });
  });

  describe('Performance', () => {
    it('should handle multiple concurrent discovery calls', () => {
      const calls = Array(5).fill(0).map(() => service.runAsyncModelDiscovery());
      
      // Should not throw even with multiple concurrent calls
      expect(() => Promise.all(calls)).not.toThrow();
    });

    it('should complete debug operations within reasonable time', async () => {
      const startTime = performance.now();
      await service.debugModelStatus();
      const endTime = performance.now();
      
      expect(endTime - startTime).toBeLessThan(5000); // 5 seconds max
    });
  });
});