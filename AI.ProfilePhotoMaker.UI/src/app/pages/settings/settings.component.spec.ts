import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';

import { SettingsComponent } from './settings.component';
import { AuthService } from '../../services/auth.service';
import { ProfileService, UserProfile } from '../../services/profile.service';
import { FileUploadService } from '../../services/file-upload.service';
import { NotificationService } from '../../services/notification.service';
import { DashboardStateService } from '../../services/dashboard-state.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';

describe('SettingsComponent', () => {
  let component: SettingsComponent;
  let fixture: ComponentFixture<SettingsComponent>;
  let mockAuthService: jasmine.SpyObj<AuthService>;
  let mockProfileService: jasmine.SpyObj<ProfileService>;
  let mockFileUploadService: jasmine.SpyObj<FileUploadService>;
  let mockNotificationService: jasmine.SpyObj<NotificationService>;
  let mockDashboardStateService: jasmine.SpyObj<DashboardStateService>;

  const mockUserProfile: UserProfile = {
    id: 1,
    userId: 'test-user-123',
    firstName: 'John',
    lastName: 'Doe',
    gender: 'Male',
    ethnicity: 'Asian',
    subscriptionTier: 'Basic',
    credits: 3,
    lastCreditReset: new Date(),
    profileImageUrl: '',
    createdAt: new Date('2024-01-01'),
    updatedAt: new Date()
  };

  const mockDataStats = {
    inputPhotos: 5,
    generatedPhotos: 10,
    enhancedPhotos: 3,
    hasTrainedModel: true,
    totalDataSize: 1024 * 1024 * 50, // 50MB
    accountAge: 365
  };

  const mockCreditsInfo = {
    availableCredits: 3,
    totalCredits: 3
  };

  const mockUserCreditStatus = {
    weeklyCredits: 3,
    purchasedCredits: 10,
    totalCredits: 13,
    lastReset: new Date()
  };

  beforeEach(async () => {
    // Create mock services
    mockAuthService = jasmine.createSpyObj('AuthService', ['getCurrentUser', 'logout']);
    mockProfileService = jasmine.createSpyObj('ProfileService', ['getProfile', 'getDataStats', 'deleteInputPhotos', 'deleteAIModel', 'deleteAllData', 'deleteAccount', 'exportData']);
    mockFileUploadService = jasmine.createSpyObj('FileUploadService', ['someMethod']);
    mockNotificationService = jasmine.createSpyObj('NotificationService', ['success', 'error', 'info']);
    mockDashboardStateService = jasmine.createSpyObj('DashboardStateService', ['getState']);

    // Set up default return values
    mockAuthService.getCurrentUser.and.returnValue({ email: 'test@example.com' });
    mockProfileService.getProfile.and.returnValue(of({ success: true, data: mockUserProfile }));
    mockProfileService.getDataStats.and.returnValue(of({ success: true, data: mockDataStats }));
    mockDashboardStateService.getState.and.returnValue({
      creditsInfo: mockCreditsInfo,
      userCreditStatus: mockUserCreditStatus
    });

    await TestBed.configureTestingModule({
      imports: [
        SettingsComponent,
        RouterTestingModule,
        HttpClientTestingModule,
        FormsModule
      ],
      providers: [
        { provide: AuthService, useValue: mockAuthService },
        { provide: ProfileService, useValue: mockProfileService },
        { provide: FileUploadService, useValue: mockFileUploadService },
        { provide: NotificationService, useValue: mockNotificationService },
        { provide: DashboardStateService, useValue: mockDashboardStateService }
      ]
    })
    .overrideComponent(SettingsComponent, {
      remove: { imports: [HeaderNavigationComponent] },
      add: { imports: [] }
    })
    .compileComponents();

    fixture = TestBed.createComponent(SettingsComponent);
    component = fixture.componentInstance;
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
      
      expect(mockProfileService.getProfile).toHaveBeenCalled();
      expect(mockProfileService.getDataStats).toHaveBeenCalled();
      expect(component.userEmail).toBe('test@example.com');
    });

    it('should set user profile data correctly', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      expect(component.userProfile).toEqual(mockUserProfile);
      expect(component.isLoading).toBe(false);
    });

    it('should set data stats correctly', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      expect(component.dataStats).toEqual(mockDataStats);
    });
  });

  describe('Template Rendering', () => {
    beforeEach(async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    });

    it('should render all 4 main sections', () => {
      const compiled = fixture.nativeElement;
      const sections = compiled.querySelectorAll('.settings-section');
      
      expect(sections.length).toBe(4);
    });

    it('should render Account Information section', () => {
      const compiled = fixture.nativeElement;
      const accountSection = compiled.querySelector('.settings-section:first-child');
      
      expect(accountSection.querySelector('h2')?.textContent).toContain('Account Information');
      expect(accountSection.querySelector('.info-grid')).toBeTruthy();
    });

    it('should render Credit Management section', () => {
      const compiled = fixture.nativeElement;
      const sections = compiled.querySelectorAll('.settings-section');
      const creditSection = sections[1];
      
      expect(creditSection.querySelector('h2')?.textContent).toContain('Credit Management');
      expect(creditSection.querySelector('.credit-overview')).toBeTruthy();
    });

    it('should render Your Data section', () => {
      const compiled = fixture.nativeElement;
      const sections = compiled.querySelectorAll('.settings-section');
      const dataSection = sections[2];
      
      expect(dataSection.querySelector('h2')?.textContent).toContain('Your Data');
      expect(dataSection.querySelector('.data-stats')).toBeTruthy();
    });

    it('should render Data Management section', () => {
      const compiled = fixture.nativeElement;
      const sections = compiled.querySelectorAll('.settings-section');
      const managementSection = sections[3];
      
      expect(managementSection.querySelector('h2')?.textContent).toContain('Data Management');
      expect(managementSection.querySelector('.data-actions')).toBeTruthy();
    });
  });

  describe('CSS Classes Presence', () => {
    beforeEach(async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    });

    it('should have all container CSS classes', () => {
      const compiled = fixture.nativeElement;
      
      expect(compiled.querySelector('.settings-container')).toBeTruthy();
      expect(compiled.querySelector('.settings-main')).toBeTruthy();
      expect(compiled.querySelector('.settings-content')).toBeTruthy();
      expect(compiled.querySelector('.settings-sections')).toBeTruthy();
    });

    it('should have all section-specific CSS classes', () => {
      const compiled = fixture.nativeElement;
      
      // Account Information
      expect(compiled.querySelector('.info-grid')).toBeTruthy();
      expect(compiled.querySelector('.info-item')).toBeTruthy();
      
      // Credit Management
      expect(compiled.querySelector('.credit-overview')).toBeTruthy();
      expect(compiled.querySelector('.credit-card')).toBeTruthy();
      
      // Data Statistics
      expect(compiled.querySelector('.data-stats')).toBeTruthy();
      expect(compiled.querySelector('.stat-card')).toBeTruthy();
      
      // Data Management
      expect(compiled.querySelector('.data-actions')).toBeTruthy();
      expect(compiled.querySelector('.action-item')).toBeTruthy();
    });

    it('should have button CSS classes', () => {
      const compiled = fixture.nativeElement;
      
      expect(compiled.querySelector('.btn')).toBeTruthy();
      expect(compiled.querySelector('.btn-primary')).toBeTruthy();
      expect(compiled.querySelector('.btn-secondary')).toBeTruthy();
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
    beforeEach(async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    });

    it('should display user email', () => {
      const compiled = fixture.nativeElement;
      const emailElement = compiled.querySelector('.info-item span');
      
      expect(emailElement?.textContent).toContain('test@example.com');
    });

    it('should display full name', () => {
      const compiled = fixture.nativeElement;
      const infoItems = compiled.querySelectorAll('.info-item');
      const nameItem = Array.from(infoItems).find(item => 
        item.querySelector('label')?.textContent === 'Full Name'
      );
      
      expect(nameItem?.querySelector('span')?.textContent).toContain('John Doe');
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
    it('should show confirmation modal when delete action is triggered', () => {
      component.showConfirmationModal('photos');
      fixture.detectChanges();
      
      expect(component.showModal).toBe(true);
      expect(component.deletionType).toBe('photos');
      expect(component.confirmationText).toBe('');
    });

    it('should hide modal when cancelled', () => {
      component.showModal = true;
      component.hideModal();
      fixture.detectChanges();
      
      expect(component.showModal).toBe(false);
      expect(component.confirmationText).toBe('');
    });
  });

  describe('Delete Operations', () => {
    it('should call deleteInputPhotos when confirmed', async () => {
      mockProfileService.deleteInputPhotos.and.returnValue(of({ success: true }));
      
      component.deletionType = 'photos';
      await component.confirmDelete();
      
      expect(mockProfileService.deleteInputPhotos).toHaveBeenCalled();
      expect(mockNotificationService.success).toHaveBeenCalled();
    });

    it('should handle delete errors', async () => {
      mockProfileService.deleteInputPhotos.and.returnValue(throwError({ error: { message: 'Error' } }));
      
      component.deletionType = 'photos';
      await component.confirmDelete();
      
      expect(mockNotificationService.error).toHaveBeenCalled();
    });
  });

  describe('Export Functionality', () => {
    it('should handle data export', async () => {
      const mockBlob = new Blob(['test data'], { type: 'application/json' });
      mockProfileService.exportData.and.returnValue(of(mockBlob));
      spyOn(window.URL, 'createObjectURL').and.returnValue('blob:test');
      
      await component.exportData();
      
      expect(mockProfileService.exportData).toHaveBeenCalled();
      expect(component.isExporting).toBe(false);
    });
  });

  describe('Helper Methods', () => {
    it('should format data size correctly', () => {
      expect(component.formatDataSize(1024)).toBe('1.0 KB');
      expect(component.formatDataSize(1024 * 1024)).toBe('1.0 MB');
      expect(component.formatDataSize(1024 * 1024 * 1024)).toBe('1.0 GB');
    });

    it('should get full name correctly', () => {
      component.userProfile = mockUserProfile;
      expect(component.getFullName()).toBe('John Doe');
      
      component.userProfile = null;
      expect(component.getFullName()).toBe('');
    });

    it('should get total available credits', () => {
      const total = component.getTotalAvailableCredits();
      expect(total).toBe(13); // 3 weekly + 10 purchased
    });
  });
});