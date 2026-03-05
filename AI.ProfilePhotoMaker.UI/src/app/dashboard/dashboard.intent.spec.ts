import { ChangeDetectorRef, Injector } from '@angular/core';
import { Router } from '@angular/router';
import { of } from 'rxjs';

import { DashboardComponent } from './dashboard.component';
import { SignupIntent } from '../services/intent-tracking.service';

describe('DashboardComponent intent personalization', () => {
  const baseState = {
    userProfile: null,
    creditsInfo: { credits: 0 },
    userCreditStatus: { credits: 0 },
    uploadedImages: 0,
    uploadedImageThumbnails: [],
    generatedPhotosCount: 0,
    modelStatus: 'pending',
    isPremiumWorkflow: false,
    isLoading: false,
    modelStatusSemantic: null,
  };

  const createComponent = (overrides?: { intent?: SignupIntent | null; state?: any }) => {
    const state = { ...baseState, ...(overrides?.state ?? {}) };

    const authService = jasmine.createSpyObj('AuthService', ['isAuthenticated']);
    authService.isAuthenticated.and.returnValue(true);

    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    const navigation = jasmine.createSpyObj('NavigationService', ['goToPricingPlans']);
    const styleService = jasmine.createSpyObj('StyleService', ['getActiveStyles']);
    styleService.getActiveStyles.and.returnValue(of({ success: true, data: [] }) as any);
    const notification = jasmine.createSpyObj('NotificationService', ['error', 'info']);
    const creditService = jasmine.createSpyObj('CreditService', ['getTotalAvailableCredits']);
    creditService.getTotalAvailableCredits.and.returnValue(0);

    const stateService = jasmine.createSpyObj(
      'DashboardCoordinatorService',
      ['getState', 'loadInitialDashboardData', 'setState', 'forceRefresh'],
      {
        state$: of(state),
      }
    );
    stateService.getState.and.returnValue(state);

    const modelStatus = jasmine.createSpyObj('ModelStatusService', ['canGenerate']);
    modelStatus.canGenerate.and.returnValue(false);
    const config = jasmine.createSpyObj('ConfigService', ['getApiUrl']);
    const stylePreview = jasmine.createSpyObj('StylePreviewService', ['getCachedUrl']);
    stylePreview.getCachedUrl.and.returnValue('preview.jpg');

    const workflowStepService = jasmine.createSpyObj('WorkflowStepService', [
      'updateCurrentStep',
      'getStepStatus',
      'getStepStatusText',
    ]);
    workflowStepService.updateCurrentStep.and.returnValue(1);
    workflowStepService.getStepStatus.and.returnValue('pending');
    workflowStepService.getStepStatusText.and.returnValue('Pending');

    const injector = jasmine.createSpyObj<Injector>('Injector', ['get']);
    const cdr = jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', [
      'detectChanges',
      'markForCheck',
    ]);
    const logger = jasmine.createSpyObj('LoggingService', ['conditionalLog', 'error']);
    const fileUpload = jasmine.createSpyObj('FileUploadService', ['getUnifiedModelStatus']);
    fileUpload.getUnifiedModelStatus.and.returnValue(of(null));
    const intentTracking = jasmine.createSpyObj('IntentTrackingService', [
      'getIntent',
      'clearIntent',
    ]);
    intentTracking.getIntent.and.returnValue(overrides?.intent ?? null);

    const component = new DashboardComponent(
      authService,
      router,
      navigation,
      styleService,
      notification,
      creditService,
      stateService,
      modelStatus,
      config,
      stylePreview,
      workflowStepService,
      injector,
      cdr,
      logger,
      fileUpload,
      intentTracking
    );

    return { component, intentTracking, navigation };
  };

  it('identifies first-time users', () => {
    const { component } = createComponent();
    expect(component.isFirstTimeUser()).toBeTrue();
  });

  it('uses personalized welcome for linkedin headshots', () => {
    const { component } = createComponent({
      intent: {
        sourcePage: 'linkedin-headshots',
        ctaType: 'headshots',
        timestamp: Date.now(),
      },
    });

    component.signupIntent = {
      sourcePage: 'linkedin-headshots',
      ctaType: 'headshots',
      timestamp: Date.now(),
    };

    expect(component.getWelcomeMessage()).toBe('Ready for your LinkedIn headshots?');
    expect(component.getWelcomeDescription()).toContain('LinkedIn');
    expect(component.getPrimaryCta().label).toBe('Start Training Your Model');
  });

  it('uses source-aware welcome copy for legal headshots', () => {
    const { component } = createComponent({
      intent: {
        sourcePage: 'lawyer-headshots',
        ctaType: 'headshots',
        timestamp: Date.now(),
      },
    });

    component.signupIntent = {
      sourcePage: 'lawyer-headshots',
      ctaType: 'headshots',
      timestamp: Date.now(),
    };

    expect(component.getWelcomeMessage()).toBe('Ready for your legal profile headshots?');
    expect(component.getWelcomeDescription()).toContain('law firm');
  });

  it('clears intent after personalized first-time render path', () => {
    const { component, intentTracking } = createComponent({
      intent: {
        sourcePage: 'professional-headshots',
        ctaType: 'headshots',
        timestamp: Date.now(),
      },
    });

    component.ngOnInit();

    expect(intentTracking.clearIntent).toHaveBeenCalled();
  });

  it('routes upgrade credit action to pricing plans', () => {
    const { component, navigation } = createComponent();

    component.onCreditAction({ action: 'upgrade' });

    expect(navigation.goToPricingPlans).toHaveBeenCalled();
  });

  it('routes out-of-credits modal upgrade to pricing plans', () => {
    const { component, navigation } = createComponent();

    component.showOutOfCreditsModal = true;
    component.onOutOfCreditsUpgradeClicked();

    expect(component.showOutOfCreditsModal).toBeFalse();
    expect(navigation.goToPricingPlans).toHaveBeenCalled();
  });
});
