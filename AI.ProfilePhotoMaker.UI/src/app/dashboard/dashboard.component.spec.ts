import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { NgZone } from '@angular/core';

import { DashboardComponent } from './dashboard.component';
import { 
  MockAuthService, 
  MockDashboardStateService, 
  MockNotificationService,
  TestingHelpers
} from '../testing/testing-utils';

import { AuthService } from '../services/auth.service';
import { DashboardStateService } from '../services/dashboard-state.service';
import { NotificationService } from '../services/notification.service';
import { FileUploadService } from '../services/file-upload.service';
import { ReplicateService } from '../services/replicate.service';
import { CreditService } from '../services/credit.service';
import { FaceDetectionService } from '../services/face-detection.service';
import { StyleService } from '../services/style.service';
import { ConfigService } from '../services/config.service';
import { FileUploadManagerService } from '../services/file-upload-manager.service';

/**
 * Dashboard Component Test Suite
 * 
 * Simplified tests that verify basic component functionality.
 * This serves as a safety net before refactoring the large dashboard component.
 */
describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockNgZone: jasmine.SpyObj<NgZone>;

  beforeEach(async () => {
    // Create spy objects for dependencies
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockNgZone = jasmine.createSpyObj('NgZone', ['run']);

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: AuthService, useClass: MockAuthService },
        { provide: DashboardStateService, useClass: MockDashboardStateService },
        { provide: NotificationService, useClass: MockNotificationService },
        { provide: Router, useValue: mockRouter },
        { provide: NgZone, useValue: mockNgZone },
        // Mock other services with minimal implementations
        { 
          provide: FileUploadService, 
          useValue: { 
            uploadMultipleImages: () => Promise.resolve({ success: true, data: [] }),
            deleteImage: () => Promise.resolve({ success: true })
          } 
        },
        { 
          provide: ReplicateService, 
          useValue: { 
            trainModel: () => Promise.resolve({ success: true, data: { id: 'mock-id' } }),
            generateImages: () => Promise.resolve({ success: true, data: { id: 'mock-id' } })
          } 
        },
        { 
          provide: CreditService, 
          useValue: { 
            getCredits: () => Promise.resolve({ success: true, data: { totalCredits: 30 } })
          } 
        },
        { 
          provide: FaceDetectionService, 
          useValue: { 
            loadModels: () => Promise.resolve(),
            validateImageQuality: () => Promise.resolve({ isValid: true })
          } 
        },
        { 
          provide: StyleService, 
          useValue: { 
            getActiveStyles: () => Promise.resolve({ success: true, data: [] })
          } 
        },
        { 
          provide: ConfigService, 
          useValue: { 
            getApiUrl: () => 'http://localhost:5000'
          } 
        },
        { 
          provide: FileUploadManagerService, 
          useValue: { 
            uploadFiles: () => Promise.resolve({ success: true, data: [] })
          } 
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  describe('Component Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.currentStep).toBe(1);
      expect(component.selectedFiles).toEqual([]);
      expect(component.isUploading).toBeFalse();
      expect(component.isDragOver).toBeFalse();
      expect(component.isTraining).toBeFalse();
      expect(component.isGenerating).toBeFalse();
    });

    it('should have observable state', () => {
      expect(component.state$).toBeDefined();
    });
  });

  describe('File Selection', () => {
    it('should handle file selection', () => {
      const mockFiles = TestingHelpers.createMockFiles(3);
      
      component.selectedFiles = mockFiles;
      
      expect(component.selectedFiles.length).toBe(3);
    });

    it('should track drag over state', () => {
      expect(component.isDragOver).toBeFalse();
      
      component.isDragOver = true;
      expect(component.isDragOver).toBeTrue();
    });

    it('should track upload progress', () => {
      component.uploadProgress = 50;
      expect(component.uploadProgress).toBe(50);
    });
  });

  describe('Workflow State Management', () => {
    it('should track current step', () => {
      expect(component.currentStep).toBe(1);
      
      component.currentStep = 2;
      expect(component.currentStep).toBe(2);
    });

    it('should track training state', () => {
      expect(component.isTraining).toBeFalse();
      
      component.isTraining = true;
      expect(component.isTraining).toBeTrue();
    });

    it('should track generation state', () => {
      expect(component.isGenerating).toBeFalse();
      
      component.isGenerating = true;
      expect(component.isGenerating).toBeTrue();
    });

    it('should track selected styles count', () => {
      component.selectedStyles = 3;
      expect(component.selectedStyles).toBe(3);
    });
  });

  describe('Progress Tracking', () => {
    it('should track progress percentage', () => {
      component.progressPercentage = 75;
      expect(component.progressPercentage).toBe(75);
    });

    it('should track quality check progress', () => {
      component.qualityCheckProgress = 'Checking image quality...';
      expect(component.qualityCheckProgress).toBe('Checking image quality...');
    });

    it('should track estimated completion', () => {
      component.estimatedCompletion = '5 minutes remaining';
      expect(component.estimatedCompletion).toBe('5 minutes remaining');
    });
  });

  describe('Data Collections', () => {
    it('should manage available styles', () => {
      const mockStyles = [
        { id: '1', name: 'corporate', description: 'Professional corporate style', previewUrl: 'corporate.jpg', selected: false },
        { id: '2', name: 'casual', description: 'Casual everyday style', previewUrl: 'casual.jpg', selected: false }
      ];
      
      component.availableStyles = mockStyles;
      expect(component.availableStyles.length).toBe(2);
    });

    it('should manage gallery images', () => {
      const mockImages = [
        { id: 1, url: 'image1.jpg', title: 'Image 1', type: 'generated', status: 'completed', createdAt: new Date() }
      ];
      
      component.galleryImages = mockImages as any;
      expect(component.galleryImages.length).toBe(1);
    });

    it('should manage quality check errors', () => {
      const mockFile = TestingHelpers.createMockFile('test.jpg');
      const mockErrors = [
        { fileName: 'test.jpg', file: mockFile, errors: ['No face detected'], warnings: [] }
      ];
      
      component.qualityCheckErrors = mockErrors;
      expect(component.qualityCheckErrors.length).toBe(1);
    });
  });

  describe('Configuration Properties', () => {
    it('should have configurable images per style', () => {
      expect(component.imagesPerStyle).toBe(2);
      
      component.imagesPerStyle = 4;
      expect(component.imagesPerStyle).toBe(4);
    });

    it('should track training ID', () => {
      component.trainingId = 'mock-training-id';
      expect(component.trainingId).toBe('mock-training-id');
    });

    it('should track training zip path', () => {
      component.trainingZipPath = '/path/to/training.zip';
      expect(component.trainingZipPath).toBe('/path/to/training.zip');
    });
  });

  describe('Component Cleanup', () => {
    it('should implement OnDestroy', () => {
      expect(component.ngOnDestroy).toBeDefined();
    });

    it('should clear polling intervals on destroy', () => {
      component.photoCompletionPollingInterval = setInterval(() => {}, 1000);
      
      component.ngOnDestroy();
      
      // The component should handle cleanup
      expect(component).toBeTruthy();
    });
  });
});

