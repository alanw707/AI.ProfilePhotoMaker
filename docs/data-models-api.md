# Data Models - API

Deep scan based on EF Core DbContext and model class properties.

## DbContext Sets
- UserProfiles: `UserProfile`
- ProcessedImages: `ProcessedImage`
- Styles: `Style`
- UserStyleSelections: `UserStyleSelection`
- ModelCreationRequests: `ModelCreationRequest`
- UsageLogs: `UsageLog`
- Predictions: `Prediction`
- PendingGenerationRequests: `PendingGenerationRequest`
- SubscriptionPlans: `SubscriptionPlan`
- Subscriptions: `Subscription`
- PaymentTransactions: `PaymentTransaction`
- FeedbackSubmissions: `FeedbackSubmission`
- CreditPackages: `CreditPackage`
- CreditPurchases: `CreditPurchase`

## Model Properties
### ApplicationUser
- File: `AI.ProfilePhotoMaker.API/Models/ApplicationUser.cs`
- Properties:
  - FirstName: string
  - LastName: string
  - CreatedAt: DateTime

### CreditCostConfig
- File: `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs`
- Properties: none detected

### CreditPackage
- File: `AI.ProfilePhotoMaker.API/Models/CreditPackage.cs`
- Properties:
  - Id: int
  - Name: string
  - Credits: int
  - Price: decimal
  - Description: string
  - IsActive: bool
  - DisplayOrder: int
  - BonusCredits: int
  - StripeProductId: string?
  - StripePriceId: string?
  - CreatedAt: DateTime
  - UpdatedAt: DateTime?

### CreditPurchase
- File: `AI.ProfilePhotoMaker.API/Models/CreditPurchase.cs`
- Properties:
  - Id: int
  - UserId: string
  - PackageId: int
  - PurchaseDate: DateTime
  - CreditsAwarded: int
  - AmountPaid: decimal
  - PaymentTransactionId: string?
  - PaymentProvider: string
  - ExternalTransactionId: string?
  - Status: PaymentStatus
  - CompletedAt: DateTime?

### FeedbackSubmission
- File: `AI.ProfilePhotoMaker.API/Models/FeedbackSubmission.cs`
- Properties:
  - Id: Guid
  - UserId: string
  - User: ApplicationUser
  - Category: string
  - Message: string
  - PageUrl: string?
  - UserAgent: string?
  - CreatedAtUtc: DateTime

### ModelCreationRequest
- File: `AI.ProfilePhotoMaker.API/Models/ModelCreationRequest.cs`
- Properties:
  - Id: string
  - UserId: string
  - ModelName: string
  - ReplicateModelId: string?
  - TrainedModelVersion: string?
  - Status: ModelCreationStatus
  - TrainingImageZipUrl: string?
  - CreatedAt: DateTime
  - CompletedAt: DateTime?
  - ErrorMessage: string?
  - PendingTrainingRequestId: string?

### PaymentTransaction
- File: `AI.ProfilePhotoMaker.API/Models/PaymentTransaction.cs`
- Properties:
  - Id: int
  - UserId: string
  - User: ApplicationUser
  - SubscriptionId: int?
  - Subscription: Subscription?
  - ExternalTransactionId: string
  - Amount: decimal
  - Currency: string
  - PaymentProvider: string
  - Status: PaymentStatus
  - Type: PaymentType
  - Description: string?
  - FailureReason: string?
  - ProcessedAt: DateTime?
  - CreatedAt: DateTime
  - UpdatedAt: DateTime

### PendingGenerationRequest
- File: `AI.ProfilePhotoMaker.API/Models/PendingGenerationRequest.cs`
- Properties:
  - Id: int
  - UserId: string
  - TrainingRequestId: string
  - StylesJson: string
  - NumOutputsPerStyle: int
  - Status: PendingGenerationStatus
  - ErrorMessage: string?
  - LastPredictionId: string?
  - CreatedAt: DateTime
  - StartedAt: DateTime?
  - CompletedAt: DateTime?

### Prediction
- File: `AI.ProfilePhotoMaker.API/Models/Prediction.cs`
- Properties:
  - Id: string
  - UserId: string
  - Style: string?
  - CreatedAt: DateTime

### ProcessedImage
- File: `AI.ProfilePhotoMaker.API/Models/ProcessedImage.cs`
- Properties:
  - Id: int
  - OriginalImageUrl: string
  - ProcessedImageUrl: string
  - Style: string
  - UserProfileId: int
  - UserProfile: UserProfile
  - CreatedAt: DateTime
  - IsGenerated: bool
  - IsOriginalUpload: bool
  - ScheduledDeletionDate: DateTime

