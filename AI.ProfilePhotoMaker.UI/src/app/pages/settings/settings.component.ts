import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { ProfileService, UserProfile } from '../../services/profile.service';
import { FileUploadService } from '../../services/file-upload.service';
import { NotificationService } from '../../services/notification.service';
import { DashboardCoordinatorService } from '../../services/dashboard-coordinator.service';
import { AccountInfoComponent } from '../../components/settings/account-info/account-info.component';
import { CreditManagementComponent } from '../../components/settings/credit-management/credit-management.component';
import { firstValueFrom } from 'rxjs';

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
export class SettingsComponent implements OnInit, OnDestroy {
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

  // Subscription Management
  private subscriptions: any[] = [];

  constructor(
    private authService: AuthService,
    private router: Router,
    private profileService: ProfileService,
    private fileUploadService: FileUploadService,
    private notificationService: NotificationService,
    private dashboardStateService: DashboardCoordinatorService,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit() {
    // Check authentication first
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/auth/login']);
      return;
    }

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

  ngOnDestroy() {
    // Clean up all subscriptions to prevent memory leaks
    this.subscriptions.forEach(subscription => {
      if (subscription && typeof subscription.unsubscribe === 'function') {
        subscription.unsubscribe();
      }
    });
    this.subscriptions = [];
  }

  loadUserInfo() {
    // Get user email from auth service with proper subscription handling
    const subscription = this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userEmail = user.email;
      }
    });

    // Store subscription for cleanup
    this.subscriptions.push(subscription);
  }

  loadUserProfile() {
    // Load user profile from API
    const subscription = this.profileService.getCurrentUserProfile().subscribe({
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

    // Store subscription for cleanup
    this.subscriptions.push(subscription);
  }

  async loadDataStats() {
    try {
      // Load data stats from API
      const statsResponse = await firstValueFrom(this.profileService.getDataStats());
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
        // Wait for user profile to be loaded first to check trainedModelId
        if (!this.userProfile) {
          await this.loadUserProfileAsync();
        }
        await this.checkTrainedModelStatus();
      } else {
        // Fallback to existing method if API is not available
        const imagesResponse = await firstValueFrom(this.fileUploadService.getUserImages());
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
        // Wait for user profile to be loaded first to check trainedModelId
        if (!this.userProfile) {
          await this.loadUserProfileAsync();
        }
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
          'Are you sure you want to delete your AI model? This includes trained models and any failed training attempts stored on our servers. You will need to re-upload photos and retrain to generate new styled photos.';
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

      // Success - close modal after operation completes
      this.isDeleting = false;
      this.showConfirmationModal = false;
    } catch (error) {
      console.error('Delete operation failed:', error);
      this.notificationService.error(
        'Delete Failed',
        'The delete operation failed. Please try again.'
      );

      // Error - just reset loading state, keep modal open
      this.isDeleting = false;
    }
  }

  private async deleteInputPhotos() {
    try {
      const response = await firstValueFrom(this.profileService.deleteInputPhotos());
      if (response?.success) {
        this.notificationService.success(
          'Photos Deleted',
          `Successfully deleted ${response.data.deletedCount} input photos.`
        );
        this.dataStats.inputPhotos = 0;
        // Refresh stats, but don't fail the overall delete if this errors
        try {
          await this.loadDataStats();
        } catch (refreshErr) {
          console.warn('Post-delete stats refresh failed:', refreshErr);
        }
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
      const response = await firstValueFrom(this.profileService.deleteAIModel());
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
        // Close modal and reset state immediately so the UI doesn't feel stuck
        this.isDeleting = false;
        this.showConfirmationModal = false;
        this.cdr.detectChanges();

        // Refresh stats in the background; don't block UI/closing
        this.loadDataStats().catch(refreshErr => {
          console.warn('Post-delete stats refresh failed:', refreshErr);
        });
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
      const response = await firstValueFrom(this.profileService.deleteAllUserData());
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
        // Refresh stats, but don't fail the overall delete if this errors
        try {
          await this.loadDataStats();
        } catch (refreshErr) {
          console.warn('Post-delete stats refresh failed:', refreshErr);
        }
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
      const response = await firstValueFrom(this.profileService.deleteUserAccount());
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
      const blob = await firstValueFrom(this.profileService.exportUserData());
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
    // Subscribe to dashboard state for credit information with proper cleanup
    const subscription = this.dashboardStateService.state$.subscribe(state => {
      this.creditsInfo = state.creditsInfo;
      this.userCreditStatus = state.userCreditStatus;
    });

    // Store subscription for cleanup
    this.subscriptions.push(subscription);

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
        firstValueFrom(this.profileService.getCurrentUserProfile()),
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
   * Check if we can delete AI models (includes Failed models that exist on Replicate)
   */
  canDeleteAIModel(): boolean {
    const canDelete = this.dataStats.hasTrainedModel || !!this.userProfile?.trainedModelId;

    if (canDelete) {
      console.log('🗑️ Model deletion enabled:', {
        hasTrainedModel: this.dataStats.hasTrainedModel,
        trainedModelId: this.userProfile?.trainedModelId,
        reason: this.dataStats.hasTrainedModel ? 'hasTrainedModel=true' : 'trainedModelId exists',
      });
    }

    return canDelete;
  }

  /**
   * Comprehensive check for trained model status using multiple data sources
   * This addresses the issue where model status might be inconsistent across different endpoints
   * Updated to properly detect Failed models that exist on Replicate for deletion purposes
   */
  private async checkTrainedModelStatus(): Promise<void> {
    let hasTrainedModel = false;
    const statusSources: string[] = [];

    try {
      // Method 1: Check user profile for model IDs (primary source - includes Failed models with ReplicateModelId)
      if (this.userProfile?.trainedModelId) {
        hasTrainedModel = true;
        statusSources.push('user-profile');
        console.log('✅ Model found in user profile:', this.userProfile.trainedModelId);
      }

      // Method 2: Check training status endpoint (for Ready models only)
      try {
        const trainingStatus = await firstValueFrom(this.fileUploadService.getTrainingStatus());
        if (trainingStatus?.hasTrainedModel) {
          hasTrainedModel = true;
          statusSources.push('training-status');
        }
      } catch (error) {
        console.warn('⚠️ Training status endpoint failed:', error);
      }

      // Method 3: Check user model requests endpoint (fallback)
      try {
        const modelRequests = await firstValueFrom(this.fileUploadService.getUserModelRequests());
        if (modelRequests?.success && modelRequests.data?.hasTrainedModel) {
          hasTrainedModel = true;
          statusSources.push('model-requests');
        }
      } catch (error) {
        console.warn('⚠️ Model requests endpoint failed:', error);
      }

      // Update the status - now includes Failed models that exist on Replicate
      this.dataStats.hasTrainedModel = hasTrainedModel;

      console.log(`🔍 Model status check complete:`, {
        hasTrainedModel,
        sources: statusSources,
        trainedModelId: this.userProfile?.trainedModelId,
        trainedModelVersionId: this.userProfile?.trainedModelVersionId,
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
