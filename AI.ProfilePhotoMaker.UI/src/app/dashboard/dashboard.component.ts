import { Component, OnInit, OnDestroy, NgZone } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';

import { HeaderNavigationComponent } from '../shared/header-navigation/header-navigation.component';
import { StatsCardComponent } from '../components/dashboard/stats-card/stats-card.component';
import { StyleSelectorComponent, StyleOption } from '../components/dashboard/style-selector/style-selector.component';
import { FileUploadSectionComponent } from '../components/dashboard/file-upload-section/file-upload-section.component';
import { CreditDisplayComponent } from '../components/dashboard/credit-display/credit-display.component';

import { AuthService } from '../services/auth.service';
import { FileUploadService } from '../services/file-upload.service';
import { StyleService, Style } from '../services/style.service';
import { NotificationService } from '../services/notification.service';
import { CreditService } from '../services/credit.service';
import { DashboardStateService } from '../services/dashboard-state.service';
import { ConfigService } from '../services/config.service';
import { WorkflowOrchestrationService, WorkflowProgress } from '../services/workflow-orchestration.service';

import { GalleryImage } from '../components/photo-gallery/photo-gallery.component';
import { 
  GeneratedPhoto, 
  QualityCheckError, 
  SelectedFileWithQuality, 
  QualityCheckResult,
  UploadProgress,
  TrainingStatus,
  GenerationStatus
} from '../models/dashboard.types';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    FormsModule, 
    HeaderNavigationComponent, 
    StatsCardComponent, 
    StyleSelectorComponent,
    FileUploadSectionComponent,
    CreditDisplayComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.sass']
})
export class DashboardComponent implements OnInit, OnDestroy {

  state$: Observable<any>;
  workflowProgress$: Observable<WorkflowProgress>;

  // Component-specific state
  currentStep: number = 1;
  isTrainingStarted: boolean = false;
  imagesPerStyle: number = 2;
  availableStyles: StyleOption[] = [];
  selectedStyles: number = 0;

  // State-based getters for template
  get uploadedImages(): number {
    return this.stateService.getState().uploadedImages;
  }

  get modelStatus(): string {
    return this.stateService.getState().modelStatus;
  }

  get creditsInfo(): any {
    return this.stateService.getState().creditsInfo;
  }

  get userCreditStatus(): any {
    return this.stateService.getState().userCreditStatus;
  }

  get uploadedImageThumbnails(): Array<{id: number; url: string; fileName: string}> {
    return this.stateService.getState().uploadedImageThumbnails;
  }

  get generatedPhotosCount(): number {
    return this.stateService.getState().generatedPhotosCount;
  }

  getTotalAvailableCredits(): number {
    const weeklyCredits = this.getWeeklyCredits();
    const purchasedCredits = this.getPurchasedCredits();
    
    // Always calculate total from individual components to ensure accuracy
    return weeklyCredits + purchasedCredits;
  }

  getPurchasedCredits(): number {
    return this.userCreditStatus?.purchasedCredits || 0;
  }

  getWeeklyCredits(): number {
    // Use weeklyCredits from userCreditStatus first, fallback to creditsInfo.availableCredits
    return this.userCreditStatus?.weeklyCredits || this.creditsInfo?.availableCredits || 0;
  }

  onCreditAction(event: { action: string, context?: string }): void {
    switch (event.action) {
      case 'purchase':
      case 'viewPackages':
        this.router.navigate(['/packages']);
        break;
      case 'upgrade':
        this.router.navigate(['/premium']);
        break;
      default:
        console.warn('Unknown credit action:', event.action);
    }
  }

  constructor(
    private authService: AuthService,
    private router: Router,
    private fileUploadService: FileUploadService,
    private styleService: StyleService,
    private notificationService: NotificationService,
    public creditService: CreditService,
    public stateService: DashboardStateService,
    private config: ConfigService,
    private workflowService: WorkflowOrchestrationService,
    private ngZone: NgZone
  ) {
    this.state$ = this.stateService.state$;
    this.workflowProgress$ = this.workflowService.progress$;
    
    // Enable debug methods for troubleshooting
    this.stateService.enableGlobalDebug();
  }

