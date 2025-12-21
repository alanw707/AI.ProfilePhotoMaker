# API Contracts - UI

Deep scan based on Angular services and detected endpoint literals.

## Services
- ImageUrlService (`AI.ProfilePhotoMaker.UI/src/app/services/image-url.service.ts`) - HttpClient: no
- FaceDetectionService (`AI.ProfilePhotoMaker.UI/src/app/services/face-detection.service.ts`) - HttpClient: no
- StylePreviewService (`AI.ProfilePhotoMaker.UI/src/app/services/style-preview.service.ts`) - HttpClient: yes
- NotificationService (`AI.ProfilePhotoMaker.UI/src/app/services/notification.service.ts`) - HttpClient: no
- LoggingService (`AI.ProfilePhotoMaker.UI/src/app/services/logging.service.ts`) - HttpClient: no
- DashboardStateService (`AI.ProfilePhotoMaker.UI/src/app/services/dashboard-state.service.ts`) - HttpClient: no
- ProfileService (`AI.ProfilePhotoMaker.UI/src/app/services/profile.service.ts`) - HttpClient: yes
- SubscriptionStateService (`AI.ProfilePhotoMaker.UI/src/app/services/subscription-state.service.ts`) - HttpClient: no
- state-base.service (`AI.ProfilePhotoMaker.UI/src/app/services/state-base.service.ts`) - HttpClient: no
- CacheManagerService (`AI.ProfilePhotoMaker.UI/src/app/services/cache-manager.service.ts`) - HttpClient: no
- FileUploadManagerService (`AI.ProfilePhotoMaker.UI/src/app/services/file-upload-manager.service.ts`) - HttpClient: no
- BaseHttpService (`AI.ProfilePhotoMaker.UI/src/app/services/base-http.service.ts`) - HttpClient: yes
- ModelStatusMapperService (`AI.ProfilePhotoMaker.UI/src/app/services/model-status-mapper.service.ts`) - HttpClient: no
- DashboardCoordinatorService (`AI.ProfilePhotoMaker.UI/src/app/services/dashboard-coordinator.service.ts`) - HttpClient: no
- WorkflowOrchestrationService (`AI.ProfilePhotoMaker.UI/src/app/services/workflow-orchestration.service.ts`) - HttpClient: no
- ImageValidationService (`AI.ProfilePhotoMaker.UI/src/app/services/image-validation.service.ts`) - HttpClient: no
- ModelStateService (`AI.ProfilePhotoMaker.UI/src/app/services/model-state.service.ts`) - HttpClient: no
- ReplicateService (`AI.ProfilePhotoMaker.UI/src/app/services/replicate.service.ts`) - HttpClient: yes
  - `/api/enhancement/enhance`
- ModelStatusService (`AI.ProfilePhotoMaker.UI/src/app/services/model-status.service.ts`) - HttpClient: no
- ImageStateService (`AI.ProfilePhotoMaker.UI/src/app/services/image-state.service.ts`) - HttpClient: no
- CreditService (`AI.ProfilePhotoMaker.UI/src/app/services/credit.service.ts`) - HttpClient: yes
- FileUploadService (`AI.ProfilePhotoMaker.UI/src/app/services/file-upload.service.ts`) - HttpClient: yes
- StyleService (`AI.ProfilePhotoMaker.UI/src/app/services/style.service.ts`) - HttpClient: yes
- ModelLoaderService (`AI.ProfilePhotoMaker.UI/src/app/services/model-loader.service.ts`) - HttpClient: no
  - `https://cdn.jsdelivr.net/npm/@vladmandic/face-api/model`
- WorkflowStepService (`AI.ProfilePhotoMaker.UI/src/app/services/workflow-step.service.ts`) - HttpClient: no
- ImageQualityService (`AI.ProfilePhotoMaker.UI/src/app/services/image-quality.service.ts`) - HttpClient: no
- DashboardService (`AI.ProfilePhotoMaker.UI/src/app/services/dashboard.service.ts`) - HttpClient: no
- ThemeService (`AI.ProfilePhotoMaker.UI/src/app/services/theme.service.ts`) - HttpClient: no
- AuthService (`AI.ProfilePhotoMaker.UI/src/app/services/auth.service.ts`) - HttpClient: yes
- NavigationService (`AI.ProfilePhotoMaker.UI/src/app/services/navigation.service.ts`) - HttpClient: no
- FileSecurityService (`AI.ProfilePhotoMaker.UI/src/app/services/file-security.service.ts`) - HttpClient: no
- FeedbackService (`AI.ProfilePhotoMaker.UI/src/app/services/feedback.service.ts`) - HttpClient: yes
- ConfigService (`AI.ProfilePhotoMaker.UI/src/app/services/config.service.ts`) - HttpClient: no
  - `)) {
      return `${apiUrl}/${cleanEndpoint}`;
    }

    // Otherwise, use relative path (development with proxy)
    return `/api/${cleanEndpoint}`;
  }

  /**
   * Get the OAuth base URL for external login
   * CRITICAL FIX: Use baseUrl from environment for OAuth redirect URI generation
   */
  getOAuthBaseUrl(): string {
    // For production, use the explicit baseUrl from environment
    // This ensures OAuth redirects go to the correct backend domain
    if (environment.baseUrl) {
      return environment.baseUrl;
    }

    // For production/external configuration, use the backend API URL
    if (environment.apiUrl?.startsWith(`
  - `/api/`
- FallbackOperationsService (`AI.ProfilePhotoMaker.UI/src/app/services/fallback-operations.service.ts`) - HttpClient: yes

## Notes
- Many services build URLs from environment base URLs; check `AI.ProfilePhotoMaker.UI/src/environments/` for runtime base values.
