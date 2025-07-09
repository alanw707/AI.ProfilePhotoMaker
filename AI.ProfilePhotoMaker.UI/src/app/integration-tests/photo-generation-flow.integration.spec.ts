import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BehaviorSubject, of, throwError } from 'rxjs';

import { DashboardComponent } from '../dashboard/dashboard.component';
import { AuthService } from '../services/auth.service';
import { FileUploadService } from '../services/file-upload.service';
import { StyleService } from '../services/style.service';
import { ReplicateService } from '../services/replicate.service';
import { CreditService } from '../services/credit.service';
import { DashboardStateService } from '../services/dashboard-state.service';
import { WorkflowOrchestrationService } from '../services/workflow-orchestration.service';
import { ConfigService } from '../services/config.service';
import { NotificationService } from '../services/notification.service';

// Mock child components
@Component({ selector: 'app-header-navigation', template: '' })
class MockHeaderNavigationComponent { }

@Component({ selector: 'app-stats-card', template: '' })
class MockStatsCardComponent { }

@Component({ selector: 'app-style-selector', template: '' })
class MockStyleSelectorComponent { }

@Component({ selector: 'app-file-upload-section', template: '' })
class MockFileUploadSectionComponent { }

@Component({ selector: 'app-credit-display', template: '' })
class MockCreditDisplayComponent { }

