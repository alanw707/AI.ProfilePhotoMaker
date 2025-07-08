import { TestBed } from '@angular/core/testing';
import { NgZone } from '@angular/core';
import { ModelLoaderService } from './model-loader.service';
import { IModelLoaderService } from '../interfaces/service.interfaces';

// Mock face-api.js
const mockFaceApi = {
  nets: {
    tinyFaceDetector: {
      loadFromUri: jasmine.createSpy('loadFromUri').and.returnValue(Promise.resolve())
    },
    faceLandmark68TinyNet: {
      loadFromUri: jasmine.createSpy('loadFromUri').and.returnValue(Promise.resolve())
    },
    faceRecognitionNet: {
      loadFromUri: jasmine.createSpy('loadFromUri').and.returnValue(Promise.resolve())
    },
    ssdMobilenetv1: {
      loadFromUri: jasmine.createSpy('loadFromUri').and.returnValue(Promise.resolve())
    }
  }
};

// Mock NgZone
class MockNgZone {
  runOutsideAngular<T>(fn: () => T): T {
    return fn();
  }
}

describe('ModelLoaderService', () => {
  let service: ModelLoaderService;
  let ngZone: NgZone;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ModelLoaderService,
        { provide: NgZone, useClass: MockNgZone }
      ]
    });
    
    service = TestBed.inject(ModelLoaderService);
    ngZone = TestBed.inject(NgZone);

    // Mock face-api.js globally
    (globalThis as any)['face-api'] = mockFaceApi;

    // Reset all spies
    Object.values(mockFaceApi.nets).forEach(net => {
      net.loadFromUri.calls.reset();
    });
  });

  afterEach(() => {
    // Clean up global mock
    delete (globalThis as any)['face-api'];
  });

  describe('Service Creation', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should implement IModelLoaderService interface', () => {
      expect(service.loadModels).toBeDefined();
      expect(service.areModelsLoaded).toBeDefined();
      expect(typeof service.loadModels).toBe('function');
      expect(typeof service.areModelsLoaded).toBe('function');
    });

    it('should start with models not loaded', () => {
      expect(service.areModelsLoaded()).toBeFalse();
    });
  });

  describe('areModelsLoaded()', () => {
    it('should return false initially', () => {
      expect(service.areModelsLoaded()).toBeFalse();
    });

    it('should return true after successful model loading', async () => {
      await service.loadModels();
      expect(service.areModelsLoaded()).toBeTrue();
    });
  });

  describe('loadModels()', () => {
    it('should load models successfully from local path', async () => {
      await service.loadModels();

      // Verify all models were loaded
      expect(mockFaceApi.nets.tinyFaceDetector.loadFromUri).toHaveBeenCalled();
      expect(mockFaceApi.nets.faceLandmark68TinyNet.loadFromUri).toHaveBeenCalled();
      expect(mockFaceApi.nets.faceRecognitionNet.loadFromUri).toHaveBeenCalled();
      expect(mockFaceApi.nets.ssdMobilenetv1.loadFromUri).toHaveBeenCalled();
      
      expect(service.areModelsLoaded()).toBeTrue();
    });

    it('should not reload models if already loaded', async () => {
      // Load models first time
      await service.loadModels();
      
      // Reset spy call counts
      Object.values(mockFaceApi.nets).forEach(net => {
        net.loadFromUri.calls.reset();
      });

      // Try to load again
      await service.loadModels();

      // Should not call loadFromUri again
      expect(mockFaceApi.nets.tinyFaceDetector.loadFromUri).not.toHaveBeenCalled();
      expect(mockFaceApi.nets.faceLandmark68TinyNet.loadFromUri).not.toHaveBeenCalled();
      expect(mockFaceApi.nets.faceRecognitionNet.loadFromUri).not.toHaveBeenCalled();
      expect(mockFaceApi.nets.ssdMobilenetv1.loadFromUri).not.toHaveBeenCalled();
    });

    it('should handle concurrent load requests', async () => {
      // Start multiple concurrent loads
      const loadPromise1 = service.loadModels();
      const loadPromise2 = service.loadModels();
      const loadPromise3 = service.loadModels();

      await Promise.all([loadPromise1, loadPromise2, loadPromise3]);

      // Should only load once
      expect(mockFaceApi.nets.tinyFaceDetector.loadFromUri).toHaveBeenCalledTimes(1);
      expect(service.areModelsLoaded()).toBeTrue();
    });

    it('should try CDN fallback when local models fail', async () => {
      // Make local loading fail
      mockFaceApi.nets.tinyFaceDetector.loadFromUri.and.callFake((url: string) => {
        if (url.includes('/assets/models')) {
          return Promise.reject(new Error('Local models not found'));
        }
        return Promise.resolve();
      });

      await service.loadModels();

      // Should be called twice - once for local, once for CDN
      expect(mockFaceApi.nets.tinyFaceDetector.loadFromUri).toHaveBeenCalledTimes(2);
      expect(mockFaceApi.nets.tinyFaceDetector.loadFromUri).toHaveBeenCalledWith('/assets/models');
      expect(mockFaceApi.nets.tinyFaceDetector.loadFromUri).toHaveBeenCalledWith('https://cdn.jsdelivr.net/npm/@vladmandic/face-api/model');
      
      expect(service.areModelsLoaded()).toBeTrue();
    });

    it('should throw error when all model sources fail', async () => {
      // Make all loading fail
      Object.values(mockFaceApi.nets).forEach(net => {
        net.loadFromUri.and.returnValue(Promise.reject(new Error('Model loading failed')));
      });

      await expectAsync(service.loadModels()).toBeRejectedWithError('Failed to initialize face detection');
      expect(service.areModelsLoaded()).toBeFalse();
    });

    it('should log success message when models load', async () => {
      spyOn(console, 'log');
      
      await service.loadModels();
      
      expect(console.log).toHaveBeenCalledWith(jasmine.stringMatching(/Face detection models loaded successfully from/));
    });

    it('should log warning when local models fail', async () => {
      spyOn(console, 'warn');
      
      // Make local loading fail
      mockFaceApi.nets.tinyFaceDetector.loadFromUri.and.callFake((url: string) => {
        if (url.includes('/assets/models')) {
          return Promise.reject(new Error('Local models not found'));
        }
        return Promise.resolve();
      });

      await service.loadModels();
      
      expect(console.warn).toHaveBeenCalledWith(jasmine.stringMatching(/Failed to load models from/), jasmine.any(Error));
    });

    it('should run outside Angular zone during loading', async () => {
      spyOn(ngZone, 'runOutsideAngular').and.callThrough();
      
      await service.loadModels();
      
      expect(ngZone.runOutsideAngular).toHaveBeenCalled();
    });

    it('should handle partial model loading failures', async () => {
      // Make one model fail, others succeed
      mockFaceApi.nets.tinyFaceDetector.loadFromUri.and.returnValue(Promise.reject(new Error('Failed')));
      
      await expectAsync(service.loadModels()).toBeRejectedWithError('Failed to initialize face detection');
      expect(service.areModelsLoaded()).toBeFalse();
    });
  });

  describe('Error Handling', () => {
    it('should handle network errors gracefully', async () => {
      // Simulate network error
      Object.values(mockFaceApi.nets).forEach(net => {
        net.loadFromUri.and.returnValue(Promise.reject(new Error('Network error')));
      });

      await expectAsync(service.loadModels()).toBeRejectedWithError('Failed to initialize face detection');
    });

    it('should handle timeout errors', async () => {
      // Simulate timeout
      Object.values(mockFaceApi.nets).forEach(net => {
        net.loadFromUri.and.returnValue(Promise.reject(new Error('Timeout')));
      });

      await expectAsync(service.loadModels()).toBeRejectedWithError('Failed to initialize face detection');
    });
  });

  describe('Integration with Interface', () => {
    it('should satisfy IModelLoaderService contract', () => {
      const interfaceService: IModelLoaderService = service;
      
      expect(interfaceService.loadModels).toBeDefined();
      expect(interfaceService.areModelsLoaded).toBeDefined();
      expect(typeof interfaceService.loadModels).toBe('function');
      expect(typeof interfaceService.areModelsLoaded).toBe('function');
    });

    it('should return Promise from loadModels', () => {
      const result = service.loadModels();
      expect(result).toBeInstanceOf(Promise);
    });

    it('should return boolean from areModelsLoaded', () => {
      const result = service.areModelsLoaded();
      expect(typeof result).toBe('boolean');
    });
  });

  describe('Performance', () => {
    it('should load models within reasonable time', async () => {
      const startTime = performance.now();
      await service.loadModels();
      const endTime = performance.now();
      
      // Should complete within 5 seconds (generous for testing)
      expect(endTime - startTime).toBeLessThan(5000);
    });

    it('should handle rapid successive calls efficiently', async () => {
      const promises = Array(10).fill(0).map(() => service.loadModels());
      
      const startTime = performance.now();
      await Promise.all(promises);
      const endTime = performance.now();
      
      // Should still complete quickly even with multiple calls
      expect(endTime - startTime).toBeLessThan(5000);
      expect(service.areModelsLoaded()).toBeTrue();
    });
  });
});