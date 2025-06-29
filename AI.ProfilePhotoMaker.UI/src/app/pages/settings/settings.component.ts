import { Component, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { ProfileService, UserProfile } from '../../services/profile.service';
import { FileUploadService } from '../../services/file-upload.service';
import { NotificationService } from '../../services/notification.service';
import { DashboardStateService } from '../../services/dashboard-state.service';

interface DataStats {
  inputPhotos: number;
  generatedPhotos: number;
  enhancedPhotos: number;
  hasTrainedModel: boolean;
  totalDataSize: number;
  accountAge: number;
}

type DeletionType = 'photos' | 'model' | 'all' | 'account';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, HeaderNavigationComponent],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.sass']
})
export class SettingsComponent implements OnInit {
  // User Info
  userProfile: UserProfile | null = null;
  userEmail: string = '';

  // Loading States
  isLoading: boolean = true;
  isDeleting: boolean = false;
  isExporting: boolean = false;

  // Data Statistics
  dataStats: DataStats = {
    inputPhotos: 0,
    generatedPhotos: 0,
    enhancedPhotos: 0,
    hasTrainedModel: false,
    totalDataSize: 0,
    accountAge: 0
  };

  // Confirmation Modal State
  showConfirmationModal: boolean = false;
  deletionType: DeletionType = 'photos';
  confirmationText: string = '';
  confirmationTitle: string = '';
  confirmationMessage: string = '';

  // Credit Management State
  creditsInfo: any = null;
  userCreditStatus: any = null;

  constructor(
    private authService: AuthService,
    private router: Router,
    private profileService: ProfileService,
    private fileUploadService: FileUploadService,
    private notificationService: NotificationService,
    private dashboardStateService: DashboardStateService
  ) {}