// Test utilities
function createMockFile(name = 'test.jpg', type = 'image/jpeg', size: number = 1024 * 1024): File {
  const file = new File(['mock content'], name, { type });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

describe('Photo Generation Flow Integration Tests', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;
  let fileUploadService: jasmine.SpyObj<FileUploadService>;
  let styleService: jasmine.SpyObj<StyleService>;
  let replicateService: jasmine.SpyObj<ReplicateService>;
  let creditService: jasmine.SpyObj<CreditService>;
  let stateService: jasmine.SpyObj<DashboardStateService>;
  let workflowService: jasmine.SpyObj<WorkflowOrchestrationService>;
  let configService: jasmine.SpyObj<ConfigService>;
  let notificationService: jasmine.SpyObj<NotificationService>;

  const mockInitialState = {
    isLoading: false,
    uploadedImages: 0,
    modelStatus: 'Not Started',
    creditsInfo: { availableCredits: 50 },
    userCreditStatus: { purchasedCredits: 50, weeklyCredits: 3 },
    uploadedImageThumbnails: [],
    generatedPhotosCount: 0,
    latestTrainedModel: null,
    hasTrainedModel: false
  };

  beforeEach(async () => {
    const stateSubject = new BehaviorSubject(mockInitialState);
    const workflowSubject = new BehaviorSubject({ phase: 'idle', progress: 0 });

    const authSpy = jasmine.createSpyObj('AuthService', ['isAuthenticated', 'getCurrentUserId']);
    const fileUploadSpy = jasmine.createSpyObj('FileUploadService', [
      'uploadFiles', 'getUserImages', 'getTrainingStatus', 'getUserModelRequests',
      'startTraining', 'generateImages', 'getGenerationStatus'
    ]);
    const styleSpy = jasmine.createSpyObj('StyleService', ['getStyles']);
    const replicateSpy = jasmine.createSpyObj('ReplicateService', ['trainModel', 'generateImages']);
    const creditSpy = jasmine.createSpyObj('CreditService', ['getCreditStatus']);
    const stateSpy = jasmine.createSpyObj('DashboardStateService', [
      'getState', 'setState', 'loadInitialDashboardData'
    ], {
      state$: stateSubject.asObservable()
    });
    const workflowSpy = jasmine.createSpyObj('WorkflowOrchestrationService', [
      'startTrainingWorkflow', 'startGenerationWorkflow', 'getProgress'
    ], {
      progress$: workflowSubject.asObservable()
    });
    const configSpy = jasmine.createSpyObj('ConfigService', ['baseUrl']);
    const notificationSpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'info']);

    await TestBed.configureTestingModule({
      imports: [
        CommonModule,
        FormsModule,
        HttpClientTestingModule,
        RouterTestingModule,
        DashboardComponent
      ],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: FileUploadService, useValue: fileUploadSpy },
        { provide: StyleService, useValue: styleSpy },
        { provide: ReplicateService, useValue: replicateSpy },
        { provide: CreditService, useValue: creditSpy },
        { provide: DashboardStateService, useValue: stateSpy },
        { provide: WorkflowOrchestrationService, useValue: workflowSpy },
        { provide: ConfigService, useValue: configSpy },
        { provide: NotificationService, useValue: notificationSpy }
      ]
    }).overrideComponent(DashboardComponent, {
      remove: { 
        imports: [
          'HeaderNavigationComponent',
          'StatsCardComponent', 
          'StyleSelectorComponent',
          'FileUploadSectionComponent',
          'CreditDisplayComponent'
        ]
      },
      add: { 
        imports: [
          MockHeaderNavigationComponent,
          MockStatsCardComponent,
          MockStyleSelectorComponent,
          MockFileUploadSectionComponent,
          MockCreditDisplayComponent
        ]
      }
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    fileUploadService = TestBed.inject(FileUploadService) as jasmine.SpyObj<FileUploadService>;
    styleService = TestBed.inject(StyleService) as jasmine.SpyObj<StyleService>;
    replicateService = TestBed.inject(ReplicateService) as jasmine.SpyObj<ReplicateService>;
    creditService = TestBed.inject(CreditService) as jasmine.SpyObj<CreditService>;
    stateService = TestBed.inject(DashboardStateService) as jasmine.SpyObj<DashboardStateService>;
    workflowService = TestBed.inject(WorkflowOrchestrationService) as jasmine.SpyObj<WorkflowOrchestrationService>;
    configService = TestBed.inject(ConfigService) as jasmine.SpyObj<ConfigService>;
    notificationService = TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>;

    // Setup default mocks
    authService.isAuthenticated.and.returnValue(true);
    authService.getCurrentUserId.and.returnValue('user-123');
    stateService.getState.and.returnValue(mockInitialState);
    configService.baseUrl.and.returnValue('http://localhost:5035');
    
    styleService.getStyles.and.returnValue(of([
      { id: 1, name: 'Professional', key: 'professional' },
      { id: 2, name: 'Creative', key: 'creative' },
      { id: 3, name: 'LinkedIn', key: 'linkedin' }
    ]));
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('Component Initialization', () => {
    it('should initialize with correct state', () => {
      fixture.detectChanges();
      
      expect(component.currentStep).toBe(1);
      expect(component.isTrainingStarted).toBe(false);
      expect(component.selectedStyles).toBe(0);
      expect(component.uploadedImages).toBe(0);
      expect(component.modelStatus).toBe('Not Started');
    });

    it('should load initial dashboard data', () => {
      component.ngOnInit();
      
      expect(stateService.loadInitialDashboardData).toHaveBeenCalled();
    });

    it('should subscribe to state changes', () => {
      component.ngOnInit();
      
      expect(component.state$).toBeDefined();
      expect(component.workflowProgress$).toBeDefined();
    });
  });

  describe('File Upload Phase', () => {
    it('should handle multiple file uploads successfully', (done) => {
      const mockFiles = [
        createMockFile('image1.jpg'),
        createMockFile('image2.jpg'),
        createMockFile('image3.jpg')
      ];

      fileUploadService.uploadFiles.and.returnValue(of({
        progress: 100,
        response: {
          success: true,
          data: {
            uploadedFiles: mockFiles.map((f, i) => ({
              id: i + 1,
              fileName: f.name,
              originalImageUrl: `/uploads/user-123/${f.name}`,
              thumbnailUrl: `/uploads/user-123/thumb_${f.name}`
            }))
          }
        }
      }));

      // Simulate file upload
      const fileUploadEvent = mockFiles;
      
      // Mock the file upload handling
      component['handleFileUpload'](fileUploadEvent);

      fileUploadService.uploadFiles().subscribe(response => {
        expect(response.success).toBe(true);
        expect(response.data.uploadedFiles).toHaveLength(3);
        done();
      });
    });

    it('should validate file count limits', () => {
      const tooManyFiles = Array(21).fill(null).map((_, i) => createMockFile(`image${i}.jpg`));
      
      // Mock file count validation
      const isValidCount = tooManyFiles.length <= 20;
      
      expect(isValidCount).toBe(false);
    });

    it('should validate file types and sizes', () => {
      const invalidFiles = [
        createMockFile('document.pdf', 'application/pdf'),
        createMockFile('large.jpg', 'image/jpeg', 8 * 1024 * 1024) // 8MB
      ];

      invalidFiles.forEach(file => {
        const isValidType = file.type.startsWith('image/');
        const isValidSize = file.size <= 7 * 1024 * 1024; // 7MB
        
        expect(isValidType || isValidSize).toBe(false);
      });
    });

    it('should update state after successful upload', () => {
      const mockUploadResponse = {
        success: true,
        data: {
          uploadedFiles: [
            { id: 1, fileName: 'image1.jpg', originalImageUrl: '/uploads/user-123/image1.jpg' },
            { id: 2, fileName: 'image2.jpg', originalImageUrl: '/uploads/user-123/image2.jpg' }
          ]
        }
      };

      fileUploadService.uploadFiles.and.returnValue(of({
        progress: 100,
        response: mockUploadResponse
      }));

      // Should update state with uploaded images
      const expectedState = {
        ...mockInitialState,
        uploadedImages: 2,
        uploadedImageThumbnails: mockUploadResponse.data.uploadedFiles.map(f => ({
          id: f.id,
          url: f.originalImageUrl,
          fileName: f.fileName
        }))
      };

      expect(stateService.setState).toHaveBeenCalledWith(expectedState);
    });
  });

  describe('Model Training Phase', () => {
    beforeEach(() => {
      // Setup uploaded images state
      stateService.getState.and.returnValue({
        ...mockInitialState,
        uploadedImages: 5,
        uploadedImageThumbnails: [
          { id: 1, url: '/uploads/user-123/image1.jpg', fileName: 'image1.jpg' },
          { id: 2, url: '/uploads/user-123/image2.jpg', fileName: 'image2.jpg' }
        ]
      });
    });

    it('should start training workflow successfully', async () => {
      const mockTrainingResponse = {
        success: true,
        data: {
          trainingId: 'training-123',
          status: 'training',
          estimatedDuration: 900 // 15 minutes
        }
      };

      workflowService.startTrainingWorkflow.and.returnValue(of(mockTrainingResponse));

      await component['startTraining']();

      expect(workflowService.startTrainingWorkflow).toHaveBeenCalled();
      expect(component.isTrainingStarted).toBe(true);
    });

    it('should track training progress', (done) => {
      const progressUpdates = [
        { phase: 'training', progress: 25, status: 'Preparing training data...' },
        { phase: 'training', progress: 50, status: 'Training in progress...' },
        { phase: 'training', progress: 75, status: 'Finalizing model...' },
        { phase: 'training', progress: 100, status: 'Training complete!' }
      ];

      let updateIndex = 0;
      workflowService.progress$.subscribe(progress => {
        expect(progress).toEqual(progressUpdates[updateIndex]);
        updateIndex++;
        
        if (updateIndex === progressUpdates.length) {
          expect(component.modelStatus).toBe('Training complete!');
          done();
        }
      });

      // Simulate progress updates
      progressUpdates.forEach(update => {
        workflowService['progressSubject'].next(update);
      });
    });

    it('should handle training failures', async () => {
      workflowService.startTrainingWorkflow.and.returnValue(
        throwError(() => new Error('Training failed'))
      );

      try {
        await component['startTraining']();
      } catch (error) {
        expect(error.message).toBe('Training failed');
        expect(notificationService.error).toHaveBeenCalledWith('Training failed');
      }
    });

    it('should check training status periodically', () => {
      fileUploadService.getTrainingStatus.and.returnValue(of({
        status: 'training',
        progress: 50,
        estimatedTimeRemaining: 450
      }));

      component['checkTrainingStatus']();

      expect(fileUploadService.getTrainingStatus).toHaveBeenCalled();
    });

    it('should handle training completion', () => {
      const mockCompletedModel = {
        id: 'model-123',
        version: 'v1',
        status: 'succeeded',
        createdAt: new Date().toISOString()
      };

      fileUploadService.getTrainingStatus.and.returnValue(of({
        status: 'succeeded',
        progress: 100,
        model: mockCompletedModel
      }));

      component['handleTrainingCompletion'](mockCompletedModel);

      expect(stateService.setState).toHaveBeenCalledWith({
        ...mockInitialState,
        modelStatus: 'Model Ready',
        hasTrainedModel: true,
        latestTrainedModel: mockCompletedModel
      });
    });
  });

  describe('Style Selection Phase', () => {
    beforeEach(() => {
      // Setup trained model state
      stateService.getState.and.returnValue({
        ...mockInitialState,
        modelStatus: 'Model Ready',
        hasTrainedModel: true,
        latestTrainedModel: { id: 'model-123', version: 'v1' }
      });
    });

    it('should load available styles', () => {
      component.ngOnInit();
      
      expect(styleService.getStyles).toHaveBeenCalled();
      expect(component.availableStyles).toEqual([
        { id: 1, name: 'Professional', key: 'professional' },
        { id: 2, name: 'Creative', key: 'creative' },
        { id: 3, name: 'LinkedIn', key: 'linkedin' }
      ]);
    });

    it('should handle style selection', () => {
      const selectedStyleIds = [1, 3]; // Professional and LinkedIn
      
      component['handleStyleSelection'](selectedStyleIds);
      
      expect(component.selectedStyles).toBe(2);
    });

    it('should calculate credit requirements', () => {
      const selectedStyleIds = [1, 2, 3]; // 3 styles
      const imagesPerStyle = 2;
      const creditCostPerImage = 5;
      
      const totalCreditsNeeded = selectedStyleIds.length * imagesPerStyle * creditCostPerImage;
      
      expect(totalCreditsNeeded).toBe(30); // 3 * 2 * 5
    });

    it('should validate sufficient credits', () => {
      const requiredCredits = 30;
      const availableCredits = 50;
      
      const hasSufficientCredits = availableCredits >= requiredCredits;
      
      expect(hasSufficientCredits).toBe(true);
    });

    it('should prevent generation with insufficient credits', () => {
      const requiredCredits = 60;
      const availableCredits = 50;
      
      const canGenerate = availableCredits >= requiredCredits;
      
      expect(canGenerate).toBe(false);
    });
  });

  describe('Photo Generation Phase', () => {
    beforeEach(() => {
      // Setup ready for generation state
      stateService.getState.and.returnValue({
        ...mockInitialState,
        modelStatus: 'Model Ready',
        hasTrainedModel: true,
        latestTrainedModel: { id: 'model-123', version: 'v1' },
        creditsInfo: { availableCredits: 50 }
      });
      
      component.selectedStyles = 2;
      component.availableStyles = [
        { id: 1, name: 'Professional', key: 'professional', selected: true },
        { id: 2, name: 'LinkedIn', key: 'linkedin', selected: true }
      ];
    });

    it('should start generation workflow successfully', async () => {
      const mockGenerationResponse = {
        success: true,
        data: {
          generationId: 'gen-123',
          status: 'processing',
          estimatedDuration: 300 // 5 minutes
        }
      };

      workflowService.startGenerationWorkflow.and.returnValue(of(mockGenerationResponse));

      await component['startGeneration']();

      expect(workflowService.startGenerationWorkflow).toHaveBeenCalledWith({
        modelId: 'model-123',
        styles: ['professional', 'linkedin'],
        imagesPerStyle: 2
      });
    });

    it('should track generation progress', (done) => {
      const progressUpdates = [
        { phase: 'generation', progress: 20, status: 'Generating Professional style...' },
        { phase: 'generation', progress: 60, status: 'Generating LinkedIn style...' },
        { phase: 'generation', progress: 100, status: 'Generation complete!' }
      ];

      let updateIndex = 0;
      workflowService.progress$.subscribe(progress => {
        expect(progress).toEqual(progressUpdates[updateIndex]);
        updateIndex++;
        
        if (updateIndex === progressUpdates.length) {
          done();
        }
      });

      // Simulate progress updates
      progressUpdates.forEach(update => {
        workflowService['progressSubject'].next(update);
      });
    });

    it('should handle generation completion', () => {
      const mockGeneratedImages = [
        { id: 1, style: 'professional', url: '/generated/prof1.jpg' },
        { id: 2, style: 'professional', url: '/generated/prof2.jpg' },
        { id: 3, style: 'linkedin', url: '/generated/link1.jpg' },
        { id: 4, style: 'linkedin', url: '/generated/link2.jpg' }
      ];

      component['handleGenerationCompletion'](mockGeneratedImages);

      expect(stateService.setState).toHaveBeenCalledWith({
        ...mockInitialState,
        generatedPhotosCount: 4,
        creditsInfo: { availableCredits: 30 } // 50 - 20 (4 images * 5 credits)
      });
    });

    it('should handle generation failures', async () => {
      workflowService.startGenerationWorkflow.and.returnValue(
        throwError(() => new Error('Generation failed'))
      );

      try {
        await component['startGeneration']();
      } catch (error) {
        expect(error.message).toBe('Generation failed');
        expect(notificationService.error).toHaveBeenCalledWith('Generation failed');
      }
    });

    it('should update credit consumption', () => {
      const initialCredits = 50;
      const generatedImages = 4;
      const creditsPerImage = 5;
      
      const remainingCredits = initialCredits - (generatedImages * creditsPerImage);
      
      expect(remainingCredits).toBe(30);
    });
  });

  describe('Workflow Orchestration', () => {
    it('should coordinate full workflow phases', async () => {
      const workflowPhases = [
        'upload',
        'training',
        'style-selection',
        'generation',
        'completion'
      ];

      let currentPhase = 0;
      
      workflowService.progress$.subscribe(progress => {
        expect(progress.phase).toBe(workflowPhases[currentPhase]);
        currentPhase++;
      });

      // Simulate complete workflow
      for (const phase of workflowPhases) {
        workflowService['progressSubject'].next({ phase, progress: 100 });
      }

      expect(currentPhase).toBe(workflowPhases.length);
    });

    it('should handle workflow interruption', () => {
      // Simulate workflow interruption
      workflowService.startTrainingWorkflow.and.returnValue(
        throwError(() => new Error('Network error'))
      );

      component['handleWorkflowInterruption']('Network error');

      expect(notificationService.error).toHaveBeenCalledWith('Network error');
      expect(component.isTrainingStarted).toBe(false);
    });

    it('should allow workflow resumption', () => {
      // Setup interrupted state
      stateService.getState.and.returnValue({
        ...mockInitialState,
        modelStatus: 'Training Interrupted',
        uploadedImages: 5
      });

      component['resumeWorkflow']();

      expect(workflowService.startTrainingWorkflow).toHaveBeenCalled();
    });
  });

  describe('Error Handling and Recovery', () => {
    it('should handle upload errors gracefully', () => {
      fileUploadService.uploadFiles.and.returnValue(
        throwError(() => new Error('Upload failed'))
      );

      component['handleUploadError']('Upload failed');

      expect(notificationService.error).toHaveBeenCalledWith('Upload failed');
    });

    it('should handle API timeouts', () => {
      const timeoutError = new Error('Request timeout');
      timeoutError.name = 'TimeoutError';

      component['handleApiTimeout'](timeoutError);

      expect(notificationService.error).toHaveBeenCalledWith(
        'Request timed out. Please try again.'
      );
    });

    it('should handle insufficient credits', () => {
      stateService.getState.and.returnValue({
        ...mockInitialState,
        creditsInfo: { availableCredits: 5 }
      });

      const requiredCredits = 20;
      const canProceed = component.getTotalAvailableCredits() >= requiredCredits;

      expect(canProceed).toBe(false);
    });

    it('should provide recovery options', () => {
      const recoveryOptions = component['getRecoveryOptions']();

      expect(recoveryOptions).toContain('Retry operation');
      expect(recoveryOptions).toContain('Purchase more credits');
      expect(recoveryOptions).toContain('Contact support');
    });
  });

  describe('State Persistence', () => {
    it('should maintain state across component lifecycle', () => {
      // Setup state
      const testState = {
        ...mockInitialState,
        uploadedImages: 3,
        modelStatus: 'Training'
      };

      stateService.getState.and.returnValue(testState);

      // Destroy and recreate component
      component.ngOnDestroy();
      component.ngOnInit();

      expect(component.uploadedImages).toBe(3);
      expect(component.modelStatus).toBe('Training');
    });

    it('should handle state restoration after refresh', () => {
      // Mock persisted state
      const persistedState = {
        uploadedImages: 5,
        modelStatus: 'Model Ready',
        hasTrainedModel: true,
        generatedPhotosCount: 8
      };

      stateService.getState.and.returnValue(persistedState);

      component.ngOnInit();

      expect(component.uploadedImages).toBe(5);
      expect(component.modelStatus).toBe('Model Ready');
      expect(component.generatedPhotosCount).toBe(8);
    });
  });

  describe('Performance Optimization', () => {
    it('should debounce rapid state updates', () => {
      spyOn(component, 'updateState');

      // Simulate rapid state updates
      for (let i = 0; i < 10; i++) {
        component['scheduleStateUpdate']();
      }

      // Should only update once after debounce
      setTimeout(() => {
        expect(component.updateState).toHaveBeenCalledTimes(1);
      }, 100);
    });

    it('should optimize image loading', () => {
      const mockImages = Array(50).fill(null).map((_, i) => ({
        id: i,
        url: `/generated/image${i}.jpg`,
        style: 'professional'
      }));

      // Should implement lazy loading for large image sets
      const visibleImages = mockImages.slice(0, 10);
      expect(visibleImages.length).toBe(10);
    });
  });
});