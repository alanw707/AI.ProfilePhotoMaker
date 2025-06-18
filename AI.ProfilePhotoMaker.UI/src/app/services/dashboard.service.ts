import { Injectable } from '@angular/core';
import { Observable, BehaviorSubject, combineLatest, map, tap, catchError, of } from 'rxjs';
import { ProfileService, UserProfile } from './profile.service';
import { FileUploadService, ProcessedImage } from './file-upload.service';
import { ReplicateService, CreditsInfo } from './replicate.service';
import { StyleService, Style } from './style.service';

export interface DashboardState {
  userProfile: UserProfile | null;
  uploadedImages: ProcessedImage[];
  availableStyles: Style[];
  selectedStyles: Style[];
  generatedPhotos: ProcessedImage[];
  creditsInfo: CreditsInfo | null;
  trainingStatus: any;
  currentStep: number;
  loading: {
    profile: boolean;
    images: boolean;
    styles: boolean;
    credits: boolean;
    upload: boolean;
    generation: boolean;
  };
  errors: {
    profile: string | null;
    images: string | null;
    styles: string | null;
    credits: string | null;
    upload: string | null;
    generation: string | null;
  };
}

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private stateSubject = new BehaviorSubject<DashboardState>(this.getInitialState());
  public state$ = this.stateSubject.asObservable();

  constructor(
    private profileService: ProfileService,
    private fileUploadService: FileUploadService,
    private replicateService: ReplicateService,
    private styleService: StyleService
  ) {}

  private getInitialState(): DashboardState {
    return {
      userProfile: null,
      uploadedImages: [],
      availableStyles: [],
      selectedStyles: [],
      generatedPhotos: [],
      creditsInfo: null,
      trainingStatus: null,
      currentStep: 1,
      loading: {
        profile: false,
        images: false,
        styles: false,
        credits: false,
        upload: false,
        generation: false
      },
      errors: {
        profile: null,
        images: null,
        styles: null,
        credits: null,
        upload: null,
        generation: null
      }
    };
  }

  // Initialize dashboard data
  initializeDashboard(): Observable<DashboardState> {
    this.updateLoadingState({ profile: true, images: true, styles: true, credits: true });

    return combineLatest([
      this.loadUserProfile(),
      this.loadUserImages(),
      this.loadAvailableStyles(),
      this.loadCreditsInfo()
    ]).pipe(
      map(() => this.stateSubject.value),
      tap(() => this.updateCurrentStep()),
      tap(() => this.updateLoadingState({ profile: false, images: false, styles: false, credits: false }))
    );
  }

  // Profile Management
  loadUserProfile(): Observable<UserProfile | null> {
    this.updateLoadingState({ profile: true });
    this.clearError('profile');

    return this.profileService.getCurrentUserProfile().pipe(
      tap(response => {
        if (response.success) {
          this.updateState({ userProfile: response.data });
        } else {
          this.updateError('profile', 'Failed to load user profile');
        }
        this.updateLoadingState({ profile: false });
      }),
      map(response => response.success ? response.data : null),
      catchError(error => {
        this.updateError('profile', error.message || 'Failed to load user profile');
        this.updateLoadingState({ profile: false });
        return of(null);
      })
    );
  }

  updateUserProfile(profile: any): Observable<UserProfile | null> {
    this.updateLoadingState({ profile: true });
    this.clearError('profile');

    return this.profileService.updateProfile(profile).pipe(
      tap(response => {
        if (response.success) {
          this.updateState({ userProfile: response.data });
        } else {
          this.updateError('profile', 'Failed to update profile');
        }
        this.updateLoadingState({ profile: false });
      }),
      map(response => response.success ? response.data : null),
      catchError(error => {
        this.updateError('profile', error.message || 'Failed to update profile');
        this.updateLoadingState({ profile: false });
        return of(null);
      })
    );
  }

  // File Upload Management
  loadUserImages(): Observable<ProcessedImage[]> {
    this.updateLoadingState({ images: true });
    this.clearError('images');

    return this.fileUploadService.getUserImages().pipe(
      tap(response => {
        if (response.success) {
          this.updateState({ uploadedImages: response.data });
        } else {
          this.updateError('images', 'Failed to load images');
        }
        this.updateLoadingState({ images: false });
      }),
      map(response => response.success ? response.data : []),
      catchError(error => {
        this.updateError('images', error.message || 'Failed to load images');
        this.updateLoadingState({ images: false });
        return of([]);
      })
    );
  }

  uploadImages(files: File[]): Observable<{ progress: number; completed: boolean; success: boolean }> {
    this.updateLoadingState({ upload: true });
    this.clearError('upload');

    return this.fileUploadService.uploadImages(files).pipe(
      map(result => {
        if (result.response) {
          // Upload completed
          this.updateLoadingState({ upload: false });
          if (result.response.success) {
            this.loadUserImages(); // Refresh images
            this.updateCurrentStep();
            return { progress: 100, completed: true, success: true };
          } else {
            this.updateError('upload', result.response.message || 'Upload failed');
            return { progress: 100, completed: true, success: false };
          }
        } else {
          // Upload in progress
          return { progress: result.progress, completed: false, success: true };
        }
      }),
      catchError(error => {
        this.updateError('upload', error.message || 'Upload failed');
        this.updateLoadingState({ upload: false });
        return of({ progress: 0, completed: true, success: false });
      })
    );
  }

  // Style Management
  loadAvailableStyles(): Observable<Style[]> {
    this.updateLoadingState({ styles: true });
    this.clearError('styles');

    return this.styleService.getActiveStyles().pipe(
      tap(response => {
        if (response.success) {
          this.updateState({ availableStyles: response.data });
        } else {
          this.updateError('styles', 'Failed to load styles');
        }
        this.updateLoadingState({ styles: false });
      }),
      map(response => response.success ? response.data : []),
      catchError(error => {
        this.updateError('styles', error.message || 'Failed to load styles');
        this.updateLoadingState({ styles: false });
        return of([]);
      })
    );
  }

  selectStyles(styleIds: number[]): Observable<boolean> {
    this.clearError('styles');

    return this.styleService.selectStyles({ styleIds }).pipe(
      tap(response => {
        if (response.success) {
          const selectedStyles = this.stateSubject.value.availableStyles.filter(s => styleIds.includes(s.id));
          this.updateState({ selectedStyles });
          this.updateCurrentStep();
        } else {
          this.updateError('styles', response.message || 'Failed to select styles');
        }
      }),
      map(response => response.success),
      catchError(error => {
        this.updateError('styles', error.message || 'Failed to select styles');
        return of(false);
      })
    );
  }

  // Credits Management
  loadCreditsInfo(): Observable<CreditsInfo | null> {
    this.updateLoadingState({ credits: true });
    this.clearError('credits');

    return this.replicateService.getCredits().pipe(
      tap(response => {
        if (response.success) {
          this.updateState({ creditsInfo: response.data });
        } else {
          this.updateError('credits', 'Failed to load credits info');
        }
        this.updateLoadingState({ credits: false });
      }),
      map(response => response.success ? response.data : null),
      catchError(error => {
        this.updateError('credits', error.message || 'Failed to load credits info');
        this.updateLoadingState({ credits: false });
        return of(null);
      })
    );
  }

  // Free Generation
  generateFreeImage(gender: string, userInfo?: any): Observable<{ success: boolean; creditsRemaining?: number }> {
    this.updateLoadingState({ generation: true });
    this.clearError('generation');

    return this.replicateService.generateFreeImage({ gender, userInfo }).pipe(
      tap(response => {
        if (response.success) {
          // Update credits info
          this.loadCreditsInfo();
          // Refresh images to include new generation
          this.loadUserImages();
        } else {
          this.updateError('generation', response.error?.message || 'Failed to generate image');
        }
        this.updateLoadingState({ generation: false });
      }),
      map(response => ({
        success: response.success,
        creditsRemaining: response.data?.creditsRemaining
      })),
      catchError(error => {
        this.updateError('generation', error.error?.message || 'Failed to generate image');
        this.updateLoadingState({ generation: false });
        return of({ success: false });
      })
    );
  }

  // Current step calculation
  private updateCurrentStep(): void {
    const state = this.stateSubject.value;
    let currentStep = 1;

    if (state.uploadedImages.length > 0) {
      currentStep = 2;
      
      if (state.trainingStatus === 'trained') {
        currentStep = 3;
        
        if (state.selectedStyles.length > 0) {
          currentStep = 4;
        }
      }
    }

    this.updateState({ currentStep });
  }

  // State management helpers
  private updateState(updates: Partial<DashboardState>): void {
    const currentState = this.stateSubject.value;
    this.stateSubject.next({ ...currentState, ...updates });
  }

  private updateLoadingState(loadingUpdates: Partial<DashboardState['loading']>): void {
    const currentState = this.stateSubject.value;
    this.updateState({ 
      loading: { ...currentState.loading, ...loadingUpdates } 
    });
  }

  private updateError(errorKey: keyof DashboardState['errors'], message: string): void {
    const currentState = this.stateSubject.value;
    this.updateState({ 
      errors: { ...currentState.errors, [errorKey]: message } 
    });
  }

  private clearError(errorKey: keyof DashboardState['errors']): void {
    const currentState = this.stateSubject.value;
    this.updateState({ 
      errors: { ...currentState.errors, [errorKey]: null } 
    });
  }

  // Getters for specific state slices
  get userProfile$(): Observable<UserProfile | null> {
    return this.state$.pipe(map(state => state.userProfile));
  }

  get uploadedImages$(): Observable<ProcessedImage[]> {
    return this.state$.pipe(map(state => state.uploadedImages));
  }

  get availableStyles$(): Observable<Style[]> {
    return this.state$.pipe(map(state => state.availableStyles));
  }

  get creditsInfo$(): Observable<CreditsInfo | null> {
    return this.state$.pipe(map(state => state.creditsInfo));
  }

  get currentStep$(): Observable<number> {
    return this.state$.pipe(map(state => state.currentStep));
  }

  get loading$(): Observable<DashboardState['loading']> {
    return this.state$.pipe(map(state => state.loading));
  }

  get errors$(): Observable<DashboardState['errors']> {
    return this.state$.pipe(map(state => state.errors));
  }
}