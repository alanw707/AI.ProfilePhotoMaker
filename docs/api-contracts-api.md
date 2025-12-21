# API Contracts - API

Deep scan based on controller attributes and routes.

## AdminController
- Base Route: `api/admin`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/admin/cleanup-orphaned-model` | CleanupOrphanedModel | unspecified |

## AuthController
- Base Route: `api/[controller]`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/Auth/register` | Register | unspecified |
| GET | `/api/Auth/account-status` | GetAccountStatus | required |
| POST | `/api/Auth/resend-confirmation-email` | ResendConfirmationEmail | required |
| POST | `/api/Auth/confirm-email` | ConfirmEmail | anonymous |
| GET | `/api/Auth/confirm-email` | ConfirmEmailRedirect | anonymous |
| POST | `/api/Auth/dev/confirm-email` | DevConfirmEmail | required |
| POST | `/api/Auth/login` | Login | unspecified |
| POST | `/api/Auth/logout` | Logout | unspecified |
| GET | `/api/Auth/google-oauth-url` | GetGoogleOAuthUrl | unspecified |
| GET | `/api/Auth/external-login/{provider}` | ExternalLogin | unspecified |
| GET | `/api/Auth/external-login-callback` | ExternalLoginCallback | unspecified |
| GET | `/api/Auth/validate-session` | ValidateSession | required |
| GET | `/api/Auth/profile-completion-status` | GetProfileCompletionStatus | required |
| POST | `/api/Auth/complete-profile` | CompleteProfile | required |
| POST | `/api/Auth/set-cookie` | SetCookie | unspecified |

## BaseController
- Base Route: `api/[controller]`
- Auth: unspecified

- No HTTP endpoints detected

## ConfigController
- Base Route: `api/[controller]`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/Config/replicate/status` | GetReplicateStatus | unspecified |
| GET | `/api/Config/client` | GetClientConfiguration | unspecified |
| GET | `/api/Config/status` | GetConfigurationStatus | unspecified |
| GET | `/api/Config/validate` | ValidateConfiguration | required |

## CreditController
- Base Route: `api/[controller]`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/Credit/status` | GetCreditStatus | required |
| GET | `/api/Credit/packages` | GetCreditPackages | anonymous |
| POST | `/api/Credit/purchase` | PurchaseCreditPackage | required |
| GET | `/api/Credit/history` | GetPurchaseHistory | required |
| POST | `/api/Credit/create-payment-intent` | CreatePaymentIntent | required |
| GET | `/api/Credit/costs` | GetCreditCosts | anonymous |
| GET | `/api/Credit/payment-config` | GetPaymentConfig | anonymous |

