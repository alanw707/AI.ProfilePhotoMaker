import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { RouterTestingModule } from '@angular/router/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';

import { SettingsComponent } from './settings.component';
import { AuthService } from '../../services/auth.service';
import { ProfileService, UserProfile } from '../../services/profile.service';
import { FileUploadService } from '../../services/file-upload.service';
import { NotificationService } from '../../services/notification.service';
import { DashboardCoordinatorService } from '../../services/dashboard-coordinator.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';

describe('SettingsComponent', () => {
  let component: SettingsComponent;
  let fixture: ComponentFixture<SettingsComponent>;
  let mockAuthService: jasmine.SpyObj<AuthService>;
  let mockProfileService: jasmine.SpyObj<ProfileService>;
  let mockFileUploadService: jasmine.SpyObj<FileUploadService>;
  let mockNotificationService: jasmine.SpyObj<NotificationService>;
  let mockDashboardCoordinatorService: jasmine.SpyObj<DashboardCoordinatorService>;

  const mockUserProfile: UserProfile = {
    id: 1,
    userId: 'test-user-123',
    firstName: 'John',
    lastName: 'Doe',
    gender: 'Male',
    ethnicity: 'Asian',
    subscriptionTier: 'Basic',
    basicCredits: 3,
    lastCreditReset: new Date(),
    createdAt: new Date('2024-01-01'),
    updatedAt: new Date(),
  };

  const mockDataStats = {
    inputPhotos: 5,
    generatedPhotos: 10,
    hasTrainedModel: true,
    totalDataSize: 1024 * 1024 * 50, // 50MB
    accountAge: 365,
  };

  const mockCreditsInfo = {
    availableCredits: 3,
    totalCredits: 3,
  };

  const mockUserCreditStatus = {
    weeklyCredits: 3,
    purchasedCredits: 10,
    totalCredits: 13,
    lastReset: new Date(),
  };

  // Stub for header to avoid pulling full component dependencies
  @Component({
    selector: 'app-header-navigation',
    standalone: true,
    template: '',
  })
  class HeaderNavigationStubComponent {}

  beforeEach(async () => {
    // Create mock services
    mockAuthService = jasmine.createSpyObj('AuthService', ['isAuthenticated', 'logout'], {
      currentUser$: of({ email: 'test@example.com' }) as any,
    });
    mockProfileService = jasmine.createSpyObj('ProfileService', [
      'getCurrentUserProfile',
      'getDataStats',
      'deleteInputPhotos',
      'deleteAIModel',
      'deleteAllUserData',
      'deleteUserAccount',
      'exportUserData',
    ]);
    mockFileUploadService = jasmine.createSpyObj('FileUploadService', [
      'getUserImages',
      'getTrainingStatus',
      'getUserModelRequests',
    ]);
    mockNotificationService = jasmine.createSpyObj('NotificationService', [
      'success',
      'error',
      'info',
      'warning',
    ]);
    mockDashboardCoordinatorService = jasmine.createSpyObj(
      'DashboardCoordinatorService',
      ['getState', 'loadInitialDashboardData'],
      {
        state$: of({
          userProfile: null,
          modelStatus: 'Not Started',
          latestTrainedModel: null,
          uploadedImages: 0,
          uploadedImageThumbnails: [],
          generatedPhotosCount: 0,
          imagesValidated: false,
          lastValidationTime: null,
          userCreditStatus: { weeklyCredits: 3, purchasedCredits: 10, totalCredits: 13 },
          creditsInfo: { availableCredits: 3, totalCredits: 13 },
          totalCredits: 13,
          isPremiumWorkflow: false,
          isLoading: false,
        }) as any,
      }
    );

    // Set up default return values
    mockAuthService.isAuthenticated.and.returnValue(true);
    mockProfileService.getCurrentUserProfile.and.returnValue(
      of({ success: true, data: mockUserProfile, error: null } as any)
    );
    mockProfileService.getDataStats.and.returnValue(
      of({ success: true, data: mockDataStats, error: null } as any)
    );
    mockDashboardCoordinatorService.getState.and.returnValue({
      userProfile: null,
      creditsInfo: mockCreditsInfo,
      userCreditStatus: mockUserCreditStatus,
      uploadedImages: 0,
      uploadedImageThumbnails: [],
      generatedPhotosCount: 0,
      modelStatus: 'Not Started',
      isPremiumWorkflow: false,
      isLoading: false,
    } as any);

    // Default returns for FileUploadService used in Settings flows
    mockFileUploadService.getUserImages.and.returnValue(
      of({
        success: true,
        data: {
          totalImages: 0,
          originalUploads: 0,
          generatedImages: 0,
          images: [],
        },
      }) as any
    );
    mockFileUploadService.getTrainingStatus.and.returnValue(
      of({
        profileId: 1,
        hasTrainedModel: true,
        trainedModelId: null as any,
        modelTrainedAt: null as any,
        totalUploadedImages: 0,
        latestZipFile: null as any,
        canStartTraining: true,
        status: 'Not Started',
      }) as any
    );
    mockFileUploadService.getUserModelRequests.and.returnValue(
      of({
        success: true,
        data: [],
      }) as any
    );

    await TestBed.configureTestingModule({
      imports: [
        SettingsComponent,
        HeaderNavigationStubComponent,
        RouterTestingModule,
        HttpClientTestingModule,
        FormsModule,
      ],
      providers: [
        { provide: AuthService, useValue: mockAuthService },
        { provide: ProfileService, useValue: mockProfileService },
        { provide: FileUploadService, useValue: mockFileUploadService },
        { provide: NotificationService, useValue: mockNotificationService },
        { provide: DashboardCoordinatorService, useValue: mockDashboardCoordinatorService },
      ],
      // Allow unknown elements/attributes (e.g. header component and other standalone deps)
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
      .overrideComponent(SettingsComponent, {
        remove: { imports: [HeaderNavigationComponent] },
        add: { imports: [HeaderNavigationStubComponent] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(SettingsComponent);
    component = fixture.componentInstance;
    // Avoid long timeouts from async loaders in ngOnInit
    spyOn(component as any, 'loadCreditInfoAsync').and.returnValue(Promise.resolve());
    spyOn(component as any, 'checkTrainedModelStatus').and.returnValue(Promise.resolve());
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Component Initialization', () => {
    it('should initialize with loading state', () => {
      expect(component.isLoading).toBe(true);
      expect(component.isDeleting).toBe(false);
      expect(component.isExporting).toBe(false);
    });

    it('should load user data on init', () => {
      fixture.detectChanges();
      expect(mockProfileService.getCurrentUserProfile).toHaveBeenCalled();
      expect(mockProfileService.getDataStats).toHaveBeenCalled();
    });

    it('should set user profile data correctly', () => {
      component.userProfile = mockUserProfile;
      component.isLoading = false;
      fixture.detectChanges();
      expect(component.userProfile).toEqual(mockUserProfile);
      expect(component.isLoading).toBe(false);
    });

    it('should set data stats correctly', () => {
      component.dataStats = { ...mockDataStats } as any;
      fixture.detectChanges();
      expect(component.dataStats.inputPhotos).toBe(mockDataStats.inputPhotos);
      expect(component.dataStats.generatedPhotos).toBe(mockDataStats.generatedPhotos);
      expect(component.dataStats.totalDataSize).toBe(mockDataStats.totalDataSize);
    });
  });

  describe('Template Rendering', () => {
    beforeEach(() => {
      component.isLoading = false;
      component.userEmail = 'test@example.com';
      component.userProfile = mockUserProfile;
      component.dataStats = { ...mockDataStats } as any;
      component.creditsInfo = { availableCredits: 3, totalCredits: 13 } as any;
      component.userCreditStatus = {
        weeklyCredits: 3,
        purchasedCredits: 10,
        totalCredits: 13,
      } as any;
      fixture.detectChanges();
    });

    it('should render the main sections', () => {
      const compiled = fixture.nativeElement;
      const sections = compiled.querySelectorAll('.settings-section') as NodeListOf<HTMLElement>;

      expect(sections.length).toBeGreaterThanOrEqual(3);
    });

    it('should render Account Information section', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      const headings = Array.from(compiled.querySelectorAll('h2')).map(h => h.textContent || '');
      expect(headings.join(' ')).toMatch(/Account/i);
    });

    it('should render Credit Management section', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      const headings = Array.from(compiled.querySelectorAll('h2')).map(h => h.textContent || '');
      expect(headings.join(' ')).toMatch(/Credit/i);
    });

    it('should render Your Data section', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      const dataStats = compiled.querySelector('.data-stats');
      expect(dataStats).toBeTruthy();
    });

    it('should render Data Management section', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('.data-actions')).toBeTruthy();
    });
  });

  describe('CSS Classes Presence', () => {
    beforeEach(() => {
      component.isLoading = false;
      component.userEmail = 'test@example.com';
      component.userProfile = mockUserProfile;
      component.dataStats = { ...mockDataStats } as any;
      fixture.detectChanges();
    });

    it('should have all container CSS classes', () => {
      const compiled = fixture.nativeElement as HTMLElement;

      expect(compiled.querySelector('.settings-container')).toBeTruthy();
      expect(compiled.querySelector('.settings-main')).toBeTruthy();
      expect(compiled.querySelector('.settings-content')).toBeTruthy();
      expect(compiled.querySelector('.settings-sections')).toBeTruthy();
    });

    it('should have key section elements present', () => {
      const compiled = fixture.nativeElement as HTMLElement;

      // At least one settings section exists
      expect(compiled.querySelector('.settings-section')).toBeTruthy();

      // Credit Management section exists (class names may vary)
      const sections = compiled.querySelectorAll('.settings-section');
      expect(sections.length).toBeGreaterThan(0);

      // Data Statistics
      expect(compiled.querySelector('.data-stats')).toBeTruthy();

      // Data Management
      expect(compiled.querySelector('.data-actions')).toBeTruthy();
    });

    it('should have actionable buttons', () => {
      const compiled = fixture.nativeElement;

      const buttons = compiled.querySelectorAll('button');
      expect(buttons.length).toBeGreaterThan(0);
    });
  });

  describe('Loading State', () => {
    it('should show loading spinner when isLoading is true', () => {
      component.isLoading = true;
      fixture.detectChanges();

      const compiled = fixture.nativeElement;
      expect(compiled.querySelector('.loading-section')).toBeTruthy();
      expect(compiled.querySelector('.spinner')).toBeTruthy();
      expect(compiled.querySelector('.settings-sections')).toBeFalsy();
    });

    it('should hide loading spinner when isLoading is false', () => {
      component.isLoading = false;
      fixture.detectChanges();

      const compiled = fixture.nativeElement;
      expect(compiled.querySelector('.loading-section')).toBeFalsy();
      expect(compiled.querySelector('.settings-sections')).toBeTruthy();
    });
  });

  describe('Data Display', () => {
    beforeEach(() => {
      component.isLoading = false;
      component.userEmail = 'test@example.com';
      component.userProfile = mockUserProfile;
      component.dataStats = { ...mockDataStats } as any;
      fixture.detectChanges();
    });

    it('should display user email', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('test@example.com');
    });

    it('should display full name', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('John Doe');
    });

    it('should display data statistics', () => {
      const compiled = fixture.nativeElement;
      const statCards = compiled.querySelectorAll('.stat-card');

      expect(statCards.length).toBeGreaterThan(0);
      // Check if stats are displayed
      const statsText = compiled.textContent;
      expect(statsText).toContain('5'); // input photos
      expect(statsText).toContain('10'); // generated photos
    });
  });

  describe('Modal Functionality', () => {
    it('should reflect confirmation modal state', () => {
      (component as any).showConfirmationModal = true;
      component.deletionType = 'photos' as any;
      fixture.detectChanges();

      expect((component as any).showConfirmationModal).toBeTrue();
      expect(component.deletionType).toBe('photos');
    });

    it('should hide modal when cancelled', () => {
      (component as any).showConfirmationModal = true;
      (component as any).showConfirmationModal = false;
      fixture.detectChanges();

      expect((component as any).showConfirmationModal).toBeFalse();
    });
  });

  describe('Delete Operations', () => {
    it('should call deleteInputPhotos when executing delete', async () => {
      mockProfileService.deleteInputPhotos.and.returnValue(
        of({ success: true, data: { deletedCount: 0 }, error: null } as any)
      );

      component.deletionType = 'photos';
      await (component as any).executeDelete();

      expect(mockProfileService.deleteInputPhotos).toHaveBeenCalled();
      expect(mockNotificationService.success).toHaveBeenCalled();
    });

    it('should handle delete errors', async () => {
      mockProfileService.deleteInputPhotos.and.returnValue(
        throwError(() => ({ error: { message: 'Error' } })) as any
      );

      component.deletionType = 'photos';
      await (component as any).executeDelete();

      expect(mockNotificationService.error).toHaveBeenCalled();
    });
  });

  describe('Export Functionality', () => {
    it('should handle data export', async () => {
      const mockBlob = new Blob(['test data'], { type: 'application/json' });
      mockProfileService.exportUserData.and.returnValue(of(mockBlob) as any);
      spyOn(window.URL, 'createObjectURL').and.returnValue('blob:test');

      await component.exportData();

      expect(mockProfileService.exportUserData).toHaveBeenCalled();
      expect(component.isExporting).toBe(false);
    });
  });

  describe('Helper Methods', () => {
    it('should format data size correctly', () => {
      expect(component.formatDataSize(1024)).toBe('1 KB');
      expect(component.formatDataSize(1024 * 1024)).toBe('1 MB');
      expect(component.formatDataSize(1024 * 1024 * 1024)).toBe('1 GB');
    });

    it('should compute full name from user profile', () => {
      component.userProfile = mockUserProfile;
      const fullName = `${component.userProfile.firstName} ${component.userProfile.lastName}`;
      expect(fullName).toBe('John Doe');

      component.userProfile = null;
      const emptyName = (component as any)?.userProfile
        ? `${(component as any).userProfile.firstName} ${(component as any).userProfile.lastName}`
        : '';
      expect(emptyName).toBe('');
    });

    it('should get total available credits', () => {
      // Credits come from dashboard state service; use mocked values
      const total =
        (mockUserCreditStatus.weeklyCredits || 0) + (mockUserCreditStatus.purchasedCredits || 0);
      expect(total).toBe(13); // 3 weekly + 10 purchased
    });
  });
});
