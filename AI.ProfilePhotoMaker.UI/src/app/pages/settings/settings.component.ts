import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { ProfileService, UserProfile } from '../../services/profile.service';
import { FileUploadService } from '../../services/file-upload.service';
import { NotificationService } from '../../services/notification.service';
import { DashboardStateService } from '../../services/dashboard-state.service';
import { AccountInfoComponent } from '../../components/settings/account-info/account-info.component';
import { CreditManagementComponent } from '../../components/settings/credit-management/credit-management.component';

interface DataStats {
  inputPhotos: number;
  generatedPhotos: number;
  hasTrainedModel: boolean;
  totalDataSize: number;
  accountAge: number;
}

type DeletionType = 'photos' | 'model' | 'all' | 'account';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    HeaderNavigationComponent,
    AccountInfoComponent,
    CreditManagementComponent,
  ],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.sass'],
})
export class SettingsComponent implements OnInit {
  // Constants
  readonly MAX_PHOTOS_LIMIT = 200;

  // User Info
  userProfile: UserProfile | null = null;
  userEmail = '';

  // Loading States
  isLoading = true;
  isDeleting = false;
  isExporting = false;

  // Data Statistics
  dataStats: DataStats = {
    inputPhotos: 0,
    generatedPhotos: 0,
    hasTrainedModel: false,
    totalDataSize: 0,
    accountAge: 0,
  };

  // Confirmation Modal State
  showConfirmationModal = false;
  deletionType: DeletionType = 'photos';
  confirmationText = '';
  confirmationTitle = '';
  confirmationMessage = '';

  // Credit Management State
  creditsInfo: any = null;
  userCreditStatus: any = null;

