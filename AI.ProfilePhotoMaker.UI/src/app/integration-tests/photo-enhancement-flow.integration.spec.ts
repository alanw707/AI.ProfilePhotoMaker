import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';

import { PhotoEnhancementComponent } from '../components/photo-enhancement/photo-enhancement.component';
import { AuthService } from '../services/auth.service';
import { ReplicateService } from '../services/replicate.service';
import { FileUploadService } from '../services/file-upload.service';
import { DashboardStateService } from '../services/dashboard-state.service';
import { ConfigService } from '../services/config.service';
import { HeaderNavigationComponent } from '../shared/header-navigation/header-navigation.component';

// Mock components
@Component({ template: '' })
class MockHeaderNavigationComponent {}

// Test utilities
function createMockFile(name = 'test.jpg', type = 'image/jpeg', size: number = 1024 * 1024): File {
  const file = new File(['mock content'], name, { type });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

function createMockFileReader(result = 'data:image/jpeg;base64,mock-data'): FileReader {
  const reader = {
    readAsDataURL: jasmine.createSpy('readAsDataURL').and.callFake(function (_file: File) {
      setTimeout(() => {
        this.onload({ target: { result } });
      }, 0);
    }),
    onload: null as any,
    onerror: null as any,
  };
  return reader as any;
}

describe('Photo Enhancement Flow Integration Tests', () => {
  let component: PhotoEnhancementComponent;
  let fixture: ComponentFixture<PhotoEnhancementComponent>;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;
  let replicateService: jasmine.SpyObj<ReplicateService>;
  let fileUploadService: jasmine.SpyObj<FileUploadService>;
  let stateService: jasmine.SpyObj<DashboardStateService>;
  let configService: jasmine.SpyObj<ConfigService>;

  beforeEach(async () => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isAuthenticated', 'getCurrentUserId']);
    const replicateSpy = jasmine.createSpyObj('ReplicateService', [
      'enhancePhoto',
      'getPredictionStatus',
    ]);
    const fileUploadSpy = jasmine.createSpyObj('FileUploadService', ['uploadSingleImage']);
    const stateSpy = jasmine.createSpyObj(
      'DashboardStateService',
      ['getState', 'setState', 'loadInitialDashboardData'],
      {
        state$: of({
          creditsInfo: { availableCredits: 5 },
          isLoading: false,
        }),
      }
    );
    const configSpy = jasmine.createSpyObj('ConfigService', ['baseUrl']);

    await TestBed.configureTestingModule({
      imports: [
        CommonModule,
        FormsModule,
        HttpClientTestingModule,
        RouterTestingModule,
        PhotoEnhancementComponent,
      ],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: ReplicateService, useValue: replicateSpy },
        { provide: FileUploadService, useValue: fileUploadSpy },
        { provide: DashboardStateService, useValue: stateSpy },
        { provide: ConfigService, useValue: configSpy },
      ],
    })
      .overrideComponent(PhotoEnhancementComponent, {
        remove: { imports: [HeaderNavigationComponent] },
        add: { imports: [MockHeaderNavigationComponent] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(PhotoEnhancementComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    replicateService = TestBed.inject(ReplicateService) as jasmine.SpyObj<ReplicateService>;
    fileUploadService = TestBed.inject(FileUploadService) as jasmine.SpyObj<FileUploadService>;
    stateService = TestBed.inject(DashboardStateService) as jasmine.SpyObj<DashboardStateService>;
    configService = TestBed.inject(ConfigService) as jasmine.SpyObj<ConfigService>;

    // Setup default mocks
    authService.isAuthenticated.and.returnValue(true);
    stateService.getState.and.returnValue({
      creditsInfo: { availableCredits: 5 },
      isLoading: false,
    });
    configService.baseUrl.and.returnValue('http://localhost:5032');
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('Component Initialization', () => {
    it('should initialize with default state', () => {
      fixture.detectChanges();

      expect(component.selectedFile).toBe(null);
      expect(component.imagePreview).toBe(null);
      expect(component.enhancementType).toBe('background');
      expect(component.isProcessing).toBe(false);
      expect(component.enhancedImage).toBe(null);
      expect(component.errorMessage).toBe('');
    });

    it('should load credits on initialization', () => {
      component.ngOnInit();

      expect(stateService.getState).toHaveBeenCalled();
      expect(component.creditsInfo).toEqual({ availableCredits: 5 });
    });

    it('should load dashboard data if credits not available', () => {
      stateService.getState.and.returnValue({
        creditsInfo: null,
        isLoading: false,
      });

      component.ngOnInit();

      expect(stateService.loadInitialDashboardData).toHaveBeenCalled();
    });
  });

  describe('File Upload and Validation', () => {
    it('should handle file selection successfully', () => {
      const mockFile = createMockFile();
      const mockFileReader = createMockFileReader();
      spyOn(window, 'FileReader').and.returnValue(mockFileReader);

      const event = { target: { files: [mockFile] } };
      component.onFileSelected(event);

      expect(component.selectedFile).toBe(mockFile);
      expect(component.errorMessage).toBe('');
      expect(mockFileReader.readAsDataURL).toHaveBeenCalledWith(mockFile);
    });

    it('should validate file type', () => {
      const invalidFile = createMockFile('test.txt', 'text/plain');
      const event = { target: { files: [invalidFile] } };

      component.onFileSelected(event);

      expect(component.selectedFile).toBe(null);
      expect(component.errorMessage).toBe('Please select a valid image file.');
    });

    it('should validate file size', () => {
      const oversizedFile = createMockFile('large.jpg', 'image/jpeg', 8 * 1024 * 1024); // 8MB
      const event = { target: { files: [oversizedFile] } };

      component.onFileSelected(event);

      expect(component.selectedFile).toBe(null);
      expect(component.errorMessage).toBe('File size must be less than 7MB.');
    });

    it('should handle drag and drop file upload', () => {
      const mockFile = createMockFile();
      const mockFileReader = createMockFileReader();
      spyOn(window, 'FileReader').and.returnValue(mockFileReader);

      const dragEvent = {
        preventDefault: jasmine.createSpy('preventDefault'),
        dataTransfer: { files: [mockFile] },
      } as any;

      component.onDrop(dragEvent);

      expect(dragEvent.preventDefault).toHaveBeenCalled();
      expect(component.isDragOver).toBe(false);
      expect(component.selectedFile).toBe(mockFile);
    });

    it('should handle file preview creation', done => {
      const mockFile = createMockFile();
      const mockFileReader = createMockFileReader('data:image/jpeg;base64,preview-data');
      spyOn(window, 'FileReader').and.returnValue(mockFileReader);

      component.processFile(mockFile);

      setTimeout(() => {
        expect(component.imagePreview).toBe('data:image/jpeg;base64,preview-data');
        done();
      }, 10);
    });
  });

  describe('Enhancement Processing Workflow', () => {
    beforeEach(() => {
      const mockFile = createMockFile();
      component.selectedFile = mockFile;
      component.creditsInfo = { availableCredits: 5 };
    });

    it('should complete full enhancement workflow successfully', async () => {
      // Mock successful upload
      fileUploadService.uploadSingleImage.and.returnValue(
        of({
          progress: 100,
          response: {
            success: true,
            data: {
              url: '/uploads/test-image.jpg',
              fileName: 'test-image.jpg',
            },
          },
        })
      );

      // Mock successful enhancement
      replicateService.enhancePhoto.and.returnValue(
        of({
          success: true,
          data: {
            prediction: { id: 'pred-123' },
            creditsRemaining: 4,
          },
        })
      );

      // Mock successful prediction polling
      replicateService.getPredictionStatus.and.returnValue(
        of({
          success: true,
          data: {
            status: 'succeeded',
            output: ['https://enhanced-image.jpg'],
            dataUrl: 'data:image/jpeg;base64,enhanced-data',
          },
        })
      );

      await component.startEnhancement();

      expect(component.isProcessing).toBe(false);
      expect(component.processingProgress).toBe(100);
      expect(component.processingStatus).toBe('Enhancement complete!');
      expect(component.enhancedImage).toEqual({
        url: 'data:image/jpeg;base64,enhanced-data',
        type: 'enhanced',
      });
      expect(stateService.setState).toHaveBeenCalledWith({
        creditsInfo: {
          availableCredits: 5,
          availableCredits: 4,
        },
      });
    });

    it('should handle upload failure gracefully', async () => {
      fileUploadService.uploadSingleImage.and.returnValue(
        throwError(() => new Error('Upload failed'))
      );

      await component.startEnhancement();

      expect(component.isProcessing).toBe(false);
      expect(component.errorMessage).toBe('Upload failed');
      expect(component.enhancedImage).toBe(null);
    });

    it('should handle enhancement API failure', async () => {
      // Mock successful upload
      fileUploadService.uploadSingleImage.and.returnValue(
        of({
          progress: 100,
          response: {
            success: true,
            data: {
              url: '/uploads/test-image.jpg',
              fileName: 'test-image.jpg',
            },
          },
        })
      );

      // Mock failed enhancement
      replicateService.enhancePhoto.and.returnValue(
        of({
          success: false,
          error: { message: 'Enhancement failed' },
        })
      );

      await component.startEnhancement();

      expect(component.isProcessing).toBe(false);
      expect(component.errorMessage).toBe('Enhancement failed');
    });

    it('should handle prediction polling timeout', async () => {
      // Mock successful upload
      fileUploadService.uploadSingleImage.and.returnValue(
        of({
          progress: 100,
          response: {
            success: true,
            data: {
              url: '/uploads/test-image.jpg',
              fileName: 'test-image.jpg',
            },
          },
        })
      );

      // Mock successful enhancement
      replicateService.enhancePhoto.and.returnValue(
        of({
          success: true,
          data: {
            prediction: { id: 'pred-123' },
            creditsRemaining: 4,
          },
        })
      );

      // Mock prediction still processing (will timeout)
      replicateService.getPredictionStatus.and.returnValue(
        of({
          success: true,
          data: {
            status: 'processing',
            output: null,
          },
        })
      );

      // Mock polling timeout by spying on setTimeout
      spyOn(window, 'setTimeout').and.callFake((_callback: Function) => {
        // Return promise that never resolves to simulate timeout
        return setTimeout(() => {}, 10) as any;
      });

      try {
        await component.startEnhancement();
      } catch {
        expect(component.errorMessage).toContain('Enhancement timed out');
      }
    });
  });

  describe('Credit System Integration', () => {
    it('should prevent enhancement when insufficient credits', async () => {
      component.selectedFile = createMockFile();
      component.creditsInfo = { availableCredits: 0 };

      await component.startEnhancement();

      expect(fileUploadService.uploadSingleImage).not.toHaveBeenCalled();
      expect(replicateService.enhancePhoto).not.toHaveBeenCalled();
    });

    it('should update credits after successful enhancement', async () => {
      component.selectedFile = createMockFile();
      component.creditsInfo = { availableCredits: 5 };

      // Mock successful workflow
      fileUploadService.uploadSingleImage.and.returnValue(
        of({
          progress: 100,
          response: {
            success: true,
            data: {
              url: '/uploads/test-image.jpg',
              fileName: 'test-image.jpg',
            },
          },
        })
      );

      replicateService.enhancePhoto.and.returnValue(
        of({
          success: true,
          data: {
            prediction: { id: 'pred-123' },
            creditsRemaining: 4,
          },
        })
      );

      replicateService.getPredictionStatus.and.returnValue(
        of({
          success: true,
          data: {
            status: 'succeeded',
            output: ['https://enhanced-image.jpg'],
          },
        })
      );

      await component.startEnhancement();

      expect(stateService.setState).toHaveBeenCalledWith({
        creditsInfo: {
          availableCredits: 5,
          availableCredits: 4,
        },
      });
    });
  });

  describe('Progress Tracking', () => {
    it('should track upload progress correctly', async () => {
      component.selectedFile = createMockFile();
      component.creditsInfo = { availableCredits: 5 };

      // Mock progressive upload
      fileUploadService.uploadSingleImage.and.returnValue(
        of({
          progress: 50,
          response: null,
        })
      );

      component['uploadImageForEnhancement']();

      setTimeout(() => {
        expect(component.processingProgress).toBe(10); // 50% * 0.2 = 10%
      }, 0);
    });

    it('should update progress during enhancement phases', async () => {
      component.selectedFile = createMockFile();
      component.creditsInfo = { availableCredits: 5 };

      // Mock workflow phases
      fileUploadService.uploadSingleImage.and.returnValue(
        of({
          progress: 100,
          response: {
            success: true,
            data: {
              url: '/uploads/test-image.jpg',
              fileName: 'test-image.jpg',
            },
          },
        })
      );

      replicateService.enhancePhoto.and.returnValue(
        of({
          success: true,
          data: {
            prediction: { id: 'pred-123' },
            creditsRemaining: 4,
          },
        })
      );

      replicateService.getPredictionStatus.and.returnValue(
        of({
          success: true,
          data: {
            status: 'succeeded',
            output: ['https://enhanced-image.jpg'],
          },
        })
      );

      await component.startEnhancement();

      expect(component.processingProgress).toBe(100);
      expect(component.processingStatus).toBe('Enhancement complete!');
    });
  });

  describe('Error Handling and Recovery', () => {
    it('should handle network errors gracefully', async () => {
      component.selectedFile = createMockFile();
      component.creditsInfo = { availableCredits: 5 };

      fileUploadService.uploadSingleImage.and.returnValue(
        throwError(() => ({
          status: 0,
          error: { message: 'Network error' },
        }))
      );

      await component.startEnhancement();

      expect(component.isProcessing).toBe(false);
      expect(component.errorMessage).toContain('Network error');
    });

    it('should handle prediction failures', async () => {
      component.selectedFile = createMockFile();
      component.creditsInfo = { availableCredits: 5 };

      fileUploadService.uploadSingleImage.and.returnValue(
        of({
          progress: 100,
          response: {
            success: true,
            data: {
              url: '/uploads/test-image.jpg',
              fileName: 'test-image.jpg',
            },
          },
        })
      );

      replicateService.enhancePhoto.and.returnValue(
        of({
          success: true,
          data: {
            prediction: { id: 'pred-123' },
            creditsRemaining: 4,
          },
        })
      );

      replicateService.getPredictionStatus.and.returnValue(
        of({
          success: true,
          data: {
            status: 'failed',
            error: 'Processing failed',
          },
        })
      );

      await component.startEnhancement();

      expect(component.isProcessing).toBe(false);
      expect(component.errorMessage).toBe('Processing failed');
    });
  });

  describe('UI Interactions', () => {
    it('should handle file removal', () => {
      component.selectedFile = createMockFile();
      component.imagePreview = 'data:image/jpeg;base64,preview';
      component.errorMessage = 'Some error';

      component.removeFile();

      expect(component.selectedFile).toBe(null);
      expect(component.imagePreview).toBe(null);
      expect(component.errorMessage).toBe('');
    });

    it('should handle download functionality', () => {
      const mockLink = {
        href: '',
        download: '',
        click: jasmine.createSpy('click'),
      };
      spyOn(document, 'createElement').and.returnValue(mockLink as any);

      component.enhancedImage = {
        url: 'data:image/jpeg;base64,enhanced-data',
        type: 'enhanced',
      };

      component.downloadEnhanced();

      expect(mockLink.href).toBe('data:image/jpeg;base64,enhanced-data');
      expect(mockLink.download).toContain('enhanced-photo-');
      expect(mockLink.download).toContain('.png');
      expect(mockLink.click).toHaveBeenCalled();
    });

    it('should handle component reset', () => {
      component.selectedFile = createMockFile();
      component.imagePreview = 'preview';
      component.enhancedImage = { url: 'enhanced', type: 'enhanced' };
      component.errorMessage = 'error';
      component.isProcessing = true;

      component.resetComponent();

      expect(component.selectedFile).toBe(null);
      expect(component.imagePreview).toBe(null);
      expect(component.enhancedImage).toBe(null);
      expect(component.errorMessage).toBe('');
      expect(component.isProcessing).toBe(false);
      expect(stateService.loadInitialDashboardData).toHaveBeenCalled();
    });
  });

  describe('State Synchronization', () => {
    it('should sync with dashboard state service', () => {
      const mockState = {
        creditsInfo: { availableCredits: 3 },
        isLoading: false,
      };

      stateService.state$ = of(mockState);

      component.ngOnInit();

      expect(component.creditsInfo).toEqual({ availableCredits: 3 });
      expect(component.isLoadingCredits).toBe(false);
    });

    it('should show loading state when credits unavailable', () => {
      const mockState = {
        creditsInfo: null,
        isLoading: true,
      };

      stateService.state$ = of(mockState);

      component.ngOnInit();

      expect(component.isLoadingCredits).toBe(true);
    });
  });

  describe('Enhancement Type Selection', () => {
    it('should handle enhancement type changes', () => {
      component.enhancementType = 'background';

      // Simulate type change
      component.enhancementType = 'social';

      expect(component.enhancementType).toBe('social');
    });

    it('should send correct enhancement type to API', async () => {
      component.selectedFile = createMockFile();
      component.creditsInfo = { availableCredits: 5 };
      component.enhancementType = 'cartoon';

      fileUploadService.uploadSingleImage.and.returnValue(
        of({
          progress: 100,
          response: {
            success: true,
            data: {
              url: '/uploads/test-image.jpg',
              fileName: 'test-image.jpg',
            },
          },
        })
      );

      replicateService.enhancePhoto.and.returnValue(
        of({
          success: true,
          data: {
            prediction: { id: 'pred-123' },
            creditsRemaining: 4,
          },
        })
      );

      replicateService.getPredictionStatus.and.returnValue(
        of({
          success: true,
          data: {
            status: 'succeeded',
            output: ['https://enhanced-image.jpg'],
          },
        })
      );

      await component.startEnhancement();

      expect(replicateService.enhancePhoto).toHaveBeenCalledWith({
        imageUrl: 'http://localhost:5032/uploads/test-image.jpg',
        enhancementType: 'cartoon',
      });
    });
  });
});
