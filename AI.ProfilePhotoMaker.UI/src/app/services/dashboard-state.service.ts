import { Injectable } from '@angular/core';
import { BehaviorSubject, forkJoin } from 'rxjs';
import { UserProfile, ProfileService } from './profile.service';
import { CreditsInfo, ReplicateService } from './replicate.service';
import { UserCreditStatus, CreditService } from './credit.service';
import { FileUploadService } from './file-upload.service';
import { StyleService } from './style.service';
import { NotificationService } from './notification.service';

export interface UploadedImageThumbnail {
  id: number;
  url: string;
  fileName: string;
}

export interface DashboardState {
  userProfile: UserProfile | null;
  creditsInfo: CreditsInfo | null;
  userCreditStatus: UserCreditStatus | null;
  uploadedImages: number;
  uploadedImageThumbnails: UploadedImageThumbnail[];
  modelStatus: string;
  isPremiumWorkflow: boolean;
  isLoading: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class DashboardStateService {
  private readonly initialState: DashboardState = {
    userProfile: null,
    creditsInfo: null,
    userCreditStatus: null,
    uploadedImages: 0,
    uploadedImageThumbnails: [],
    modelStatus: 'Not Started',
    isPremiumWorkflow: false,
    isLoading: true,
  };

  private readonly _state = new BehaviorSubject<DashboardState>(this.initialState);
  readonly state$ = this._state.asObservable();

  constructor(
    private profileService: ProfileService,
    private replicateService: ReplicateService,
    private creditService: CreditService,
    private fileUploadService: FileUploadService,
    private styleService: StyleService,
    private notificationService: NotificationService
  ) { }

  getState(): DashboardState {
    return this._state.getValue();
  }

  setState(newState: Partial<DashboardState>) {
    this._state.next({
      ...this.getState(),
      ...newState
    });
  }

  loadInitialDashboardData() {
    this.setState({ isLoading: true });

    forkJoin({
      profile: this.profileService.getCurrentUserProfile(),
      credits: this.replicateService.getCredits(),
      creditStatus: this.creditService.getCreditStatus(),
      trainingStatus: this.fileUploadService.getTrainingStatus(),
      userImages: this.fileUploadService.getUserImages()
    }).subscribe({
      next: ({ profile, credits, creditStatus, trainingStatus, userImages }) => {
        const userProfile = profile.success ? profile.data : null;
        const creditsInfo = credits.success ? credits.data : null;
        const userCreditStatus = creditStatus.success ? creditStatus.data : null;
        
        // Process uploaded images into thumbnails format
        const uploadedImageThumbnails: UploadedImageThumbnail[] = userImages.images
          .filter(img => img.isOriginalUpload && img.fileExists)
          .map(img => ({
            id: img.id,
            url: img.originalImageUrl,
            fileName: `Image ${img.id}` // Use a default filename since it's not in ProcessedImage
          }));
        
        this.setState({
          userProfile,
          creditsInfo,
          userCreditStatus,
          uploadedImages: trainingStatus.totalUploadedImages || 0,
          uploadedImageThumbnails,
          modelStatus: trainingStatus.hasTrainedModel ? 'trained' : (trainingStatus.status || 'Not Started'),
          isPremiumWorkflow: (userCreditStatus?.purchasedCredits || 0) > 0,
          isLoading: false
        });
      },
      error: (error) => {
        this.notificationService.error('Dashboard Load Failed', 'Could not load dashboard data. Please try again.');
        this.setState({ isLoading: false });
      }
    });
  }

  resetState() {
    this._state.next(this.initialState);
  }
}
