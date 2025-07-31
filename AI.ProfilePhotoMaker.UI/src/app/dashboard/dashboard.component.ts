import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Injector, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BehaviorSubject, Observable } from 'rxjs';

import { HeaderNavigationComponent } from '../shared/header-navigation/header-navigation.component';
import { StatsCardComponent } from '../components/dashboard/stats-card/stats-card.component';
import {
  StyleOption,
  StyleSelectorComponent,
} from '../components/dashboard/style-selector/style-selector.component';
import { FileUploadSectionComponent } from '../components/dashboard/file-upload-section/file-upload-section.component';
import { CreditDisplayComponent } from '../components/dashboard/credit-display/credit-display.component';

import { AuthService } from '../services/auth.service';
import { StyleService } from '../services/style.service';
import { NotificationService } from '../services/notification.service';
import { CreditService } from '../services/credit.service';
import { DashboardCoordinatorService } from '../services/dashboard-coordinator.service';
import { ConfigService } from '../services/config.service';
// Lazy-loaded service types
interface WorkflowProgress {
  isTraining: boolean;
  isGenerating: boolean;
  progressPercentage: number;
  progressMessage: string;
  estimatedCompletion: string;
  trainingId: string;
  generationStartTime: number;
  expectedGenerationTime: number;
  lastGenerationCount: number;
  showLastGenerationMessage: boolean;
  activePredictionIds: string[];
}