  async ngOnInit() {
    console.log('Settings ngOnInit');
    
    // Check authentication first
    if (!this.authService.isAuthenticated()) {
      console.log('Not authenticated, redirecting to login');
      this.router.navigate(['/login']);
      return;
    }
    
    console.log('User is authenticated, loading settings data');
    this.loadUserInfo();
    await this.loadDataStats();
    this.loadUserProfile();
    this.loadCreditInfo();
    this.isLoading = false;
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
      next: (response) => {
        if (response.success) {
          this.userProfile = response.data;

          // Calculate account age
          if (this.userProfile.createdAt) {
            const createdDate = new Date(this.userProfile.createdAt);
            const now = new Date();
            this.dataStats.accountAge = Math.floor((now.getTime() - createdDate.getTime()) / (1000 * 60 * 60 * 24));
          }
        } else {
          this.notificationService.error('Profile Load Failed', 'Failed to load user profile information.');
        }
      },
      error: (error) => {
        console.error('Failed to load user profile:', error);
        
        // Email is already loaded from auth service
        
        this.notificationService.error('Profile Load Failed', 'Unable to connect to the server. Please check your connection and try again.');
      }
    });
  }

  async loadDataStats() {
    try {
      // Load data stats from API
      const statsResponse = await this.profileService.getDataStats().toPromise();
      if (statsResponse && statsResponse.success) {
        this.dataStats = {
          inputPhotos: statsResponse.data.inputPhotos || 0,
          generatedPhotos: statsResponse.data.generatedPhotos || 0,
          enhancedPhotos: statsResponse.data.enhancedPhotos || 0,
          hasTrainedModel: statsResponse.data.hasTrainedModel || false,
          totalDataSize: statsResponse.data.totalDataSize || 0,
          accountAge: statsResponse.data.accountAge || 0
        };
      } else {
        // Fallback to existing method if API is not available
        const imagesResponse = await this.fileUploadService.getUserImages().toPromise();
        if (imagesResponse) {
          const originalImages = imagesResponse.images.filter(img => !img.isGenerated);
          const generatedImages = imagesResponse.images.filter(img => img.isGenerated);
          const enhancedImages = imagesResponse.images.filter(img => img.style === 'Enhanced' || img.style === 'Background Remover' || img.style === 'Social Media' || img.style === 'Cartoon');
          
          this.dataStats.inputPhotos = originalImages.length;
          this.dataStats.generatedPhotos = generatedImages.length;
          this.dataStats.enhancedPhotos = enhancedImages.length;
        }

        // Check if user has trained model
        const trainingStatus = await this.fileUploadService.getTrainingStatus().toPromise();
        if (trainingStatus) {
          this.dataStats.hasTrainedModel = trainingStatus.hasTrainedModel;
        }
      }
    } catch (error) {
      console.error('Error loading data stats:', error);
      this.notificationService.warning('Data Load Warning', 'Some data statistics may not be available.');
    }
  }

  // Helper Methods
  getFullName(): string {
    if (!this.userProfile) return '';
    const firstName = this.userProfile.firstName || '';
    const lastName = this.userProfile.lastName || '';
    return `${firstName} ${lastName}`.trim() || 'Not provided';
  }

  formatDate(date: Date | string): string {
    if (!date) return 'Not available';
    const d = new Date(date);
    return d.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }

  // Navigation Methods
  editProfile() {
    // For now, just show a notification. In a full implementation, this would open an edit modal
    this.notificationService.info('Feature Coming Soon', 'Profile editing will be available in a future update.');
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
        this.confirmationMessage = 'Are you sure you want to delete your trained AI model? You will need to re-upload photos and retrain to generate new styled photos.';
        break;
      case 'all':
        this.confirmationTitle = 'Delete All Data';
        this.confirmationMessage = 'Are you sure you want to permanently delete ALL your data? This includes all photos, AI models, and usage history. This action cannot be undone.';
        break;
      case 'account':
        this.confirmationTitle = 'Delete Account';
        this.confirmationMessage = 'Are you sure you want to permanently delete your entire account? This will close your account, delete all data, and log you out immediately. This action cannot be undone.';
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
      this.notificationService.error('Delete Failed', 'The delete operation failed. Please try again.');
    } finally {
      this.isDeleting = false;
      this.showConfirmationModal = false;
    }
  }

  private async deleteInputPhotos() {
    try {
      const response = await this.profileService.deleteInputPhotos().toPromise();
      if (response && response.success) {
        this.notificationService.success('Photos Deleted', `Successfully deleted ${response.data.deletedCount} input photos.`);
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
      if (response && response.success) {
        this.notificationService.success('AI Model Deleted', response.data.message || 'Your trained AI model has been successfully deleted.');
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
      if (response && response.success) {
        this.notificationService.success('All Data Deleted', response.data.message || 'All your data has been successfully deleted.');
        // Reset all stats
        this.dataStats = {
          inputPhotos: 0,
          generatedPhotos: 0,
          enhancedPhotos: 0,
          hasTrainedModel: false,
          totalDataSize: 0,
          accountAge: this.dataStats.accountAge
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
      if (response && response.success) {
        this.notificationService.success('Account Deleted', 'Your account has been successfully deleted. You will be logged out.');
        // Log out user immediately
        setTimeout(() => {
          this.authService.logout();
          this.router.navigate(['/login']);
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
        
        this.notificationService.success('Export Complete', 'Your data export has been generated and downloaded.');
      } else {
        throw new Error('No data received from export');
      }
    } catch (error) {
      console.error('Export failed:', error);
      this.notificationService.error('Export Failed', 'Failed to export your data. Please try again.');
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

  getTotalAvailableCredits(): number {
    const weeklyCredits = this.getWeeklyCredits();
    const purchasedCredits = this.getPurchasedCredits();
    return weeklyCredits + purchasedCredits;
  }

  getPurchasedCredits(): number {
    return this.userCreditStatus?.purchasedCredits || 0;
  }

  getWeeklyCredits(): number {
    return this.userCreditStatus?.weeklyCredits || this.creditsInfo?.availableCredits || 0;
  }

  getMaxWeeklyCredits(): number {
    return 3; // Fixed weekly credit limit
  }

  getCreditUsagePercentage(): number {
    const weekly = this.getWeeklyCredits();
    const max = this.getMaxWeeklyCredits();
    return max > 0 ? (weekly / max) * 100 : 0;
  }

  getNextCreditReset(): string {
    // Calculate next weekly reset (simplified)
    const now = new Date();
    const nextWeek = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
    return nextWeek.toLocaleDateString('en-US', {
      weekday: 'long',
      month: 'short',
      day: 'numeric'
    });
  }

}
