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