## DeploymentController
- Base Route: `api/[controller]`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/Deployment/validate/pre-deployment` | ValidatePreDeploymentAsync | unspecified |
| POST | `/api/Deployment/validate/post-deployment` | ValidatePostDeploymentAsync | unspecified |
| GET | `/api/Deployment/readiness-score` | GetReadinessScoreAsync | unspecified |
| GET | `/api/Deployment/validate/configuration` | ValidateConfigurationAsync | unspecified |
| GET | `/api/Deployment/validate/performance` | ValidatePerformanceAsync | unspecified |
| GET | `/api/Deployment/validate/security` | ValidateSecurityAsync | unspecified |
| GET | `/api/Deployment/validate/azure` | ValidateAzureServicesAsync | unspecified |
| GET | `/api/Deployment/validate/database` | ValidateDatabaseAsync | unspecified |
| POST | `/api/Deployment/validate/regression-tests` | RunRegressionTestsAsync | unspecified |
| GET | `/api/Deployment/health` | GetDeploymentHealthAsync | unspecified |
| GET | `/api/Deployment/monitor/configuration-drift` | DetectConfigurationDriftAsync | unspecified |
| GET | `/api/Deployment/monitor/service-availability` | ValidateServiceAvailabilityAsync | unspecified |
| GET | `/api/Deployment/monitor/performance-regression` | CheckPerformanceRegressionAsync | unspecified |
| POST | `/api/Deployment/monitor/reset-baseline` | ResetConfigurationBaselineAsync | required |
| GET | `/api/Deployment/summary` | GetDeploymentSummaryAsync | unspecified |

## DiagnosticController
- Base Route: `api/[controller]`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/Diagnostic/run-migrations` | RunMigrations | unspecified |
| POST | `/api/Diagnostic/reset-database` | ResetDatabase | unspecified |
| POST | `/api/Diagnostic/create-tables-sql` | CreateTablesWithRawSql | unspecified |
| POST | `/api/Diagnostic/fix-schema` | FixMissingColumns | unspecified |
| POST | `/api/Diagnostic/add-missing-columns` | AddMissingColumns | unspecified |
| POST | `/api/Diagnostic/migrate-descriptions` | MigrateDescriptions | unspecified |
| GET | `/api/Diagnostic/inspect-tables` | InspectTables | unspecified |
| POST | `/api/Diagnostic/fix-packages-simple` | FixPackagesSimple | unspecified |
| POST | `/api/Diagnostic/update-credit-descriptions` | UpdateCreditDescriptions | unspecified |
| POST | `/api/Diagnostic/populate-missing-styles` | PopulateMissingStyles | unspecified |
| POST | `/api/Diagnostic/populate-all-styles` | PopulateAllStyles | unspecified |
| GET | `/api/Diagnostic/database-status` | GetDatabaseStatus | unspecified |
| POST | `/api/Diagnostic/update-prompt-templates` | UpdatePromptTemplates | unspecified |
| POST | `/api/Diagnostic/fix-duplicate-subject` | FixDuplicateSubject | unspecified |
| GET | `/api/Diagnostic/model-requests` | GetModelRequests | unspecified |
| POST | `/api/Diagnostic/cleanup-model-requests` | CleanupModelRequests | unspecified |

## EnhancementController
- Base Route: `api/[controller]`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/Enhancement/health` | HealthCheck | anonymous |
| POST | `/api/Enhancement/enhance` | EnhancePhoto | required |

## FeedbackController
- Base Route: `api/[controller]`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/Feedback` | SubmitFeedback | required |

## ImageController
- Base Route: `api/[controller]`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/Image/reconcile-database` | ReconcileDatabase | required |
| GET | `/api/Image/styles` | GetStyles | required |
| POST | `/api/Image/upload` | UploadImages | required |
| GET | `/api/Image/images` | GetImages | required |
| DELETE | `/api/Image/images/{imageId}` | DeleteImage | required |
| POST | `/api/Image/create-training-zip` | CreateTrainingZip | required |
| GET | `/api/Image/training-zips` | GetTrainingZips | required |
| GET | `/api/Image/latest-training-zip` | GetLatestTrainingZip | required |
| DELETE | `/api/Image/training-zips/{fileName}` | DeleteTrainingZip | required |
| DELETE | `/api/Image/training-zips` | DeleteAllTrainingZips | required |
| GET | `/api/Image/diagnostic` | DiagnosticInfo | anonymous |
| POST | `/api/Image/save-enhanced` | SaveEnhancedImage | required |

## ModelCreationStatusController
- Base Route: `api/model-creation`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/model-creation/user/current` | GetCurrentUserModelRequests | required |
| GET | `/api/model-creation/status/{requestId}` | GetModelCreationStatus | required |
| GET | `/api/model-creation/debug/user/{userId}` | GetCurrentUserModelRequestsDebug | required |
| GET | `/api/model-creation/user/{userId}` | GetUserModelCreationRequests | required |

## ModelDiscoveryController
- Base Route: `api/model-discovery`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/model-discovery/sync` | DiscoverAndSyncModels | required |
| GET | `/api/model-discovery/check` | QuickModelCheck | required |
| GET | `/api/model-discovery/status` | GetModelSyncStatus | required |
| POST | `/api/model-discovery/sync-specific` | SyncSpecificModel | required |
| POST | `/api/model-discovery/override-deletion` | OverrideModelDeletion | required |

## ModelStatusController
- Base Route: `api/model-status`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/model-status/debug/{userId}` | GetDebug | required |
| GET | `/api/model-status` | Get | required |