interface WorkflowOrchestrationService {
  progress$: Observable<WorkflowProgress>;
  startTrainingWithStyles(selectedStyles: StyleOption[], imagesPerStyle: number): Promise<void>;
  calculateCredits(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    modelStatus: string
  ): {
    trainingCredits: number;
    generationCredits: number;
    totalCredits: number;
    hasEnoughCredits: boolean;
    remainingCredits: number;
  };
  dismissSuccessMessage(): void;
  dispose(): void;
}
import { WorkflowStepService } from '../services/workflow-step.service';

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
    CreditDisplayComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit, OnDestroy {
  state$: Observable<unknown>;
  workflowProgress$: Observable<WorkflowProgress>;

  // Component-specific state
  currentStep = 1;
  isTrainingStarted = false;
  imagesPerStyle = 2;
  availableStyles: StyleOption[] = [];
  selectedStyles = 0;

  // Lazy-loaded service
  private workflowService: WorkflowOrchestrationService | null = null;
  private workflowProgressSubject = new BehaviorSubject<WorkflowProgress>({
    isTraining: false,
    isGenerating: false,
    progressPercentage: 0,
    progressMessage: '',
    estimatedCompletion: '',
    trainingId: '',
    generationStartTime: 0,
    expectedGenerationTime: 0,
    lastGenerationCount: 0,
    showLastGenerationMessage: false,
    activePredictionIds: [],
  });

  // State-based getters for template - removed, using stateService.getState() directly

  getTotalAvailableCredits(): number {
    return this.creditService.getTotalAvailableCredits(
      this.stateService.getState().userCreditStatus,
      this.stateService.getState().creditsInfo
    );
  }

  getPurchasedCredits(): number {
    return this.creditService.getPurchasedCredits(this.stateService.getState().userCreditStatus);
  }

  getWeeklyCredits(): number {
    return this.creditService.getWeeklyCredits(
      this.stateService.getState().userCreditStatus,
      this.stateService.getState().creditsInfo
    );
  }

  onCreditAction(event: { action: string; context?: string }): void {
    switch (event.action) {
      case 'purchase':
      case 'viewPackages':
        this._router.navigate(['/pricing']);
        break;
      case 'upgrade':
        this._router.navigate(['/premium']);
        break;
      default:
      // Unknown credit action - silently ignore
    }
  }

  constructor(
    private _authService: AuthService,
    private _router: Router,
    private _styleService: StyleService,
    private _notificationService: NotificationService,
    public creditService: CreditService,
    public stateService: DashboardCoordinatorService,
    private _config: ConfigService,
    private _workflowStepService: WorkflowStepService,
    private _injector: Injector,
    private _cdr: ChangeDetectorRef
  ) {
    this.state$ = this.stateService.state$;
    this.workflowProgress$ = this.workflowProgressSubject.asObservable();
  }

  ngOnInit(): void {
    if (!this._authService.isAuthenticated()) {
      this._router.navigate(['/auth/login']);
      return;
    }

    // Subscribe to state changes to update UI
    this.state$.subscribe(_state => {
      // Force change detection when state updates
      this.selectedStyles = this.getSelectedStylesCount();

      // Update current step based on progress
      this.updateCurrentStep();

      // Force change detection for async updates
      this._cdr.detectChanges();
    });

    this.stateService.loadInitialDashboardData();
    this.loadAvailableStyles();
  }
  ngOnDestroy(): void {
    this.stateService.resetState();
    if (this.workflowService) {
      this.workflowService.dispose();
    }
    this.workflowProgressSubject.complete();
  }
  private updateCurrentStep(): void {
    this.currentStep = this._workflowStepService.updateCurrentStep(
      this.stateService.getState().uploadedImages,
      this.stateService.getState().uploadedImageThumbnails,
      this.stateService.getState().generatedPhotosCount,
      this.currentStep
    );
  }
  private loadAvailableStyles(): void {
    this._styleService.getActiveStyles().subscribe({
      next: response => {
        if (response.success && response.data) {
          this.availableStyles = response.data.map(style => ({
            id: style.id.toString(),
            name: style.name,
            description: style.description,
            previewUrl: this.getStylePreviewUrl(style.name),
            selected: false,
          }));
        } else {
          // Failed to load styles - error handled by notification
          this._notificationService.error(
            'Style Load Failed',
            'Could not load available styles. Please refresh the page.'
          );
        }
      },
      error: error => {
        // Error loading styles - handled by notification
        this._notificationService.error(
          'Style Load Failed',
          'Could not load available styles. Please refresh the page.'
        );
      },
    });
  }
  private getStylePreviewUrl(styleName: string): string {
    return this._config.buildStylePreviewUrl(styleName);
  }

  // UI Event Handlers
  onUploadCompleted(uploadedFiles: unknown[]) {
    this.refreshUploadedImagesFromServer();

    setTimeout(() => {
      const thumbnails = this.stateService.getState().uploadedImageThumbnails;
      if (thumbnails.length >= 10) {
        this.currentStep = 2;
        this._cdr.detectChanges();
      }
    }, 1000);
  }

  onUploadedImageDeleted(event: { thumb: unknown; index: number; refreshRequired?: boolean }) {
    const { thumb, index, refreshRequired } = event;

    if (refreshRequired) {
      // For stale references, immediately remove from UI and refresh from server
      const currentThumbnails = this.stateService.getState().uploadedImageThumbnails;
      if (index >= 0 && index < currentThumbnails.length) {
        const updatedThumbnails = [...currentThumbnails];
        updatedThumbnails.splice(index, 1);
        this.stateService.setState({
          uploadedImageThumbnails: updatedThumbnails,
          uploadedImages: updatedThumbnails.length,
        });
        this._cdr.detectChanges();
      }
      // Also refresh from server to sync completely
      this.refreshUploadedImagesFromServer();
      return;
    }

    if ((thumb as { id?: number })?.id) {
      const currentThumbnails = this.stateService.getState().uploadedImageThumbnails;
      const updatedThumbnails = currentThumbnails.filter(
        t => t.id !== (thumb as { id: number }).id
      );
      this.stateService.setState({
        uploadedImageThumbnails: updatedThumbnails,
        uploadedImages: updatedThumbnails.length,
      });
      this._cdr.detectChanges();
    }
  }

  private async refreshUploadedImagesFromServer() {
    try {
      this.stateService.forceRefresh();
      this._cdr.detectChanges();
    } catch (error) {
      // Failed to refresh images - will be retried on next load
      this._notificationService.error('Refresh Failed', 'Failed to refresh image list from server');
    }
  }

  selectAllStyles() {
    this.availableStyles.forEach(style => (style.selected = true));
    this.selectedStyles = this.getSelectedStylesCount();
  }

  deselectAllStyles() {
    this.availableStyles.forEach(style => (style.selected = false));
    this.selectedStyles = this.getSelectedStylesCount();
  }

  toggleStyle(style: StyleOption) {
    style.selected = !style.selected;
    this.selectedStyles = this.getSelectedStylesCount();
  }

  onImagesPerStyleChanged(count: number) {
    this.imagesPerStyle = count;
  }

  private async loadWorkflowService(): Promise<void> {
    try {
      // Dynamically import the WorkflowOrchestrationService
      const { WorkflowOrchestrationService } = await import(
        '../services/workflow-orchestration.service'
      );

      // Get the service instance from the injector
      this.workflowService = this._injector.get(WorkflowOrchestrationService);

      // Subscribe to progress updates and forward them to our proxy observable
      this.workflowService.progress$.subscribe(progress => {
        this.workflowProgressSubject.next(progress);
      });
    } catch (error) {
      // Failed to load workflow service - critical error, propagate
      throw error;
    }
  }

  async startTrainingWithStyles() {
    const selectedStyles = this.availableStyles.filter(s => s.selected);

    this.isTrainingStarted = true;
    this.currentStep = 3;

    try {
      // Lazy load the WorkflowOrchestrationService
      if (!this.workflowService) {
        await this.loadWorkflowService();
      }

      if (this.workflowService) {
        await this.workflowService.startTrainingWithStyles(selectedStyles, this.imagesPerStyle);
      }
    } catch (error) {
      // Training workflow error handled by service notifications
      this.isTrainingStarted = false;
      this.currentStep = 2;
    }
  }

  // Workflow methods
  isPremiumWorkflow(): boolean {
    return true;
  }

  getStepStatus(step: number): string {
    return this._workflowStepService.getStepStatus(
      step,
      this.stateService.getState().uploadedImages,
      this.stateService.getState().uploadedImageThumbnails,
      this.stateService.getState().generatedPhotosCount,
      this.currentStep
    );
  }

  getStepStatusText(step: number): string {
    return this._workflowStepService.getStepStatusText(
      step,
      this.stateService.getState().uploadedImages,
      this.stateService.getState().uploadedImageThumbnails,
      this.stateService.getState().generatedPhotosCount,
      this.currentStep
    );
  }

  // Credit calculation methods - now handle lazy loading
  private getCreditCalculation() {
    const selectedStyles = this.availableStyles.filter(s => s.selected);

    // If workflow service is not loaded yet, return default values
    if (!this.workflowService) {
      return this.calculateCreditsLocally(
        selectedStyles,
        this.imagesPerStyle,
        this.stateService.getState().modelStatus
      );
    }

    return this.workflowService.calculateCredits(
      selectedStyles,
      this.imagesPerStyle,
      this.stateService.getState().modelStatus
    );
  }

  // Local credit calculation to avoid loading the service just for credit display
  private calculateCreditsLocally(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    modelStatus: string
  ) {
    const trainingCredits = this.calculateTrainingCreditsLocally(modelStatus);
    const generationCredits = this.calculateGenerationCreditsLocally(
      selectedStyles,
      imagesPerStyle
    );
    const totalCredits = trainingCredits + generationCredits;

    const availableCredits = this.getTotalAvailableCreditsLocally();
    const hasEnoughCredits = availableCredits >= totalCredits;
    const remainingCredits = availableCredits - totalCredits;

    return {
      trainingCredits,
      generationCredits,
      totalCredits,
      hasEnoughCredits,
      remainingCredits,
    };
  }

  private calculateTrainingCreditsLocally(modelStatus: string): number {
    if (modelStatus === 'Model Ready') {
      return 0; // Model already trained, no additional cost
    }
    return 15; // Training required - 15 credits
  }

  private calculateGenerationCreditsLocally(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): number {
    const generationCostPerImage = 5; // 5 credits per image generated
    const selectedStyleCount = selectedStyles.length;
    const totalImages = selectedStyleCount * imagesPerStyle;
    return totalImages * generationCostPerImage;
  }

  private getTotalAvailableCreditsLocally(): number {
    const userCreditStatus = this.stateService.getState().userCreditStatus;
    const creditsInfo = this.stateService.getState().creditsInfo;

    const weeklyCredits = userCreditStatus?.weeklyCredits || creditsInfo?.availableCredits || 0;
    const purchasedCredits = userCreditStatus?.purchasedCredits || 0;

    return weeklyCredits + purchasedCredits;
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
    this._notificationService.info(
      'Continuing in Background',
      "Training and generation will continue. We'll email you when your photos are ready."
    );
    // Navigate to gallery with refresh parameter to force reload
    this._router.navigate(['/app/gallery'], {
      queryParams: { refresh: Date.now() },
    });
  }

  // Method to handle dismissing success message
  dismissSuccessMessage() {
    if (this.workflowService) {
      this.workflowService.dismissSuccessMessage();
    } else {
      // If service not loaded, update local state
      this.workflowProgressSubject.next({
        ...this.workflowProgressSubject.value,
        showLastGenerationMessage: false,
        lastGenerationCount: 0,
      });
    }
  }
}
