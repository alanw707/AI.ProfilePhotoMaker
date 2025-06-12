# AI.ProfilePhotoMaker Architecture

This document outlines the architecture and design decisions for the AI.ProfilePhotoMaker application.

---

## System Architecture

The AI.ProfilePhotoMaker is built using a modern, layered architecture with the following components:

### Backend (.NET 8 Web API)
- **Presentation Layer**: Controllers, DTOs, Response Models
- **Service Layer**: Business Logic, External API Integration
- **Data Access Layer**: Entity Framework Core, Database Models
- **Infrastructure**: Configuration, Authentication, Middleware

### Frontend (Angular 19)
- **Presentation**: Components, Templates, Styles
- **State Management**: Services, Reactive Approach
- **API Integration**: HTTP Services, Interceptors
- **Routing**: Feature-based Routing Module

### External Services
- **Replicate.com**: AI Model Training & Generation
- **Azure Blob Storage** (planned): File Storage
- **Stripe** (planned): Payment Processing

---

## Data Flow

### User Registration & Authentication
1. User submits registration form
2. Backend validates input and creates account
3. JWT token issued for authentication
4. Subsequent requests include JWT in Authorization header

### Image Processing Workflow
1. User uploads selfie images (up to 10)
2. Images are validated, processed, and stored
3. Images are compressed and sent to Replicate.com
4. Custom AI model is trained based on user images
5. Webhook notification received when training completes
6. User selects desired styles for generation
7. Styles and trained model used to generate images
8. Generated images stored and presented to user

### Payment Processing (Planned)
1. User selects subscription plan
2. Payment form presented via Stripe integration
3. Payment processed and subscription created
4. User granted access based on subscription level

---

## Database Schema

### Core Entities

#### ApplicationUser
- Extended from ASP.NET Identity
- Contains authentication data

#### UserProfile
```
UserProfile
├── Id: int (PK)
├── UserId: string (FK to ApplicationUser)
├── FirstName: string
├── LastName: string
├── Gender: string (nullable)
├── Ethnicity: string (nullable)
├── CreatedAt: DateTime
└── ProcessedImages: List<ProcessedImage>
```

#### ProcessedImage
```
ProcessedImage
├── Id: int (PK)
├── OriginalImageUrl: string
├── ProcessedImageUrl: string
├── Style: string
├── UserProfileId: int (FK to UserProfile)
├── CreatedAt: DateTime
└── UserProfile: UserProfile
```

#### Subscription
```
Subscription
├── Id: int (PK)
├── UserId: string (FK to ApplicationUser)
├── PlanId: string (FK to SubscriptionPlan)
├── StartDate: DateTime
├── EndDate: DateTime
├── IsActive: bool
├── PaymentProvider: string
├── ExternalSubscriptionId: string
└── User: ApplicationUser
```

#### SubscriptionPlan
```
SubscriptionPlan
├── Id: string (PK)
├── Name: string
├── Description: string
├── Price: decimal
├── Currency: string
├── BillingPeriod: string
└── Features: List<string>
```

---

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Authenticate user

### User Profile
- `GET /api/profile` - Get current user profile
- `PUT /api/profile` - Update profile information
- `DELETE /api/profile` - Delete user account

### Image Processing
- `POST /api/profile/upload` - Upload selfie images
- `GET /api/profile/styles` - Get available styles
- `POST /api/profile/generate` - Generate images with styles
- `GET /api/profile/images` - Get user's processed images
- `GET /api/profile/images/{id}` - Download specific image

### Webhooks
- `POST /api/webhooks/replicate/training-complete` - Training completion webhook
- `POST /api/webhooks/replicate/prediction-complete` - Generation completion webhook

### Subscriptions (Planned)
- `GET /api/subscriptions/plans` - Get available plans
- `POST /api/subscriptions` - Create subscription
- `GET /api/subscriptions` - Get current subscription
- `PUT /api/subscriptions/{id}` - Update subscription
- `DELETE /api/subscriptions/{id}` - Cancel subscription

---

## Technical Implementations

### AI Integration (Replicate.com)

The application uses Replicate.com's API to:
1. Train custom FLUX.1 models using user images
2. Generate styled profile photos based on trained models