## PlaceholderImageController
- Base Route: `api/placeholder`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/placeholder/style-preview` | GetStylePreviewPlaceholder | unspecified |

## ProfileController
- Base Route: `api/[controller]`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/Profile` | GetProfile | required |
| POST | `/api/Profile` | CreateProfile | required |
| PUT | `/api/Profile` | UpdateProfile | required |
| DELETE | `/api/Profile` | DeleteProfile | required |
| GET | `/api/Profile/styles` | GetStyles | required |
| POST | `/api/Profile/generate` | GenerateImages | required |
| GET | `/api/Profile/training-status` | GetTrainingStatus | required |
| GET | `/api/Profile/data-stats` | GetDataStats | required |
| DELETE | `/api/Profile/data/photos` | DeleteInputPhotos | required |
| DELETE | `/api/Profile/data/model` | DeleteAIModel | required |
| DELETE | `/api/Profile/data/all` | DeleteAllUserData | required |
| DELETE | `/api/Profile/account` | DeleteUserAccount | required |
| GET | `/api/Profile/data/export` | ExportUserData | required |

## ProfileManagementController
- Base Route: `api/profile-management`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/profile-management` | GetProfile | required |
| POST | `/api/profile-management` | CreateProfile | required |
| PUT | `/api/profile-management` | UpdateProfile | required |
| DELETE | `/api/profile-management` | DeleteProfile | required |

## StyleGenerationResult
- Base Route: `api/[controller]`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/StyleGenerationResult/train` | TrainModel | unspecified |
| POST | `/api/StyleGenerationResult/generate/queue` | QueueGeneration | unspecified |
| GET | `/api/StyleGenerationResult/train/status/{trainingId}` | GetTrainingStatus | unspecified |
| POST | `/api/StyleGenerationResult/train/finalize/{trainingId}` | FinalizeTraining | unspecified |
| POST | `/api/StyleGenerationResult/generate` | GenerateImages | unspecified |
| POST | `/api/StyleGenerationResult/generate/batch` | GenerateBatchImages | unspecified |
| GET | `/api/StyleGenerationResult/generate/status/{predictionId}` | GetPredictionStatus | unspecified |
| GET | `/api/StyleGenerationResult/model/availability/{modelId}` | CheckModelAvailability | unspecified |
| GET | `/api/StyleGenerationResult/credits` | GetCredits | unspecified |
| POST | `/api/StyleGenerationResult/enhance` | EnhancePhoto | unspecified |
| GET | `/api/StyleGenerationResult/health` | HealthCheck | unspecified |

## ReplicateWebhookController
- Base Route: `api/webhooks/replicate`
- Auth: anonymous

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/webhooks/replicate/prediction-complete` | PredictionComplete | anonymous |

## RetentionPolicyController
- Base Route: `api/[controller]`
- Auth: required

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/RetentionPolicy/expired-images` | GetExpiredImages | required |
| POST | `/api/RetentionPolicy/delete-expired` | DeleteExpiredImages | required |
| POST | `/api/RetentionPolicy/initialize-retention-dates` | InitializeRetentionDates | required |
| GET | `/api/RetentionPolicy/policy-info` | GetPolicyInfo | required |

## StripeWebhookController
- Base Route: `api/webhooks/stripe`
- Auth: anonymous

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| POST | `/api/webhooks/stripe` | Receive | anonymous |

## StyleController
- Base Route: `api/[controller]`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/Style` | GetStyles | unspecified |
| GET | `/api/Style/{id}` | GetStyle | unspecified |
| GET | `/api/Style/{id}/template` | GetStyleTemplate | unspecified |
| GET | `/api/Style/name/{name}/template` | GetStyleTemplateByName | unspecified |
| POST | `/api/Style/select` | SelectStyles | required |
| GET | `/api/Style/user-selected` | GetUserSelectedStyles | required |
| POST | `/api/Style` | CreateStyle | required |
| PUT | `/api/Style/{id}` | UpdateStyle | required |
| DELETE | `/api/Style/{id}` | DeleteStyle | required |

## StylePreviewController
- Base Route: `api/[controller]`
- Auth: unspecified

| Method | Path | Action | Auth |
| --- | --- | --- | --- |
| GET | `/api/StylePreview/url/{styleName}` | GetStylePreviewUrl | unspecified |
| GET | `/api/StylePreview/list` | ListStylePreviews | unspecified |

