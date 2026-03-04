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
import { BehaviorSubject, Observable, Subscription, interval, firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

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
import { NavigationService } from '../services/navigation.service';
import { environment } from '../../environments/environment';
import { DashboardState } from '../interfaces/service.interfaces';
import { FileUploadService, UnifiedModelStatusResponse } from '../services/file-upload.service';
import { IntentTrackingService, SignupIntent } from '../services/intent-tracking.service';
import { getHeadshotsWelcomeCopy } from '../services/signup-intent-personalization';
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

interface ActiveJobViewModel {
  kind: 'training' | 'generation' | 'error';
  title: string;
  message: string;
  progressPercent: number;
  etaText?: string;
  errorDetails?: string;
  creditImpact?: {
    chargedCredits: number;
    refundedCredits: number;
    netCredits: number;
  } | null;
  trainingRequestId?: string | null;
  predictionId?: string | null;
  showPurchaseCreditsAction?: boolean;
}

interface CreditCalculation {
  trainingCredits: number;
  generationCredits: number;
  totalCredits: number;
  hasEnoughCredits: boolean;
  remainingCredits: number;
}

interface DashboardCta {
  label: string;
  route: string;
}

interface WorkflowOrchestrationService {
  progress$: Observable<WorkflowProgress>;
  startTrainingWithStyles(selectedStyles: StyleOption[], imagesPerStyle: number): Promise<void>;
  calculateCredits(
    selectedStyles: StyleOption[],
    imagesPerStyle: number,
    modelStatus: string
  ): CreditCalculation;
  queueBackgroundGeneration(
    selectedStyles: StyleOption[],
    imagesPerStyle: number
  ): Promise<boolean>;
  dismissSuccessMessage(): void;
  pause(): void;
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
  activeJob: ActiveJobViewModel | null = null;

  // Component-specific state
  currentStep = 1;
  isTrainingStarted = false;
  imagesPerStyle = 2;
  availableStyles: StyleOption[] = [];
  selectedStyles = 0;
  signupIntent: SignupIntent | null = null;

  // Lazy-loaded service
  private _workflowService: WorkflowOrchestrationService | null = null;
  /** Guards duplicate "Continue in Background" clicks for the full training session.
   *  Reset in startTrainingWithStyles(). See also WorkflowOrchestrationService._backgroundGenerationQueued. */
  private _backgroundQueued = false;
  private readonly _subscriptions = new Subscription();
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
  private _activeJobPolling?: Subscription;
  private _intentCleared = false;
  private readonly _ctaConfig: Record<string, { primary: DashboardCta; secondary: DashboardCta }> =
    {
      headshots: {
        primary: { label: 'Start Training Your Model', route: '/pricing' },
        secondary: { label: 'Enhance a Photo First', route: '/app/enhance' },
      },
      enhance: {
        primary: { label: 'Enhance Your First Photo', route: '/app/enhance' },
        secondary: { label: 'Explore Premium Studio', route: '/pricing' },
      },
      pricing: {
        primary: { label: 'View Packages', route: '/pricing' },
        secondary: { label: 'Browse Examples', route: '/examples' },
      },
    };

  getTotalAvailableCredits(): number {
    const state = this.stateService.getState();
    return this.creditService.getTotalAvailableCredits(state.userCreditStatus, state.creditsInfo);
  }

  onCreditAction(event: { action: string; context?: string }): void {
    switch (event.action) {
      case 'purchase':
      case 'viewPackages':
        this._navigation.goToPricingPlans();
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
    private readonly _navigation: NavigationService,
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
    private readonly _logger: LoggingService,
    private readonly _fileUploadService: FileUploadService,
    private readonly _intentTracking: IntentTrackingService
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

    this.signupIntent = this._intentTracking.getIntent();

    // Subscribe to state changes to update UI
    this._subscriptions.add(
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

        if (!this._intentCleared && this.signupIntent && this.isFirstTimeUser()) {
          this._intentTracking.clearIntent();
          this._intentCleared = true;
        }

        // Force change detection for async updates
        this._cdr.detectChanges();
      })
    );

    this.stateService.loadInitialDashboardData();
    this._loadAvailableStyles();

    // Hydrate active job immediately and start polling only when needed.
    void this._refreshActiveJobFromServer();
  }
  ngOnDestroy(): void {
    if (this._workflowService) {
      this._workflowService.pause();
    }
    if (this._activeJobPolling) {
      this._activeJobPolling.unsubscribe();
      this._activeJobPolling = undefined;
    }
    this._subscriptions.unsubscribe();
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
    this._subscriptions.add(
      this._workflowService.progress$.subscribe(progress => {
        this._workflowProgressSubject.next(progress);
        this._hydrateActiveJobFromWorkflowProgress(progress);
      })
    );
  }

  async startTrainingWithStyles(): Promise<void> {
    const selectedStyles = this.availableStyles.filter(s => s.selected);

    this.isTrainingStarted = true;
    this._backgroundQueued = false; // Reset for new session
    this.currentStep = 3;
    this._setOptimisticActiveJob('training');

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
    return state.isPremiumWorkflow || this.getTotalAvailableCredits() > 0;
  }

  isFirstTimeUser(): boolean {
    const state = this.stateService.getState();
    return state.uploadedImages === 0 && state.generatedPhotosCount === 0;
  }

  showPersonalizedGetStarted(): boolean {
    return !!this.signupIntent && this.isFirstTimeUser();
  }

  isHeadshotsIntent(): boolean {
    return this.signupIntent?.ctaType === 'headshots';
  }

  isEnhanceIntent(): boolean {
    return this.signupIntent?.ctaType === 'enhance';
  }

  getWelcomeMessage(): string {
    if (!this.signupIntent) {
      return 'Welcome to AI Profile Photo Maker';
    }

    if (this.signupIntent.ctaType === 'headshots') {
      return getHeadshotsWelcomeCopy(this.signupIntent).title;
    }

    if (this.signupIntent.ctaType === 'enhance') {
      return "Let's enhance your photos!";
    }

    return 'Welcome to AI Profile Photo Maker';
  }

  getWelcomeDescription(): string {
    if (!this.signupIntent) {
      return 'Create stunning, professional AI-generated profile photos with our advanced technology';
    }

    if (this.signupIntent.ctaType === 'headshots') {
      return getHeadshotsWelcomeCopy(this.signupIntent).description;
    }

    if (this.signupIntent.ctaType === 'enhance') {
      return 'Jump into fast enhancements for your existing photos, then explore premium studio generation anytime.';
    }

    return 'Choose your best path below and use your 25 free credits to get results today.';
  }

  getPrimaryCta(): DashboardCta {
    const intentKey = this.signupIntent?.ctaType ?? 'pricing';
    return this._ctaConfig[intentKey].primary;
  }

  getSecondaryCta(): DashboardCta {
    const intentKey = this.signupIntent?.ctaType ?? 'pricing';
    return this._ctaConfig[intentKey].secondary;
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

    const availableCredits = userCreditStatus?.credits || (creditsInfo as any)?.credits || 0;

    return availableCredits;
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
    // Guard: stays true for the entire training session (reset in startTrainingWithStyles).
    // This prevents duplicate queue requests, not just rapid clicks.
    if (this._backgroundQueued) {
      return;
    }
    this._backgroundQueued = true;

    const selectedStyles = this.availableStyles.filter(s => s.selected);

    const queueWork = async () => {
      try {
        if (!this._workflowService) {
          await this._loadWorkflowService();
        }
        if (this._workflowService) {
          const success = await this._workflowService.queueBackgroundGeneration(
            selectedStyles,
            this.imagesPerStyle
          );
          if (!success) {
            this._backgroundQueued = false; // Sync with service flag — allow retry
          }
        }
      } catch (error) {
        this._backgroundQueued = false; // Allow retry on failure
        this._logger.error('Failed to queue background generation', error);
      }
    };

    void queueWork();

    this._notificationService.info(
      'Continuing in Background',
      "Training and generation will continue. You can safely leave this page — we'll email you when your photos are ready."
    );

    // Ensure a visible in-progress state immediately (avoids lingering "Training failed" from the last run).
    const progress = this._workflowProgressSubject.value;
    if (progress?.isTraining) {
      this._setOptimisticActiveJob('training');
    } else if (progress?.isGenerating) {
      this._setOptimisticActiveJob('generation');
    } else if (this.isTrainingStarted) {
      this._setOptimisticActiveJob('training');
    }

    // Keep the user on the dashboard so they can continue to see progress.
    window.scrollTo({ top: 0, behavior: 'smooth' });
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

  purchaseCredits(): void {
    this._navigation.goToPricingPlans();
  }

  private _startActiveJobPolling(): void {
    if (this._activeJobPolling) {
      return;
    }

    void this._refreshActiveJobFromServer();
    this._activeJobPolling = interval(15000).subscribe(() => {
      void this._refreshActiveJobFromServer();
    });
  }

  private _ensureActiveJobPolling(): void {
    const hasActiveJob =
      this.activeJob?.kind === 'training' || this.activeJob?.kind === 'generation';
    if (hasActiveJob) {
      this._startActiveJobPolling();
      return;
    }

    if (this._activeJobPolling) {
      this._activeJobPolling.unsubscribe();
      this._activeJobPolling = undefined;
    }
  }

  private _hydrateActiveJobFromWorkflowProgress(progress: WorkflowProgress): void {
    // When a training/generation starts in the current session, show the banner immediately
    // instead of waiting for the first server poll.
    if (!progress?.isTraining && !progress?.isGenerating) {
      return;
    }

    const kind: ActiveJobViewModel['kind'] = progress.isTraining ? 'training' : 'generation';
    const title = kind === 'training' ? 'Training in progress' : 'Generating photos';
    const defaultMessage =
      kind === 'training'
        ? 'Your AI model is training in the background.'
        : 'Your photos are being generated in the background.';

    const percent = Math.max(0, Math.min(100, Math.round(progress.progressPercentage || 0)));
    const etaText = progress.estimatedCompletion
      ? `Est. ${progress.estimatedCompletion}`
      : undefined;

    this.activeJob = {
      kind,
      title,
      message: progress.progressMessage || defaultMessage,
      progressPercent: percent,
      etaText,
      trainingRequestId: kind === 'training' ? progress.trainingId || null : undefined,
      creditImpact: null,
    };

    this._cdr.detectChanges();
    this._ensureActiveJobPolling();
  }

  private _setOptimisticActiveJob(kind: 'training' | 'generation'): void {
    const title = kind === 'training' ? 'Training in progress' : 'Generating photos';
    const message =
      kind === 'training'
        ? 'Your AI model is training in the background.'
        : 'Your photos are being generated in the background.';

    this.activeJob = {
      kind,
      title,
      message,
      progressPercent: 0,
      etaText: undefined,
      trainingRequestId: undefined,
      creditImpact: null,
    };

    this._cdr.detectChanges();
    this._ensureActiveJobPolling();
  }

  getModelStatusCardValue(state: any): string {
    if (this.activeJob?.kind === 'training') {
      return 'Training...';
    }
    if (this.activeJob?.kind === 'generation') {
      return 'Generating...';
    }
    if (this.activeJob?.kind === 'error') {
      return this.activeJob.title || 'Training failed';
    }
    return state?.modelStatus || 'Loading...';
  }

  private async _refreshActiveJobFromServer(): Promise<void> {
    const unified = await firstValueFrom(
      this._fileUploadService.getUnifiedModelStatus().pipe(catchError(() => of(null)))
    );

    if (!unified) {
      return;
    }

    const mapped = this._mapUnifiedStatusToActiveJob(unified);
    this.activeJob = mapped;

    // Also hydrate the existing workflow progress observable so the in-dashboard progress card can resume.
    this._workflowProgressSubject.next({
      ...this._workflowProgressSubject.value,
      isTraining: mapped?.kind === 'training',
      isGenerating: mapped?.kind === 'generation',
      progressPercentage:
        mapped?.kind === 'training' || mapped?.kind === 'generation' ? mapped.progressPercent : 0,
      progressMessage: mapped?.message || '',
      estimatedCompletion: mapped?.etaText || '',
      trainingId: mapped?.trainingRequestId || '',
    });

    this._cdr.detectChanges();
    this._ensureActiveJobPolling();
  }

  private _mapUnifiedStatusToActiveJob(
    unified: UnifiedModelStatusResponse
  ): ActiveJobViewModel | null {
    const creditImpact = unified.creditImpact ?? null;
    const current = unified.currentRequest ?? null;
    const generation = unified.generationStatus ?? null;

    const formatMinutesRemaining = (
      typicalMinutes: number,
      startIso?: string | null
    ): string | undefined => {
      if (!startIso) {
        return undefined;
      }
      const start = new Date(startIso).getTime();
      if (Number.isNaN(start)) {
        return undefined;
      }
      const elapsedMinutes = (Date.now() - start) / 1000 / 60;
      const remaining = Math.max(0, Math.ceil(typicalMinutes - elapsedMinutes));
      return remaining <= 1 ? 'Almost done' : `~${remaining} min remaining`;
    };

    if (unified.statusCode === 'Failed') {
      const rawErrorDetails = unified.reason || current?.errorMessage || 'Something went wrong.';
      const normalizedError = (rawErrorDetails || '').toLowerCase();

      const isReplicateInterrupted =
        normalizedError.includes('prediction interrupted') && normalizedError.includes('code: pa');

      // Some failures are not "training failed" but rather "the trained model is no longer available".
      // Adjust messaging to avoid confusing users about refunds/credits.
      const isModelMissing =
        normalizedError.includes('deleted from replicate') ||
        normalizedError.includes('no longer exists on replicate') ||
        normalizedError.includes('model was deleted') ||
        normalizedError.includes('deleted by retention policy') ||
        normalizedError.includes('retention policy') ||
        normalizedError.includes('expired');

      const isRetentionLifecycle =
        isModelMissing &&
        (normalizedError.includes('retention policy') || normalizedError.includes('expired'));

      const errorDetails = isModelMissing
        ? isRetentionLifecycle
          ? 'Your previous model was removed by the retention policy after images expired.'
          : 'Your trained model can’t be found anymore. It may have been deleted or expired.'
        : isReplicateInterrupted
          ? 'Replicate interrupted the training job (code: PA). This is on Replicate’s side; please retry. Your credits should be refunded.'
          : rawErrorDetails.length > 240
            ? `${rawErrorDetails.slice(0, 240)}…`
            : rawErrorDetails;

      return {
        kind: 'error',
        title: isRetentionLifecycle
          ? 'Model expired'
          : isModelMissing
            ? 'Model unavailable'
            : isReplicateInterrupted
              ? 'Training interrupted'
              : 'Training failed',
        message: isModelMissing
          ? isRetentionLifecycle
            ? 'Your model expired under the retention policy. Upload photos and retrain to generate new headshots.'
            : 'Your trained model is no longer available. You can retrain and generate photos again.'
          : 'Your model training did not complete.',
        progressPercent: 0,
        errorDetails,
        creditImpact: isModelMissing ? null : creditImpact,
        trainingRequestId: current?.trainingRequestId ?? null,
        showPurchaseCreditsAction: !isModelMissing,
      };
    }

    if (generation && (generation.status === 'failed' || generation.status === 'canceled')) {
      const errorDetails = generation.error || 'Photo generation failed.';
      return {
        kind: 'error',
        title: 'Generation failed',
        message: 'Your photo generation did not complete.',
        progressPercent: 0,
        errorDetails,
        creditImpact,
        predictionId: generation.predictionId ?? null,
        showPurchaseCreditsAction: true,
      };
    }

    if (unified.statusCode === 'Training') {
      const status = (current?.status || '').toLowerCase();
      const progressPercent = status === 'pending' ? 25 : status === 'creating' ? 50 : 75;
      const etaText = formatMinutesRemaining(12, current?.createdAt);
      return {
        kind: 'training',
        title: 'Training in progress',
        message: 'Your AI model is training in the background.',
        progressPercent,
        etaText,
        trainingRequestId: current?.trainingRequestId ?? null,
        creditImpact,
      };
    }

    if (
      unified.statusCode === 'Generating' ||
      generation?.status === 'queued' ||
      generation?.status === 'processing'
    ) {
      const startedAt = generation?.startedAt || null;
      const etaText = formatMinutesRemaining(8, startedAt);

      let progressPercent = 15;
      if (generation?.status === 'queued') {
        progressPercent = 10;
      } else if (generation?.status === 'processing') {
        const typicalMs = 8 * 60 * 1000;
        const startMs = startedAt ? new Date(startedAt).getTime() : 0;
        const elapsed = startMs ? Math.max(0, Date.now() - startMs) : 0;
        const ratio = typicalMs > 0 ? Math.min(elapsed / typicalMs, 0.85) : 0;
        progressPercent = Math.round(15 + ratio * 70);
      }

      return {
        kind: 'generation',
        title: 'Generating photos',
        message: 'Your photos are being generated in the background.',
        progressPercent,
        etaText,
        predictionId: generation?.predictionId ?? null,
        creditImpact,
      };
    }

    return null;
  }
}