**Key Features:**
- **Advanced FLUX.1 Prompts**: Structured with subject, style, composition, lighting, color palette, mood, and technical elements
- **Negative Prompts**: Style-specific negative prompts to avoid common AI image issues
- **Webhook Handling**: Secure handling of asynchronous notifications
- **Custom Model Training**: Personalized model for each user's face

### Authentication & Security

- **JWT Authentication**: Token-based authentication with expiration
- **Password Hashing**: Secure password storage with ASP.NET Identity
- **Input Validation**: All user inputs validated and sanitized
- **CORS Policy**: Restricted cross-origin requests
- **Webhook Signature Verification**: Ensures webhook authenticity
- **Rate Limiting** (planned): Prevents abuse of authentication endpoints

### File Storage

The application handles various types of files:
- **Original Images**: User-uploaded selfies
- **Compressed Archives**: ZIP files for training
- **Generated Images**: AI-created profile photos

**Storage Strategy:**
- Development: Local file system
- Production (planned): Azure Blob Storage with SAS tokens

### Background Processing

Planned implementation of background jobs for:
- **Image Cleanup**: Delete generated images after 7 days
- **Model Cleanup**: Delete custom models after 24 hours
- **Scheduled Notifications**: Email users about pending deletions

---

## Scalability Considerations

### Horizontal Scaling
- API designed to be stateless for load balancing
- Separate services for CPU-intensive operations

### Performance Optimization
- Cached style options and common queries
- Efficient image handling and compression
- Pagination for image listings

### Resource Management
- Automatic cleanup of unused resources
- Optimized storage utilization

---

*Last updated: June 2023*
---

## Architectural Review & Recommendations (June 2025)

This section provides an analysis of the current backend architecture and offers recommendations for improvement. The review is based on an analysis of the controllers, services, and data context.

### 1. Controller Layer

**Observation:** The `ProfileController` has a very broad set of responsibilities, including profile management, image uploading, and AI model training. This violates the Single Responsibility Principle and makes the controller large and complex.

**Recommendation:** Refactor the `ProfileController` into smaller, more focused controllers:
-   A new `ImageController` for handling image uploads and management.
-   A new `TrainingController` for managing the AI model training process.
-   The existing `ProfileController` would then be responsible only for user profile CRUD operations.

### 2. Service Layer

**Observation:** A significant amount of business logic (e.g., file validation, ZIP creation) is implemented directly within the `ProfileController`. This makes the code harder to test, maintain, and reuse.

**Recommendation:** Extract business logic to dedicated service classes. This will make the controllers "thin" and focused on handling HTTP requests and responses, while the services encapsulate the core application logic.

### 3. AI Integration

**Observation:** The `ReplicateApiClient` is tightly coupled to the Replicate.com API and contains hardcoded values (e.g., model owner).

**Recommendations:**
-   **Externalize Configuration:** Move hardcoded values like the model `owner` to `appsettings.json`.
-   **Introduce an Abstraction Layer:** To improve flexibility, introduce a more generic `IAiModelService` interface. This would decouple the application from the specifics of the Replicate API and make it easier to integrate other AI services in the future.
-   **Separate Concerns:** The business logic for creating prompts should be extracted from the `ReplicateApiClient` and moved into a dedicated `IPromptGenerationService`.

### 4. Data Layer

**Observation:** The `ApplicationDbContext` is well-structured, but the data seeding for the `Style` entity contains very long, hardcoded strings for the prompt templates. Additionally, the `Subscription` and `SubscriptionPlan` entities from the initial design are not yet implemented.

**Recommendations:**
-   **Data Seeding Strategy:** For the `Style` data, move the long prompt templates out of the source code and into a separate seed data file (e.g., a JSON file) that can be read during the migration process.
-   **Explicit Cascade Deletes:** Configure cascading deletes directly in the database schema using `OnDelete(DeleteBehavior.Cascade)` to ensure that when a `UserProfile` is deleted, all of its associated `ProcessedImages` are automatically removed.

### 5. File Storage

**Observation:** The `ProfileController` directly uses `System.IO` and `IWebHostEnvironment` for file operations, creating a tight coupling to the local file system.

**Recommendation:** Introduce a file storage service (e.g., `IFileStorageService`) to abstract away the file system details. This will allow for easy switching between local storage and the planned Azure Blob Storage.