  constructor(
    private authService: AuthService,
    private router: Router,
    private profileService: ProfileService,
    private fileUploadService: FileUploadService,
    private notificationService: NotificationService,
    private dashboardStateService: DashboardStateService,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit() {
    console.log('Settings ngOnInit');

    // Check authentication first
    if (!this.authService.isAuthenticated()) {
      console.log('Not authenticated, redirecting to login');
      this.router.navigate(['/auth/login']);
      return;
    }

    console.log('User is authenticated, loading settings data');

    try {
      // Load all data in parallel and wait for completion
      // Add timeout and individual error handling to prevent infinite loading
      await Promise.allSettled([
        this.loadUserInfoAsync(),
        this.loadDataStats(),
        this.loadUserProfileAsync(),
        this.loadCreditInfoAsync(),
      ]);
    } catch (error) {
      console.error('Error loading settings data:', error);
      this.notificationService.warning(
        'Loading Warning',
        'Some settings data may not be available.'
      );
    } finally {
      this.isLoading = false;
      this.cdr.detectChanges(); // Force Angular to update UI
    }
  }

  loadUserInfo() {
    // Get user email from auth service
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userEmail = user.email;
      }
    });
  }

  loadUserProfile() {
    // Load user profile from API
    this.profileService.getCurrentUserProfile().subscribe({
      next: response => {
        if (response.success) {
          this.userProfile = response.data;

          // Calculate account age
          if (this.userProfile.createdAt) {
            const createdDate = new Date(this.userProfile.createdAt);
            const now = new Date();
            this.dataStats.accountAge = Math.floor(
              (now.getTime() - createdDate.getTime()) / (1000 * 60 * 60 * 24)
            );
          }
        } else {
          this.notificationService.error(
            'Profile Load Failed',
            'Failed to load user profile information.'
          );
        }
      },
      error: error => {
        console.error('Failed to load user profile:', error);

        // Email is already loaded from auth service

        this.notificationService.error(
          'Profile Load Failed',
          'Unable to connect to the server. Please check your connection and try again.'
        );
      },
    });
  }

  async loadDataStats() {
    try {
      // Load data stats from API
      const statsResponse = await this.profileService.getDataStats().toPromise();
      if (statsResponse?.success) {
        this.dataStats = {
          inputPhotos: statsResponse.data.inputPhotos || 0,
          generatedPhotos: statsResponse.data.generatedPhotos || 0,
          hasTrainedModel: statsResponse.data.hasTrainedModel || false,
          totalDataSize: statsResponse.data.totalDataSize || 0,
          accountAge: statsResponse.data.accountAge || 0,
        };

        // Even if primary API succeeded, verify trained model status using comprehensive check
        // This ensures accuracy since model status might be inconsistent across systems
        await this.checkTrainedModelStatus();
      } else {
        // Fallback to existing method if API is not available
        const imagesResponse = await this.fileUploadService.getUserImages().toPromise();
        if (imagesResponse?.success && imagesResponse.data) {
          const originalImages = imagesResponse.data.images.filter(img => !img.isGenerated);
          const generatedImages = imagesResponse.data.images.filter(img => img.isGenerated);

          this.dataStats.inputPhotos = originalImages.length;
          this.dataStats.generatedPhotos = generatedImages.length;

          // Calculate total data size from original images (use fileSizeBytes or estimate)
          this.dataStats.totalDataSize = originalImages.reduce((total, img) => {
            // Try different size properties that might exist
            const size =
              (img as any).fileSizeBytes || (img as any).fileSize || (img as any).size || 0;
            return total + size;
          }, 0);
        }

        // Check if user has trained model using multiple data sources for accuracy
        await this.checkTrainedModelStatus();
      }
    } catch (error) {
      console.error('Error loading data stats:', error);
      this.notificationService.warning(
        'Data Load Warning',
        'Some data statistics may not be available.'
      );
    }
  }

  // Helper Methods (getFullName and formatDate moved to account-info.component.ts)

  // Navigation Methods
  editProfile() {
    // For now, just show a notification. In a full implementation, this would open an edit modal
    this.notificationService.info(
      'Feature Coming Soon',
      'Profile editing will be available in a future update.'
    );
  }

  // Data Management Methods
  confirmDeleteData(type: DeletionType) {
    this.deletionType = type;
    this.confirmationText = '';

    switch (type) {
      case 'photos':
        this.confirmationTitle = 'Delete Input Photos';
        this.confirmationMessage = `Are you sure you want to delete all ${this.dataStats.inputPhotos} input photos? This action cannot be undone.`;
        break;
      case 'model':
        this.confirmationTitle = 'Delete AI Model';
        this.confirmationMessage =
          'Are you sure you want to delete your trained AI model? You will need to re-upload photos and retrain to generate new styled photos.';
        break;
      case 'all':
        this.confirmationTitle = 'Delete All Data';
        this.confirmationMessage =
          'Are you sure you want to permanently delete ALL your data? This includes all photos, AI models, and usage history. This action cannot be undone.';
        break;
      case 'account':
        this.confirmationTitle = 'Delete Account';
        this.confirmationMessage =
          'Are you sure you want to permanently delete your entire account? This will close your account, delete all data, and log you out immediately. This action cannot be undone.';
        break;
    }

    this.showConfirmationModal = true;
  }

  cancelDelete() {
    this.showConfirmationModal = false;
    this.confirmationText = '';
  }

  onConfirmationTextChange() {
    // Method to handle confirmation text changes for real-time validation
  }

  canConfirmDelete(): boolean {
    if (this.deletionType === 'all' || this.deletionType === 'account') {
      return this.confirmationText.toUpperCase() === 'DELETE';
    }
    return true; // For photos and model deletion, no confirmation text required
  }

  getDeleteButtonText(): string {
    switch (this.deletionType) {
      case 'photos':
        return 'Delete Photos';
      case 'model':
        return 'Delete Model';
      case 'all':
        return 'Delete All Data';
      case 'account':
        return 'Delete Account';
      default:
        return 'Delete';
    }
  }

  async executeDelete() {
    if (!this.canConfirmDelete()) {
      return;
    }

    this.isDeleting = true;

    try {
      switch (this.deletionType) {
        case 'photos':
          await this.deleteInputPhotos();
          break;
        case 'model':
          await this.deleteAIModel();
          break;
        case 'all':
          await this.deleteAllData();
          break;
        case 'account':
          await this.deleteAccount();
          break;
      }
    } catch (error) {
      console.error('Delete operation failed:', error);
      this.notificationService.error(
        'Delete Failed',
        'The delete operation failed. Please try again.'
      );
    } finally {
      this.isDeleting = false;
      this.showConfirmationModal = false;
    }
  }

  private async deleteInputPhotos() {
    try {
      const response = await this.profileService.deleteInputPhotos().toPromise();
      if (response?.success) {
        this.notificationService.success(
          'Photos Deleted',
          `Successfully deleted ${response.data.deletedCount} input photos.`
        );
        this.dataStats.inputPhotos = 0;
        await this.loadDataStats(); // Refresh stats
      } else {
        throw new Error(response?.error?.message || 'Failed to delete photos');
      }
    } catch (error) {
      console.error('Error deleting photos:', error);
      throw error;
    }
  }

  private async deleteAIModel() {
    try {
      const response = await this.profileService.deleteAIModel().toPromise();
      if (response?.success) {
        this.notificationService.success(
          'AI Model Deleted',
          response.data.message || 'Your trained AI model has been successfully deleted.'
        );
        this.dataStats.hasTrainedModel = false;
        if (this.userProfile) {
          this.userProfile.trainedModelId = undefined;
          this.userProfile.trainedModelVersionId = undefined;
        }
        await this.loadDataStats(); // Refresh stats
      } else {
        throw new Error(response?.error?.message || 'Failed to delete AI model');
      }
    } catch (error) {
      console.error('Error deleting AI model:', error);
      throw error;
    }
  }

  private async deleteAllData() {
    try {
      const response = await this.profileService.deleteAllUserData().toPromise();
      if (response?.success) {
        this.notificationService.success(
          'All Data Deleted',
          response.data.message || 'All your data has been successfully deleted.'
        );
        // Reset all stats
        this.dataStats = {
          inputPhotos: 0,
          generatedPhotos: 0,
          hasTrainedModel: false,
          totalDataSize: 0,
          accountAge: this.dataStats.accountAge,
        };
        await this.loadDataStats(); // Refresh stats
      } else {
        throw new Error(response?.error?.message || 'Failed to delete all data');
      }
    } catch (error) {
      console.error('Error deleting all data:', error);
      throw error;
    }
  }

  private async deleteAccount() {
    try {
      const response = await this.profileService.deleteUserAccount().toPromise();
      if (response?.success) {
        this.notificationService.success(
          'Account Deleted',
          'Your account has been successfully deleted. You will be logged out.'
        );
        // Log out user immediately
        setTimeout(() => {
          this.authService.logout();
          this.router.navigate(['/auth/login']);
        }, 2000);
      } else {
        throw new Error(response?.error?.message || 'Failed to delete account');
      }
    } catch (error) {
      console.error('Error deleting account:', error);
      throw error;
    }
  }

  async exportData() {
    this.isExporting = true;

    try {
      const blob = await this.profileService.exportUserData().toPromise();
      if (blob) {
        // Create download link
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;

        // Generate filename with current date
        const now = new Date();
        const dateStr = now.toISOString().split('T')[0]; // YYYY-MM-DD
        link.download = `profile-data-export-${dateStr}.json`;

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        this.notificationService.success(
          'Export Complete',
          'Your data export has been generated and downloaded.'
        );
      } else {
        throw new Error('No data received from export');
      }
    } catch (error) {
      console.error('Export failed:', error);
      this.notificationService.error(
        'Export Failed',
        'Failed to export your data. Please try again.'
      );
    } finally {
      this.isExporting = false;
    }
  }

  // Credit Management Methods
  loadCreditInfo() {
    // Subscribe to dashboard state for credit information
    this.dashboardStateService.state$.subscribe(state => {
      this.creditsInfo = state.creditsInfo;
      this.userCreditStatus = state.userCreditStatus;
    });

    // Load initial credit data
    this.dashboardStateService.loadInitialDashboardData();
  }

  // Async versions for proper loading state management
  async loadUserInfoAsync(): Promise<void> {
    return new Promise(resolve => {
      // Get user email from auth service - take first emission and unsubscribe
      const timeout = setTimeout(() => {
        console.warn('loadUserInfoAsync timed out');
        resolve();
      }, 5000);

      let subscription: any;
      subscription = this.authService.currentUser$.subscribe(user => {
        clearTimeout(timeout);
        if (user) {
          this.userEmail = user.email;
        }
        if (subscription) {
          subscription.unsubscribe();
        }
        resolve();
      });
    });
  }

  async loadUserProfileAsync(): Promise<void> {
    try {
      // Add 10 second timeout
      const timeoutPromise = new Promise((_, reject) =>
        setTimeout(() => reject(new Error('Profile load timeout')), 10000)
      );

      const response = (await Promise.race([
        this.profileService.getCurrentUserProfile().toPromise(),
        timeoutPromise,
      ])) as any;

      if (response?.success) {
        this.userProfile = response.data;

        // Calculate account age
        if (this.userProfile?.createdAt) {
          const createdDate = new Date(this.userProfile.createdAt);
          const now = new Date();
          this.dataStats.accountAge = Math.floor(
            (now.getTime() - createdDate.getTime()) / (1000 * 60 * 60 * 24)
          );
        }
      } else {
        console.warn('Profile load failed - response:', response);
      }
    } catch (error) {
      console.error('Failed to load user profile:', error);
      // Don't show error notification here - let parent handle it
    }
  }

  async loadCreditInfoAsync(): Promise<void> {
    return new Promise(resolve => {
      // Add timeout for credit loading
      const timeout = setTimeout(() => {
        console.warn('loadCreditInfoAsync timed out');
        resolve();
      }, 8000);

      // Load basic data for settings (no validation, just counts)
      this.dashboardStateService.loadBasicDataForSettings();

      // Subscribe to dashboard state for credit information - take first emission
      let subscription: any;
      subscription = this.dashboardStateService.state$.subscribe(state => {
        clearTimeout(timeout);
        this.creditsInfo = state.creditsInfo;
        this.userCreditStatus = state.userCreditStatus;
        // Also update data stats with the lighter counts
        if (state.uploadedImages !== undefined) {
          this.dataStats.inputPhotos = state.uploadedImages;
        }
        if (state.generatedPhotosCount !== undefined) {
          this.dataStats.generatedPhotos = state.generatedPhotosCount;
        }
        if (subscription) {
          subscription.unsubscribe();
        }
        resolve();
      });
    });
  }

  // Helper method to format data size in human-readable format
  formatDataSize(bytes: number): string {
    if (bytes === 0) {
      return '0 MB';
    }

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];

    const i = Math.floor(Math.log(bytes) / Math.log(k));
    const size = parseFloat((bytes / Math.pow(k, i)).toFixed(1));

    return `${size} ${sizes[i]}`;
  }

  /**
   * Get the total number of photos (uploaded + generated)
   */
  getTotalPhotos(): number {
    return this.dataStats.inputPhotos + this.dataStats.generatedPhotos;
  }

  /**
   * Comprehensive check for trained model status using multiple data sources
   * This addresses the issue where model status might be inconsistent across different endpoints
   */
  private async checkTrainedModelStatus(): Promise<void> {
    console.log('🔍 Checking trained model status using multiple data sources...');

    let hasTrainedModel = false;
    const statusSources: string[] = [];

    try {
      // Method 1: Check training status endpoint
      try {
        const trainingStatus = await this.fileUploadService.getTrainingStatus().toPromise();
        if (trainingStatus?.hasTrainedModel) {
          hasTrainedModel = true;
          statusSources.push('training-status');
          console.log('✅ Model found via training-status endpoint:', trainingStatus);
        }
      } catch (error) {
        console.warn('⚠️ Training status endpoint failed:', error);
      }

      // Method 2: Check user model requests endpoint
      try {
        const modelRequests = await this.fileUploadService.getUserModelRequests().toPromise();
        if (modelRequests?.success && modelRequests.data?.hasTrainedModel) {
          hasTrainedModel = true;
          statusSources.push('model-requests');
          console.log('✅ Model found via model-requests endpoint:', modelRequests.data);
        }
      } catch (error) {
        console.warn('⚠️ Model requests endpoint failed:', error);
      }

      // Method 3: Check user profile for model IDs
      if (this.userProfile?.trainedModelId) {
        hasTrainedModel = true;
        statusSources.push('user-profile');
        console.log('✅ Model found via user profile:', {
          trainedModelId: this.userProfile.trainedModelId,
          trainedModelVersionId: this.userProfile.trainedModelVersionId,
        });
      }

      // Method 4: Debug endpoint as final verification (if other methods disagree)
      if (!hasTrainedModel) {
        try {
          const debugStatus = await this.fileUploadService.getDebugModelStatus().toPromise();
          if (debugStatus?.success && debugStatus.data?.hasTrainedModel) {
            hasTrainedModel = true;
            statusSources.push('debug-status');
            console.log('✅ Model found via debug endpoint:', debugStatus.data);
          }
        } catch (error) {
          console.warn('⚠️ Debug status endpoint failed:', error);
        }
      }

      // Update the status
      this.dataStats.hasTrainedModel = hasTrainedModel;

      console.log(`🎯 Final trained model status: ${hasTrainedModel ? 'YES' : 'NO'}`, {
        sources: statusSources,
        totalSources: statusSources.length,
      });

      // Log warning if no model found but expected
      if (!hasTrainedModel && statusSources.length === 0) {
        console.warn(
          '⚠️ No trained model found across all endpoints. This might indicate a data consistency issue.'
        );
      }
    } catch (error) {
      console.error('❌ Error during comprehensive model status check:', error);
      // Keep existing status as fallback
    }
  }
}