### ReplicateModelInfo
- File: `AI.ProfilePhotoMaker.API/Models/Replicate/ReplicateModelInfo.cs`
- Properties:
  - Name: string
  - Owner: string
  - Description: string?
  - CreatedAt: DateTime
  - UpdatedAt: DateTime
  - LatestVersion: string?
  - Visibility: string
  - CoverImageUrl: string?
  - RunCount: int

### ReplicateModelsResponse
- File: `AI.ProfilePhotoMaker.API/Models/Replicate/ReplicateModelsResponse.cs`
- Properties:
  - Next: string?
  - Previous: string?
  - Results: List<ReplicateModelApiResult>
  - Name: string
  - Owner: string
  - Description: string?
  - CreatedAt: DateTime
  - UpdatedAt: DateTime
  - LatestVersion: ReplicateModelVersion?
  - Visibility: string
  - CoverImageUrl: string?
  - RunCount: int
  - Id: string
  - CreatedAt: string?
  - Status: string?

### ReplicatePredictionResult
- File: `AI.ProfilePhotoMaker.API/Models/Replicate/ReplicatePredictionResult.cs`
- Properties:
  - Id: string?
  - Version: string?
  - Status: string?
  - Output: JsonElement?
  - Error: string?
  - Webhook: string?
  - Urls: ReplicateUrls?
  - CreatedAt: DateTime
  - StartedAt: DateTime?
  - CompletedAt: DateTime?

### ReplicateTrainingResult
- File: `AI.ProfilePhotoMaker.API/Models/Replicate/ReplicateTrainingResult.cs`
- Properties:
  - Id: string?
  - Status: string?
  - Version: string?
  - Error: string?
  - Logs: string?
  - Webhook: string?
  - Urls: ReplicateUrls?
  - CreatedAt: DateTime
  - StartedAt: DateTime?
  - CompletedAt: DateTime?
  - Get: string?
  - Cancel: string?

### Style
- File: `AI.ProfilePhotoMaker.API/Models/Style.cs`
- Properties:
  - Id: int
  - Name: string
  - Description: string
  - PromptTemplate: string
  - NegativePromptTemplate: string
  - IsActive: bool
  - CreatedAt: DateTime
  - UpdatedAt: DateTime

### Subscription
- File: `AI.ProfilePhotoMaker.API/Models/Subscription.cs`
- Properties:
  - Id: int
  - UserId: string
  - User: ApplicationUser
  - PlanId: string
  - Plan: SubscriptionPlan
  - StartDate: DateTime
  - EndDate: DateTime?
  - IsActive: bool
  - PaymentProvider: string
  - ExternalSubscriptionId: string
  - ExternalCustomerId: string?
  - Status: SubscriptionStatus
  - LastPaymentDate: DateTime?
  - NextBillingDate: DateTime?
  - CancelledAt: DateTime?
  - CancelAtPeriodEnd: DateTime?
  - CancellationReason: string?
  - CreatedAt: DateTime
  - UpdatedAt: DateTime

### SubscriptionPlan
- File: `AI.ProfilePhotoMaker.API/Models/SubscriptionPlan.cs`
- Properties:
  - Id: string
  - Name: string
  - Description: string
  - Price: decimal
  - BillingPeriod: string
  - ImagesPerMonth: int
  - CanTrainCustomModels: bool
  - CanBatchGenerate: bool
  - HighResolutionOutput: bool
  - MaxTrainingImages: int
  - MaxStylesAccess: int
  - StripeProductId: string?
  - StripePriceId: string?
  - IsActive: bool
  - CreatedAt: DateTime
  - UpdatedAt: DateTime
  - Subscriptions: List<Subscription>

### UsageLog
- File: `AI.ProfilePhotoMaker.API/Models/UsageLog.cs`
- Properties:
  - Id: int
  - UserId: string
  - User: ApplicationUser
  - Action: string
  - Details: string?
  - CreditsCost: int?
  - CreditsRemaining: int?
  - CreatedAt: DateTime

### UserInfo
- File: `AI.ProfilePhotoMaker.API/Models/UserInfo.cs`
- Properties:
  - Gender: string?
  - Ethnicity: string?

### UserProfile
- File: `AI.ProfilePhotoMaker.API/Models/UserProfile.cs`
- Properties:
  - Id: int
  - UserId: string
  - User: ApplicationUser
  - FirstName: string?
  - LastName: string?
  - Gender: string?
  - Ethnicity: string?
  - LastModelSyncCheck: DateTime?
  - SubscriptionTier: SubscriptionTier
  - Credits: int
  - PurchasedCredits: int
  - LastCreditReset: DateTime
  - ProcessedImages: List<ProcessedImage>
  - UsageLogs: List<UsageLog>
  - CreatedAt: DateTime
  - UpdatedAt: DateTime

### UserStyleSelection
- File: `AI.ProfilePhotoMaker.API/Models/UserStyleSelection.cs`
- Properties:
  - Id: int
  - UserProfileId: int
  - StyleId: int
  - SelectedAt: DateTime

