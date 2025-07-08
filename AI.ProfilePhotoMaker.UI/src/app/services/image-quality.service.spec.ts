import { TestBed } from '@angular/core/testing';
import { NgZone } from '@angular/core';
import { ImageQualityService } from './image-quality.service';
import { QualityScore, IImageQualityService } from '../interfaces/service.interfaces';

// Mock face-api.js detection result
const mockDetection = {
  detection: {
    score: 0.8,
    box: { x: 50, y: 60, width: 100, height: 120 }
  }
};

// Mock NgZone
class MockNgZone {
  runOutsideAngular<T>(fn: () => T): T {
    return fn();
  }
  
  run<T>(fn: () => T): T {
    return fn();
  }
}

// Helper function to create mock HTMLImageElement
function createMockImage(width: number = 200, height: number = 200): HTMLImageElement {
  const img = {
    width,
    height,
    onload: null as any,
    onerror: null as any,
    src: '',
    addEventListener: jasmine.createSpy('addEventListener'),
    removeEventListener: jasmine.createSpy('removeEventListener')
  } as any;
  return img;
}

// Helper function to create mock File
function createMockFile(size: number = 1024 * 1024, name: string = 'test.jpg'): File {
  const file = new File([''], name, { type: 'image/jpeg' });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

describe('ImageQualityService', () => {
  let service: ImageQualityService;
  let ngZone: NgZone;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ImageQualityService,
        { provide: NgZone, useClass: MockNgZone }
      ]
    });
    
    service = TestBed.inject(ImageQualityService);
    ngZone = TestBed.inject(NgZone);

    // Mock URL.createObjectURL
    spyOn(URL, 'createObjectURL').and.returnValue('blob:mock-url');
  });

  describe('Service Creation', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should implement IImageQualityService interface', () => {
      expect(service.calculateQualityScore).toBeDefined();
      expect(service.getDefaultQualityScore).toBeDefined();
      expect(service.loadImageElement).toBeDefined();
      expect(typeof service.calculateQualityScore).toBe('function');
      expect(typeof service.getDefaultQualityScore).toBe('function');
      expect(typeof service.loadImageElement).toBe('function');
    });
  });

  describe('getDefaultQualityScore()', () => {
    it('should return default quality score structure', () => {
      const defaultScore = service.getDefaultQualityScore();
      
      expect(defaultScore.overall).toBe(1);
      expect(defaultScore.breakdown.faceQuality).toBe(0);
      expect(defaultScore.breakdown.technical).toBe(0);
      expect(defaultScore.breakdown.composition).toBe(0);
      expect(defaultScore.breakdown.fluxCompatibility).toBe(0);
      expect(defaultScore.breakdown.lighting).toBe(0);
      expect(defaultScore.suggestions).toEqual(['Unable to analyze image quality. Please try a different photo.']);
    });

    it('should return valid QualityScore interface', () => {
      const score = service.getDefaultQualityScore();
      
      expect(score.overall).toBeDefined();
      expect(score.breakdown).toBeDefined();
      expect(score.suggestions).toBeDefined();
      expect(Array.isArray(score.suggestions)).toBeTrue();
    });
  });

  describe('loadImageElement()', () => {
    it('should create HTMLImageElement and resolve when loaded', async () => {
      const file = createMockFile();
      
      // Mock Image constructor
      const mockImg = createMockImage();
      spyOn(window, 'Image').and.returnValue(mockImg as any);
      
      const loadPromise = service.loadImageElement(file);
      
      // Simulate successful image load
      setTimeout(() => {
        if (mockImg.onload) {
          mockImg.onload({} as any);
        }
      }, 0);
      
      const result = await loadPromise;
      expect(result).toBe(mockImg);
      expect(URL.createObjectURL).toHaveBeenCalledWith(file);
    });

    it('should reject when image fails to load', async () => {
      const file = createMockFile();
      const mockImg = createMockImage();
      spyOn(window, 'Image').and.returnValue(mockImg as any);
      
      const loadPromise = service.loadImageElement(file);
      
      // Simulate image load error
      setTimeout(() => {
        if (mockImg.onerror) {
          mockImg.onerror({} as any);
        }
      }, 0);
      
      await expectAsync(loadPromise).toBeRejectedWithError('Failed to load image');
    });

    it('should run image loading outside Angular zone', async () => {
      spyOn(ngZone, 'runOutsideAngular').and.callThrough();
      spyOn(ngZone, 'run').and.callThrough();
      
      const file = createMockFile();
      const mockImg = createMockImage();
      spyOn(window, 'Image').and.returnValue(mockImg as any);
      
      const loadPromise = service.loadImageElement(file);
      
      setTimeout(() => {
        if (mockImg.onload) {
          mockImg.onload({} as any);
        }
      }, 0);
      
      await loadPromise;
      
      expect(ngZone.runOutsideAngular).toHaveBeenCalled();
      expect(ngZone.run).toHaveBeenCalled();
    });
  });

  describe('calculateQualityScore()', () => {
    let mockImg: HTMLImageElement;
    let mockFile: File;

    beforeEach(() => {
      mockImg = createMockImage(1024, 1024);
      mockFile = createMockFile(2 * 1024 * 1024); // 2MB file
      
      // Mock canvas and context
      const mockCanvas = {
        width: 0,
        height: 0,
        getContext: jasmine.createSpy('getContext').and.returnValue({
          drawImage: jasmine.createSpy('drawImage'),
          getImageData: jasmine.createSpy('getImageData').and.returnValue({
            data: new Uint8ClampedArray(1024 * 1024 * 4).fill(128) // Gray image
          })
        })
      };
      
      spyOn(document, 'createElement').and.returnValue(mockCanvas as any);
    });

    it('should calculate quality score for single face detection', async () => {
      const detections = [mockDetection];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.overall).toBeGreaterThan(0);
      expect(score.overall).toBeLessThanOrEqual(100);
      expect(score.breakdown.faceQuality).toBeGreaterThan(0);
      expect(score.breakdown.technical).toBeGreaterThan(0);
      expect(score.breakdown.composition).toBeGreaterThan(0);
      expect(score.breakdown.fluxCompatibility).toBeGreaterThan(0);
      expect(score.breakdown.lighting).toBeGreaterThan(0);
    });

    it('should handle no face detections', async () => {
      const detections: any[] = [];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.breakdown.faceQuality).toBe(0);
      expect(score.suggestions).toContain('Upload photo with exactly one clearly visible face.');
    });

    it('should handle multiple face detections', async () => {
      const detections = [mockDetection, mockDetection];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.breakdown.faceQuality).toBe(0);
      expect(score.suggestions).toContain('Upload photo with exactly one clearly visible face.');
    });

    it('should assess high resolution images positively', async () => {
      mockImg = createMockImage(2048, 2048); // High resolution
      const detections = [mockDetection];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.breakdown.technical).toBeGreaterThan(10); // Should get full resolution points
    });

    it('should assess low resolution images negatively', async () => {
      mockImg = createMockImage(256, 256); // Low resolution
      const detections = [mockDetection];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.suggestions).toContain('Image resolution is too low. Upload higher quality image.');
    });

    it('should assess good file size positively', async () => {
      mockFile = createMockFile(3 * 1024 * 1024); // 3MB - good size
      const detections = [mockDetection];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.breakdown.technical).toBeGreaterThan(5);
    });

    it('should assess small file size negatively', async () => {
      mockFile = createMockFile(100 * 1024); // 100KB - small size
      const detections = [mockDetection];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.suggestions).toContain('File size is small, which may indicate low quality.');
    });

    it('should assess face positioning in composition', async () => {
      // Mock well-centered face
      const centeredDetection = {
        detection: {
          score: 0.9,
          box: { x: 462, y: 312, width: 100, height: 100 } // Centered in 1024x1024
        }
      };
      
      const score = await service.calculateQualityScore(mockImg, [centeredDetection], mockFile);
      
      expect(score.breakdown.composition).toBeGreaterThan(5); // Should get positioning points
    });

    it('should suggest improvements for off-center faces', async () => {
      // Mock off-center face
      const offCenterDetection = {
        detection: {
          score: 0.9,
          box: { x: 50, y: 50, width: 100, height: 100 } // Off-center
        }
      };
      
      const score = await service.calculateQualityScore(mockImg, [offCenterDetection], mockFile);
      
      expect(score.suggestions).toContain('Center your face in the photo for better composition.');
    });

    it('should assess appropriate face size', async () => {
      // Mock appropriately sized face (15% of image area)
      const goodSizeDetection = {
        detection: {
          score: 0.9,
          box: { x: 300, y: 300, width: 200, height: 200 } // ~15% area
        }
      };
      
      const score = await service.calculateQualityScore(mockImg, [goodSizeDetection], mockFile);
      
      expect(score.breakdown.composition).toBeGreaterThan(15); // Should get face size points
    });

    it('should handle canvas errors gracefully', async () => {
      // Mock canvas getContext to return null
      spyOn(document, 'createElement').and.returnValue({
        getContext: () => null
      } as any);
      
      const score = await service.calculateQualityScore(mockImg, [mockDetection], mockFile);
      
      // Should still return a valid score even with canvas errors
      expect(score.overall).toBeGreaterThan(0);
    });

    it('should filter out empty suggestions', async () => {
      const score = await service.calculateQualityScore(mockImg, [mockDetection], mockFile);
      
      expect(score.suggestions.every(s => s.length > 0)).toBeTrue();
    });

    it('should cap overall score between 1 and 100', async () => {
      const score = await service.calculateQualityScore(mockImg, [mockDetection], mockFile);
      
      expect(score.overall).toBeGreaterThanOrEqual(1);
      expect(score.overall).toBeLessThanOrEqual(100);
    });
  });

  describe('Technical Quality Assessment', () => {
    it('should give high scores for ideal images', async () => {
      const mockImg = createMockImage(1024, 1024);
      const mockFile = createMockFile(3 * 1024 * 1024); // 3MB
      const detections = [mockDetection];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.breakdown.technical).toBeGreaterThan(15); // Should be high
    });

    it('should detect and penalize blurry images', async () => {
      // Mock getImageData to return data that would indicate blur
      const mockCanvas = {
        width: 0,
        height: 0,
        getContext: jasmine.createSpy('getContext').and.returnValue({
          drawImage: jasmine.createSpy('drawImage'),
          getImageData: jasmine.createSpy('getImageData').and.returnValue({
            data: new Uint8ClampedArray(100 * 100 * 4).fill(128) // Low variance = blur
          })
        })
      };
      
      spyOn(document, 'createElement').and.returnValue(mockCanvas as any);
      
      const mockImg = createMockImage(1024, 1024);
      const mockFile = createMockFile(2 * 1024 * 1024);
      const detections = [mockDetection];
      
      const score = await service.calculateQualityScore(mockImg, detections, mockFile);
      
      expect(score.suggestions).toContain('Image appears blurry. Use better focus or lighting.');
    });
  });

  describe('FLUX Compatibility Assessment', () => {
    it('should assess brightness levels', async () => {
      // Mock very bright image data
      const brightData = new Uint8ClampedArray(1024 * 1024 * 4);
      for (let i = 0; i < brightData.length; i += 4) {
        brightData[i] = 250;     // R
        brightData[i + 1] = 250; // G
        brightData[i + 2] = 250; // B
        brightData[i + 3] = 255; // A
      }
      
      const mockCanvas = {
        width: 1024,
        height: 1024,
        getContext: jasmine.createSpy('getContext').and.returnValue({
          drawImage: jasmine.createSpy('drawImage'),
          getImageData: jasmine.createSpy('getImageData').and.returnValue({
            data: brightData
          })
        })
      };
      
      spyOn(document, 'createElement').and.returnValue(mockCanvas as any);
      
      const score = await service.calculateQualityScore(mockImg, [mockDetection], mockFile);
      
      expect(score.suggestions).toContain('Image appears overexposed. Reduce lighting or use better camera settings.');
    });

    it('should detect underexposed images', async () => {
      // Mock very dark image data
      const darkData = new Uint8ClampedArray(1024 * 1024 * 4);
      for (let i = 0; i < darkData.length; i += 4) {
        darkData[i] = 10;     // R
        darkData[i + 1] = 10; // G
        darkData[i + 2] = 10; // B
        darkData[i + 3] = 255; // A
      }
      
      const mockCanvas = {
        width: 1024,
        height: 1024,
        getContext: jasmine.createSpy('getContext').and.returnValue({
          drawImage: jasmine.createSpy('drawImage'),
          getImageData: jasmine.createSpy('getImageData').and.returnValue({
            data: darkData
          })
        })
      };
      
      spyOn(document, 'createElement').and.returnValue(mockCanvas as any);
      
      const score = await service.calculateQualityScore(mockImg, [mockDetection], mockFile);
      
      expect(score.suggestions).toContain('Image appears underexposed. Increase lighting for better visibility.');
    });
  });

  describe('Lighting Assessment', () => {
    it('should assess good lighting positively', async () => {
      // Mock well-lit image (moderate brightness)
      const wellLitData = new Uint8ClampedArray(200 * 200 * 4);
      for (let i = 0; i < wellLitData.length; i += 4) {
        wellLitData[i] = 120;     // R
        wellLitData[i + 1] = 120; // G
        wellLitData[i + 2] = 120; // B
        wellLitData[i + 3] = 255; // A
      }
      
      const mockCanvas = {
        width: 200,
        height: 200,
        getContext: jasmine.createSpy('getContext').and.returnValue({
          drawImage: jasmine.createSpy('drawImage'),
          getImageData: jasmine.createSpy('getImageData').and.returnValue({
            data: wellLitData
          })
        })
      };
      
      spyOn(document, 'createElement').and.returnValue(mockCanvas as any);
      
      const score = await service.calculateQualityScore(mockImg, [mockDetection], mockFile);
      
      expect(score.breakdown.lighting).toBeGreaterThan(8); // Should get bonus for good lighting
    });

    it('should handle lighting analysis errors', async () => {
      const mockCanvas = {
        width: 0,
        height: 0,
        getContext: jasmine.createSpy('getContext').and.returnValue({
          drawImage: jasmine.createSpy('drawImage'),
          getImageData: jasmine.createSpy('getImageData').and.throwError('Canvas error')
        })
      };
      
      spyOn(document, 'createElement').and.returnValue(mockCanvas as any);
      
      const score = await service.calculateQualityScore(mockImg, [mockDetection], mockFile);
      
      expect(score.suggestions).toContain('Unable to assess lighting quality.');
    });
  });

  describe('Error Handling', () => {
    it('should handle missing canvas context', async () => {
      spyOn(document, 'createElement').and.returnValue({
        getContext: () => null
      } as any);
      
      const score = await service.calculateQualityScore(mockImg, [mockDetection], mockFile);
      
      expect(score).toBeDefined();
      expect(score.overall).toBeGreaterThan(0);
    });

    it('should handle invalid image dimensions', async () => {
      const invalidImg = createMockImage(0, 0);
      
      const score = await service.calculateQualityScore(invalidImg, [mockDetection], mockFile);
      
      expect(score).toBeDefined();
      expect(score.overall).toBeGreaterThan(0);
    });

    it('should handle malformed detection data', async () => {
      const invalidDetection = {
        detection: {
          score: null,
          box: null
        }
      };
      
      const score = await service.calculateQualityScore(mockImg, [invalidDetection as any], mockFile);
      
      expect(score).toBeDefined();
    });
  });

  describe('Integration with Interface', () => {
    it('should satisfy IImageQualityService contract', () => {
      const interfaceService: IImageQualityService = service;
      
      expect(interfaceService.calculateQualityScore).toBeDefined();
      expect(interfaceService.getDefaultQualityScore).toBeDefined();
      expect(interfaceService.loadImageElement).toBeDefined();
    });

    it('should return Promise from calculateQualityScore', () => {
      const result = service.calculateQualityScore(mockImg, [], mockFile);
      expect(result).toBeInstanceOf(Promise);
    });

    it('should return valid QualityScore from getDefaultQualityScore', () => {
      const score = service.getDefaultQualityScore();
      expect(typeof score.overall).toBe('number');
      expect(typeof score.breakdown).toBe('object');
      expect(Array.isArray(score.suggestions)).toBeTrue();
    });
  });

  describe('Performance', () => {
    it('should complete quality assessment within reasonable time', async () => {
      const mockImg = createMockImage(1024, 1024);
      const mockFile = createMockFile(2 * 1024 * 1024);
      const detections = [mockDetection];
      
      const startTime = performance.now();
      await service.calculateQualityScore(mockImg, detections, mockFile);
      const endTime = performance.now();
      
      expect(endTime - startTime).toBeLessThan(1000); // Should complete within 1 second
    });

    it('should handle large images efficiently', async () => {
      const largeImg = createMockImage(4096, 4096);
      const largeFile = createMockFile(10 * 1024 * 1024); // 10MB
      
      const startTime = performance.now();
      await service.calculateQualityScore(largeImg, [mockDetection], largeFile);
      const endTime = performance.now();
      
      expect(endTime - startTime).toBeLessThan(2000); // Should still complete reasonably quickly
    });
  });
});