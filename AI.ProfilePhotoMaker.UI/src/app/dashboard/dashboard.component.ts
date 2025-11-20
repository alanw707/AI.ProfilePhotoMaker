import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  Injector,
  OnDestroy,
  OnInit,
} from '@angular/core';
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
import { ModelStatusService } from '../services/model-status.service';
import { ConfigService } from '../services/config.service';
import { StylePreviewService } from '../services/style-preview.service';
import { WorkflowStepService, ImageThumbnail } from '../services/workflow-step.service';
import { LoggingService, LogLevel } from '../services/logging.service';
import { environment } from '../../environments/environment';
import { DashboardState } from '../interfaces/service.interfaces';
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

interface CreditCalculation {
  trainingCredits: number;
  generationCredits: number;
  totalCredits: number;
  hasEnoughCredits: boolean;
  remainingCredits: number;
}

interface WorkflowOrchestrationService {
  progress$: Observable<WorkflowProgress>;
  startTrainingWithStyles(selectedStyles: StyleOption[], imagesPerStyle: number): Promise<void>;
  calculateCredits(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    modelStatus: string
  ): CreditCalculation;
  dismissSuccessMessage(): void;
  dispose(): void;
}

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
  state$: Observable<DashboardState>;
  workflowProgress$: Observable<WorkflowProgress>;

  // Component-specific state
  currentStep = 1;
  isTrainingStarted = false;
  imagesPerStyle = 2;
  availableStyles: StyleOption[] = [];
  selectedStyles = 0;

  // Lazy-loaded service
  private _workflowService: WorkflowOrchestrationService | null = null;
  private _workflowProgressSubject = new BehaviorSubject<WorkflowProgress>({
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
    const state = this.stateService.getState();
    return this.creditService.getTotalAvailableCredits(state.userCreditStatus, state.creditsInfo);
  }

  getPurchasedCredits(): number {
    const userCreditStatus = this.stateService.getState().userCreditStatus;
    return this.creditService.getPurchasedCredits(userCreditStatus);
  }

  getWeeklyCredits(): number {
    const state = this.stateService.getState();
    return this.creditService.getWeeklyCredits(state.userCreditStatus, state.creditsInfo);
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
    private readonly _authService: AuthService,
    private readonly _router: Router,
    private readonly _styleService: StyleService,
    private readonly _notificationService: NotificationService,
    public readonly creditService: CreditService,
    public readonly stateService: DashboardCoordinatorService,
    private readonly _modelStatus: ModelStatusService,
    private readonly _config: ConfigService,
    private readonly _stylePreviewService: StylePreviewService,
    private readonly _workflowStepService: WorkflowStepService,
    private readonly _injector: Injector,
    private readonly _cdr: ChangeDetectorRef,
    private readonly _logger: LoggingService
  ) {
    this.state$ = this.stateService.state$;
    this.workflowProgress$ = this._workflowProgressSubject.asObservable();
  }

  // Transform uploaded image thumbnails to match the expected format
  getTransformedUploadedImageThumbnails(): { id: string; url: string; name: string }[] {
    const thumbnails = this.stateService.getState().uploadedImageThumbnails;
    return thumbnails.map(thumb => ({
      id: thumb.id.toString(),
      url: thumb.url,
      name: thumb.fileName,
    }));
  }

  ngOnInit(): void {
    if (!this._authService.isAuthenticated()) {
      this._router.navigate(['/auth/login']);
      return;
    }

    // Subscribe to state changes to update UI
    this.state$.subscribe(state => {
      // Debug logging for troubleshooting (development only)
      this._logger.conditionalLog(
        environment.features.logging?.enableDashboardDebug ?? false,
        LogLevel.DEBUG,
        'Dashboard state updated',
        {
          userProfile: !!state.userProfile,
          creditsInfo: !!state.creditsInfo,
          userCreditStatus: !!state.userCreditStatus,
          uploadedImages: state.uploadedImages,
          generatedPhotosCount: state.generatedPhotosCount,
          modelStatus: state.modelStatus,
          isPremiumWorkflow: state.isPremiumWorkflow,
          isLoading: state.isLoading,
        }
      );

      // Force change detection when state updates
      this.selectedStyles = this.getSelectedStylesCount();

      // Update current step based on progress
      this._updateCurrentStep();

      // Force change detection for async updates
      this._cdr.detectChanges();
    });

    this.stateService.loadInitialDashboardData();
    this._loadAvailableStyles();
  }
  ngOnDestroy(): void {
    this.stateService.resetState();
    if (this._workflowService) {
      this._workflowService.dispose();
    }
    this._workflowProgressSubject.complete();
  }
  private _updateCurrentStep(): void {
    const state = this.stateService.getState();
    const convertedThumbnails: ImageThumbnail[] = state.uploadedImageThumbnails.map(thumb => ({
      id: thumb.id.toString(),
      name: thumb.fileName,
      url: thumb.url,
    }));
    this.currentStep = this._workflowStepService.updateCurrentStep(
      state.uploadedImages,
      convertedThumbnails,
      state.generatedPhotosCount,
      this.currentStep
    );
  }
  private _loadAvailableStyles(): void {
    this._styleService.getActiveStyles().subscribe({
      next: response => {
        if (response.success && response.data) {
          this.availableStyles = response.data.map(style => ({
            id: style.id.toString(),
            name: style.name,
            description: style.description,
            previewUrl: this._getStylePreviewUrl(style.name),
            selected: false,
          }));
        } else {
          // Failed to load styles - error handled by notification
          this._handleStyleLoadError();
        }
      },
      error: _error => {
        // Error loading styles - handled by notification
        this._handleStyleLoadError();
      },
    });
  }
  private _handleStyleLoadError(): void {
    this._notificationService.error(
      'Style Load Failed',
      'Could not load available styles. Please refresh the page.'
    );
  }

  private _handleRefreshError(): void {
    this._notificationService.error('Refresh Failed', 'Failed to refresh image list from server');
  }

  private _getStylePreviewUrl(styleName: string): string {
    return this._stylePreviewService.getCachedUrl(styleName);
  }

  // UI Event Handlers
  onUploadCompleted(_uploadedFiles: unknown[]): void {
    this._refreshUploadedImagesFromServer();

    setTimeout(() => {
      const thumbnails = this.stateService.getState().uploadedImageThumbnails;
      if (thumbnails.length >= 10) {
        this.currentStep = 2;
        this._cdr.detectChanges();
      }
    }, 1000);
  }

  onUploadedImageDeleted(event: {
    thumb: unknown;
    index: number;
    refreshRequired?: boolean;
  }): void {
    const { thumb, index, refreshRequired } = event;

    if (refreshRequired) {
      // For stale references, immediately remove from UI and refresh from server
      this._handleStaleImageReference(index);
      return;
    }

    // Handle both string and number IDs robustly
    const rawId = (thumb as any)?.id;
    const idNum = typeof rawId === 'number' ? rawId : parseInt(String(rawId), 10);
    if (!isNaN(idNum)) {
      this._removeImageById(idNum);
    } else {
      // If ID is invalid, do a full refresh to stay in sync
      this._refreshUploadedImagesFromServer();
    }
  }

  private _handleStaleImageReference(index: number): void {
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
    this._refreshUploadedImagesFromServer();
  }

  private _removeImageById(id: number): void {
    const currentThumbnails = this.stateService.getState().uploadedImageThumbnails;
    const updatedThumbnails = currentThumbnails.filter(t => t.id !== id);
    this.stateService.setState({
      uploadedImageThumbnails: updatedThumbnails,
      uploadedImages: updatedThumbnails.length,
    });
    this._cdr.detectChanges();
  }

  private async _refreshUploadedImagesFromServer(): Promise<void> {
    try {
      this.stateService.forceRefresh();
      this._cdr.detectChanges();
    } catch {
      // Failed to refresh images - will be retried on next load
      this._handleRefreshError();
    }
  }

  selectAllStyles(): void {
    this.availableStyles.forEach(style => (style.selected = true));
    this.selectedStyles = this.getSelectedStylesCount();
  }

  deselectAllStyles(): void {
    this.availableStyles.forEach(style => (style.selected = false));
    this.selectedStyles = this.getSelectedStylesCount();
  }

  toggleStyle(style: StyleOption): void {
    style.selected = !style.selected;
    this.selectedStyles = this.getSelectedStylesCount();
  }

  onImagesPerStyleChanged(count: number): void {
    this.imagesPerStyle = count;
  }

  private async _loadWorkflowService(): Promise<void> {
    // Dynamically import the WorkflowOrchestrationService
    const { WorkflowOrchestrationService: workflowOrchestrationServiceClass } = await import(
      '../services/workflow-orchestration.service'
    );

    // Get the service instance from the injector
    this._workflowService = this._injector.get(workflowOrchestrationServiceClass);

    // Subscribe to progress updates and forward them to our proxy observable
    this._workflowService.progress$.subscribe(progress => {
      this._workflowProgressSubject.next(progress);
    });
  }

  async startTrainingWithStyles(): Promise<void> {
    const selectedStyles = this.availableStyles.filter(s => s.selected);

    this.isTrainingStarted = true;
    this.currentStep = 3;

    try {
      // Lazy load the WorkflowOrchestrationService
      if (!this._workflowService) {
        await this._loadWorkflowService();
      }

      if (this._workflowService) {
        await this._workflowService.startTrainingWithStyles(selectedStyles, this.imagesPerStyle);
      }
    } catch {
      // Training workflow error handled by service notifications
      this.isTrainingStarted = false;
      this.currentStep = 2;
    }
  }

  // Workflow methods
  isPremiumWorkflow(): boolean {
    const state = this.stateService.getState();
    // User has premium workflow if they have purchased credits, weekly credits, or are marked as premium
    return (
      state.isPremiumWorkflow ||
      (state.userCreditStatus?.purchasedCredits || 0) > 0 ||
      (state.userCreditStatus?.weeklyCredits || 0) > 0
    );
  }

  getStepStatus(step: number): string {
    const state = this.stateService.getState();
    const convertedThumbnails: ImageThumbnail[] = state.uploadedImageThumbnails.map(thumb => ({
      id: thumb.id.toString(),
      name: thumb.fileName,
      url: thumb.url,
    }));
    return this._workflowStepService.getStepStatus(
      step,
      state.uploadedImages,
      convertedThumbnails,
      state.generatedPhotosCount,
      this.currentStep
    );
  }

  getStepStatusText(step: number): string {
    const state = this.stateService.getState();
    const convertedThumbnails: ImageThumbnail[] = state.uploadedImageThumbnails.map(thumb => ({
      id: thumb.id.toString(),
      name: thumb.fileName,
      url: thumb.url,
    }));
    return this._workflowStepService.getStepStatusText(
      step,
      state.uploadedImages,
      convertedThumbnails,
      state.generatedPhotosCount,
      this.currentStep
    );
  }

  // Credit calculation methods - now handle lazy loading
  private _getCreditCalculation(): CreditCalculation {
    const selectedStyles = this.availableStyles.filter(s => s.selected);

    // If workflow service is not loaded yet, return default values
    if (!this._workflowService) {
      return this._calculateCreditsLocally(selectedStyles, this.imagesPerStyle);
    }

    return this._workflowService.calculateCredits(
      selectedStyles,
      this.imagesPerStyle,
      this.stateService.getState().modelStatus
    );
  }

  // Local credit calculation to avoid loading the service just for credit display
  private _calculateCreditsLocally(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): CreditCalculation {
    const modelStatus = this.stateService.getState().modelStatus;
    const trainingCredits = this._calculateTrainingCreditsLocally(modelStatus);
    const generationCredits = this._calculateGenerationCreditsLocally(
      selectedStyles,
      imagesPerStyle
    );
    const totalCredits = trainingCredits + generationCredits;

    const availableCredits = this._getTotalAvailableCreditsLocally();
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

  private _calculateTrainingCreditsLocally(modelStatusStr: string): number {
    // Prefer semantic status when available
    const semantic = this.getSemanticStatus();
    if (semantic) {
      return semantic.canGenerate ? 0 : 15;
    }
    // Fallback immediately to string-based readiness to avoid charging training when model is ready
    return this._modelStatus.canGenerate(modelStatusStr) ? 0 : 15;
  }

  // Helper method to get semantic status from current state
  private getSemanticStatus() {
    const currentState = this.stateService.getState();
    return currentState.modelStatusSemantic;
  }

  // Template helper methods using semantic status instead of string comparisons
  // These replace complex ngIf conditions with semantic capability checks

  get canStartTraining(): boolean {
    const semantic = this.getSemanticStatus();
    return semantic?.canTrain ?? false;
  }

  get canGenerateImages(): boolean {
    const semantic = this.getSemanticStatus();
    return semantic?.canGenerate ?? false;
  }

  get isModelTraining(): boolean {
    const semantic = this.getSemanticStatus();
    return semantic ? semantic.state === 'TRAINING' : false;
  }

  get modelDisplayText(): string {
    const semantic = this.getSemanticStatus();
    return semantic?.displayText ?? 'Loading...';
  }

  private _calculateGenerationCreditsLocally(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): number {
    const generationCostPerImage = 5; // 5 credits per image generated
    const selectedStyleCount = selectedStyles.length;
    const totalImages = selectedStyleCount * imagesPerStyle;
    return totalImages * generationCostPerImage;
  }

  private _getTotalAvailableCreditsLocally(): number {
    const state = this.stateService.getState();
    const { userCreditStatus, creditsInfo } = state;

    // Paywall: generation/training require purchased credits
    const purchasedCredits =
      userCreditStatus?.purchasedCredits ||
      (creditsInfo as any)?.purchasedCredits ||
      0;

    return purchasedCredits;
  }

  calculateTotalCredits(): number {
    return this._getCreditCalculation().totalCredits;
  }

  hasEnoughCredits(): boolean {
    return this._getCreditCalculation().hasEnoughCredits;
  }

  getRemainingCredits(): number {
    return this._getCreditCalculation().remainingCredits;
  }

  getSelectedStylesCount(): number {
    return this.availableStyles.filter(s => s.selected).length;
  }

  calculateTrainingCredits(): number {
    return this._getCreditCalculation().trainingCredits;
  }

  calculateGenerationCredits(): number {
    return this._getCreditCalculation().generationCredits;
  }

  continueInBackground(): void {
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
  dismissSuccessMessage(): void {
    if (this._workflowService) {
      this._workflowService.dismissSuccessMessage();
    } else {
      // If service not loaded, update local state
      this._workflowProgressSubject.next({
        ...this._workflowProgressSubject.value,
        showLastGenerationMessage: false,
        lastGenerationCount: 0,
      });
    }
  }
}