  ngOnInit() {
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }

    // Subscribe to state changes to update UI
    this.state$.subscribe(_state => {
      // Force change detection when state updates
      this.selectedStyles = this.getSelectedStylesCount();
      
      // Update current step based on progress
      this.updateCurrentStep();
    });

    this.stateService.loadInitialDashboardData();
    this.loadAvailableStyles();
  }
  ngOnDestroy() {
    this.stateService.resetState();
    this.workflowService.dispose();
  }
  private updateCurrentStep() {
    if ((this.uploadedImages > 0 || this.uploadedImageThumbnails.length > 0) && this.currentStep === 1) {
      this.currentStep = 2;
    }
    if (this.generatedPhotosCount > 0 && this.currentStep === 2) {
      this.currentStep = 3;
    }
  }
  private loadAvailableStyles() {
    this.styleService.getActiveStyles().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.availableStyles = response.data.map(style => ({
            id: style.id.toString(),
            name: style.name,
            description: style.description,
            previewUrl: this.getStylePreviewUrl(style.name),
            selected: false
          }));
        } else {
          console.error('Failed to load styles:', response.error);
          this.notificationService.error('Style Load Failed', 'Could not load available styles. Please refresh the page.');
        }
      },
      error: (error) => {
        console.error('Error loading styles:', error);
        this.notificationService.error('Style Load Failed', 'Could not load available styles. Please refresh the page.');
      }
    });
  }
  private getStylePreviewUrl(styleName: string): string {
    const fileName = styleName.toLowerCase().replace(/[\s\/]+/g, '-');
    const cacheBuster = Date.now();
    return `${this.config.getApiUrl()}/style-previews/${fileName}.jpg?v=${cacheBuster}`;
  }

  // UI Event Handlers
  onFilesSelected(files: File[]) { /* Dashboard awareness only */ }

  onUploadCompleted(uploadedFiles: any[]) {
    console.log('Upload completed, refreshing images from server:', uploadedFiles);
    this.refreshUploadedImagesFromServer();
    
    setTimeout(() => {
      const thumbnails = this.stateService.getState().uploadedImageThumbnails;
      if (thumbnails.length >= 10) {
        this.currentStep = 2;
      }
    }, 1000);
  }

  onUploadProgress(progress: number) { /* Dashboard awareness only */ }
  onQualityCheckCompleted(result: QualityCheckResult) { /* Dashboard awareness only */ }
  onFileRemoved(index: number) { /* Dashboard awareness only */ }

  onUploadedImageDeleted(event: { thumb: any, index: number, refreshRequired?: boolean }) {
    const { thumb, refreshRequired } = event;
    
    if (refreshRequired) {
      this.refreshUploadedImagesFromServer();
      return;
    }
    
    if (thumb && thumb.id) {
      const currentThumbnails = this.stateService.getState().uploadedImageThumbnails;
      const updatedThumbnails = currentThumbnails.filter(t => t.id !== thumb.id);
      this.stateService.setState({ 
        uploadedImageThumbnails: updatedThumbnails,
        uploadedImages: updatedThumbnails.length 
      });
    }
  }

  private async refreshUploadedImagesFromServer() {
    try {
      this.stateService.forceRefresh();
      console.log('Successfully refreshed uploaded images from server');
    } catch (error) {
      console.error('Failed to refresh uploaded images:', error);
      this.notificationService.error('Refresh Failed', 'Failed to refresh image list from server');
    }
  }


  selectAllStyles() {
    this.availableStyles.forEach(style => style.selected = true);
    this.selectedStyles = this.getSelectedStylesCount();
  }

  deselectAllStyles() {
    this.availableStyles.forEach(style => style.selected = false);
    this.selectedStyles = this.getSelectedStylesCount();
  }

  toggleStyle(style: StyleOption) {
    style.selected = !style.selected;
    this.selectedStyles = this.getSelectedStylesCount();
  }

  onStyleToggled(style: StyleOption) {
    this.toggleStyle(style);
  }

  onImagesPerStyleChanged(count: number) {
    this.imagesPerStyle = count;
  }

  onSelectAllStyles() {
    this.selectAllStyles();
  }

  onDeselectAllStyles() {
    this.deselectAllStyles();
  }

  onStartTraining() {
    this.startTrainingWithStyles();
  }

  onDismissSuccessMessage() {
    this.workflowService.dismissSuccessMessage();
  }

  async startTrainingWithStyles() {
    const selectedStyles = this.availableStyles.filter(s => s.selected);
    this.isTrainingStarted = true;
    this.currentStep = 3;

    try {
      await this.workflowService.startTrainingWithStyles(selectedStyles, this.imagesPerStyle);
    } catch (error) {
      console.error('Error in training workflow:', error);
      this.isTrainingStarted = false;
      this.currentStep = 2;
    }
  }

  // Model training is now handled by WorkflowOrchestrationService

  // Training status polling is now handled by WorkflowOrchestrationService

  // Photo completion polling is now handled by WorkflowOrchestrationService

  // Time-based progress tracking is now handled by WorkflowOrchestrationService

  // Photo generation completion is now handled by WorkflowOrchestrationService

  // Image generation with styles is now handled by WorkflowOrchestrationService

  // Photo download and share methods moved to gallery component

  async downloadAll() {
    // Since the dashboard doesn't have direct access to photo data,
    // redirect users to the gallery where they can download photos
    this.notificationService.info('Gallery Navigation', 'Redirecting to gallery to view and download your photos');
    this.router.navigate(['/gallery']);
  }


  onImageError(event: any) {
    event.target.src = `${this.config.getApiUrl()}/api/placeholder/style-preview`;
    event.target.onerror = null;
  }


  // Workflow methods
  isPremiumWorkflow(): boolean {
    return true;
  }

  getStepStatus(step: number): string {
    const hasUploadedImages = this.uploadedImages > 0 || this.uploadedImageThumbnails.length > 0;
    
    switch (step) {
      case 1:
        if (hasUploadedImages) return 'completed';
        if (this.currentStep === 1) return 'active';
        return 'pending';
      case 2:
        if (hasUploadedImages && this.generatedPhotosCount === 0) return 'active';
        if (this.generatedPhotosCount > 0) return 'completed';
        return 'pending';
      case 3:
        if (this.generatedPhotosCount > 0) return 'completed';
        return 'pending';
      default:
        if (step < this.currentStep) return 'completed';
        if (step === this.currentStep) return 'active';
        return 'pending';
    }
  }

  getStepStatusText(step: number): string {
    const status = this.getStepStatus(step);
    switch (status) {
      case 'completed': return 'Completed';
      case 'active': return 'In Progress';
      default: return 'Pending';
    }
  }

  // Credit calculation methods
  private getCreditCalculation() {
    const selectedStyles = this.availableStyles.filter(s => s.selected);
    return this.workflowService.calculateCredits(selectedStyles, this.imagesPerStyle, this.modelStatus);
  }

  calculateTotalCredits(): number {
    return this.getCreditCalculation().totalCredits;
  }

  hasEnoughCredits(): boolean {
    return this.getCreditCalculation().hasEnoughCredits;
  }

  getRemainingCredits(): number {
    return this.getCreditCalculation().remainingCredits;
  }

  getSelectedStylesCount(): number {
    return this.availableStyles.filter(s => s.selected).length;
  }


  calculateTrainingCredits(): number {
    return this.getCreditCalculation().trainingCredits;
  }

  calculateGenerationCredits(): number {
    return this.getCreditCalculation().generationCredits;
  }


  continueInBackground() {
    this.notificationService.info('Continuing in Background', 
      'Training and generation will continue. We\'ll email you when your photos are ready.');
    this.router.navigate(['/gallery']);
  }

}