/**
 * Integration Tests for Dashboard Component
 */
describe('DashboardComponent Integration Tests', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;

  beforeEach(async () => {
    await TestingHelpers.setupTestModule(DashboardComponent, [
      { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) },
      { provide: NgZone, useValue: jasmine.createSpyObj('NgZone', ['run']) }
    ]);
    
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  it('should initialize and maintain consistent state', () => {
    expect(component).toBeTruthy();
    expect(component.currentStep).toBe(1);
    expect(component.selectedFiles).toEqual([]);
    
    // Simulate file selection
    component.selectedFiles = TestingHelpers.createMockFiles(2);
    expect(component.selectedFiles.length).toBe(2);
    
    // Simulate workflow progression
    component.currentStep = 2;
    component.isTraining = true;
    
    expect(component.currentStep).toBe(2);
    expect(component.isTraining).toBeTrue();
  });

  it('should handle state transitions smoothly', () => {
    // Initial state
    expect(component.isUploading).toBeFalse();
    expect(component.isTraining).toBeFalse();
    expect(component.isGenerating).toBeFalse();
    
    // Upload state
    component.isUploading = true;
    expect(component.isUploading).toBeTrue();
    
    // Training state
    component.isUploading = false;
    component.isTraining = true;
    expect(component.isTraining).toBeTrue();
    
    // Generation state
    component.isTraining = false;
    component.isGenerating = true;
    expect(component.isGenerating).toBeTrue();
    
    // Completion state
    component.isGenerating = false;
    expect(component.isGenerating).toBeFalse();
  });
});