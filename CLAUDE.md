# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Standdar Workflow
1. First think through the problem, read the codebase for relevant files, and write a plan to tasks/todo.md.
2. The plan should have a list of todo items that you can check off as you complete them
3. Before you begin working, check in with me and I will verify the plan.
4. Then, begin working on the todo items, marking them as complete as you go.
5. Please every step of the way just give me a high level explanation of what changes you made
6. Make every task and code change you do as simple as possible. We want to avoid making any massive or complex changes. Every change should impact as little code as possible. Everything is about simplicity.
7. Finally, add a review section to the [todo.md](http://todo.md/) file with a summary of the changes you made and any other relevant information.

## Project Overview

AI.ProfilePhotoMaker is a full-stack application that generates professional profile photos using AI. Users upload selfies to train custom AI models through Replicate.com's FLUX API, then generate styled professional photos.

**Tech Stack:**
- Backend: .NET 8 Web API with Entity Framework Core, ASP.NET Identity, JWT auth
- Frontend: Angular 19 with TypeScript and SASS
- Database: SQL Server
- AI: Replicate.com FLUX.1 models
- Storage: Local filesystem (Azure Blob planned)

## Documentation Structure

Project documentation is organized as follows:
- `README.md` - Main project overview and getting started (root level for GitHub)
- `/docs/ARCHITECTURE.md` - System architecture and design patterns
- `/docs/PROJECT_PLAN.md` - Project milestones and timeline
- `/docs/TASKS.md` - Detailed task list and current status
- `/docs/SETUP.md` - Development environment setup instructions
- `/docs/OAUTH_TROUBLESHOOTING.md` - OAuth implementation guide
- `/docs/REFACTOR.md` - Comprehensive refactoring documentation

## Common Development Commands

### Backend (.NET API)
```bash
# Navigate to API project
cd AI.ProfilePhotoMaker.API

# Restore packages and build
dotnet restore
dotnet build

# Run the API (https://localhost:5001)
dotnet run

# Database migrations
dotnet ef migrations add MigrationName
dotnet ef database update

# Run tests (when available)
dotnet test
```

### Frontend (Angular)
```bash
# Navigate to UI project
cd AI.ProfilePhotoMaker.UI

# Install dependencies
npm install

# Start development server (http://localhost:4200)
ng serve

# Build for production
ng build

# Run tests
ng test

# Generate components/services
ng generate component component-name
ng generate service service-name
```

### Solution-level Commands
```bash
# Build entire solution
dotnet build AI.ProfilePhotoMaker.sln

# Run API and UI concurrently (if using concurrent tooling)
# API: dotnet run --project AI.ProfilePhotoMaker.API
# UI: cd AI.ProfilePhotoMaker.UI && ng serve
```

## Credit System Overview

AI.ProfilePhotoMaker uses a unified credit system that supports both basic tier (free weekly credits) and purchased credits for premium features.

### Credit Types

**Weekly Credits (Basic Tier):**
- Users receive 3 credits every 7 days (automatically reset)
- Can be used for Photo Enhancement operations only
- Managed by `BasicTierService` and `BasicTierBackgroundService`
- Stored in `UserProfile.Credits` field
- Reset logic tracks `LastCreditReset` timestamp

**Purchased Credits:**
- Users can purchase credit packages for premium features
- Required for Model Training and Styled Generation operations
- Managed by `CreditPackageService`
- Stored in separate credit purchase/transaction system
- No expiration or reset - permanent until consumed

### Credit Costs

| Operation | Cost | Credit Type | Notes |
|-----------|------|-------------|-------|
| Photo Enhancement | 1 credit | Weekly or Purchased | Uses Flux Kontext Pro model |
| Model Training | 15 credits | Purchased only | Custom model training via Replicate |
| Styled Generation | 5 credits | Purchased only | Generates images using trained model |

### Credit Architecture

**Database Models:**
- `UserProfile.Credits` - Current weekly credits balance
- `UserProfile.LastCreditReset` - Timestamp of last weekly reset
- `CreditPackage` - Available credit packages for purchase
- `CreditPurchase` - User credit purchase transactions
- `UsageLog` - Tracks all credit consumption with timestamps

**Key Services:**
- `BasicTierService` - Manages weekly credits and consumption
- `BasicTierBackgroundService` - Automated weekly credit resets
- `CreditPackageService` - Handles credit package purchases
- `ICreditPackageService` - Interface for credit package operations

**API Endpoints:**
- `/api/credit/packages` - Get available credit packages
- `/api/credit/purchase` - Purchase credit packages
- `/api/test/basic-tier-status` - Check user's credit status
- `/api/test/reset-credits` - Manually reset weekly credits (testing)

### Credit Validation Logic

Before any operation, the system:
1. Checks if operation requires weekly or purchased credits
2. Validates sufficient credits are available
3. Deducts credits and logs usage in `UsageLog`
4. Returns appropriate error if insufficient credits

**Weekly Credit Reset:**
- Runs as background service every hour
- Checks users where `LastCreditReset` is older than 7 days
- Resets `Credits` to 3 and updates `LastCreditReset`
- Only affects basic tier users

## Architecture Overview

### Project Structure
- `AI.ProfilePhotoMaker.API/` - .NET 8 Web API backend
  - `Controllers/` - API endpoints (Auth, Profile, Replicate, Test)
  - `Services/` - Business logic (BasicTierService, ReplicateApiClient, Auth services)
  - `Data/` - EF Core DbContext and repositories
  - `Models/` - Entity models and DTOs (GenerateBasicImageRequestDto, etc.)
  - `Migrations/` - EF Core database migrations

- `AI.ProfilePhotoMaker.UI/` - Angular 19 frontend
  - `src/app/components/` - Angular components (dashboard, photo-enhancement)
  - `src/app/services/` - Angular services (replicate, config, profile, etc.)
  - `src/app/` - Routing, authentication, and shared modules

### Key Integrations

**Replicate.com Workflow:**
1. User uploads selfies → API compresses into ZIP → Sends to Replicate for model training
2. Webhook receives training completion → Updates database with model ID
3. User selects styles → API generates images using trained model
4. Webhook receives generation completion → Stores image URLs

**Basic Tier Workflow:**
1. **Enhancement**: User uploads photo → API enhances using Flux Kontext Pro model with text-based prompts
2. Credit consumed (1 per enhancement) and tracked in UsageLog
3. Weekly background service resets credits every 7 days (3 credits per week)

**Authentication Flow:**
- ASP.NET Identity with JWT tokens
- Protected endpoints require `[Authorize]` attribute
- Frontend includes JWT in Authorization header

### Database Schema
- `ApplicationUser` (ASP.NET Identity extended)
- `UserProfile` (user demographics, Credits field for basic tier, subscription tier, last credit reset)
- `ProcessedImage` (original/processed URLs, style, timestamps)
- `UsageLog` (credit consumption tracking, actions, timestamps)
- `SubscriptionTier` (enum: Basic, Premium, Pro)
- `Subscription` & `SubscriptionPlan` (payment features, planned)

### Key API Endpoints
- **Authentication**: `/api/auth/login`, `/api/auth/google`, `/api/auth/apple`
- **Profile Management**: `/api/profile/*` (CRUD operations, file uploads)
- **Image Generation**: `/api/replicate/generate` (premium tier with trained models)
- **Photo Enhancement**: `/api/replicate/enhance` (uses Flux Kontext Pro, basic tier)
- **Credit Management**: `/api/credit/*` (packages, purchase, payment-config), `/api/test/basic-tier-status`, `/api/test/reset-credits`
- **Payment Simulation**: `/api/credit/create-payment-intent` (development mode placeholder)
- **Testing**: `/api/test/*` (cleaned up development/debugging endpoints)
  - `/api/test/fix-generated-images` (POST) - Repairs missing database records from filesystem
  - `/api/test/ping` (GET) - Health check endpoint
  - `/api/test/check-generated-images` (GET) - Debug endpoint for viewing user images
  - `/api/test/replicate-connection` (GET) - Tests Replicate API connectivity
  - `/api/test/basic-tier-status` (GET) - Debug user credit status
  - `/api/test/reset-credits` (POST) - Manually reset user credits for testing

### Key Services
- **BasicTierService**: Manages credit system, weekly resets, basic tier functionality
- **BasicTierBackgroundService**: Background service for automated credit resets
- **ReplicateApiClient**: Handles all Replicate.com API integration (training, generation, enhancement)
- **StripeService**: Payment processing with simulation mode for development
- **CreditPackageService**: Manages credit packages and purchase transactions
- **Auth Services**: JWT token management, OAuth integration

## Configuration Requirements

### API Configuration (appsettings.Development.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=aiprofilemaker.db"
  },
  "JWT": {
    "ValidAudience": "http://localhost:5035",
    "ValidIssuer": "http://localhost:5035", 
    "Secret": "STORED_IN_USER_SECRETS"    
  },
  "Replicate": {
    "ApiToken": "STORED_IN_USER_SECRETS",
    "FluxTrainingModelId": "replicate/fast-flux-trainer:e65b43286cf1fc648ebac89c32149769637c0410f5346b97c251cdbc3fc3da1a",
    "FluxGenerationModelId": "black-forest-labs/flux-dev",
    "FluxKontextProModelId": "black-forest-labs/flux-kontext-pro",
    "WebhookSecret": "STORED_IN_USER_SECRETS"
  },
  "Stripe": {
    "PublishableKey": "STORED_IN_USER_SECRETS",
    "SecretKey": "STORED_IN_USER_SECRETS",
    "WebhookSecret": "STORED_IN_USER_SECRETS"
  },
  "PaymentSimulation": {
    "Enabled": true,
    "SkipStripeIntegration": true
  },
  "AppBaseUrl": "https://16aa-71-38-148-86.ngrok-free.app",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Environment Setup
- .NET 8 SDK required
- Node.js 18+ and Angular CLI 19.x required
- SQL Server (Express acceptable)
- Replicate.com account with API credits

## Testing and Debugging

### Webhook Testing
Use ngrok for local webhook testing:
```bash
ngrok http https://localhost:5035
# Update Replicate webhook URLs to use ngrok tunnel
```

### API Testing
- Swagger UI available at `/swagger` when running in development
- Test authentication endpoints first, then use JWT tokens for protected endpoints

## Important Notes

### Development Guidelines
- The solution file only includes the API project; the UI is managed separately with Angular CLI
- Database migrations should be created when model changes are made
- Replicate API requires internet connectivity and valid credits
- JWT secret should be secure in production environments
- CORS is configured to allow all origins in development (`AllowAll` policy)

### Recent Major Changes

#### Critical Bug Fixes (2025-07-01)
- **Fixed Data Loss Bug in GetImages() Endpoint**: Resolved critical issue where generated photos were automatically deleted
  - **Problem**: ProfileController.GetImages() was checking external Replicate URLs and deleting database records when URLs expired
  - **Impact**: Users lost all previously generated photos (40+ images) when Replicate URLs became invalid
  - **Solution**: Removed external URL validation logic; now only checks local file existence
  - **Result**: Images remain in database regardless of external URL status; retention policy controls deletion properly
- **Cleaned Up TestController Endpoints**: Removed dangerous and redundant test endpoints
  - **Removed**: 6 endpoints including credit-consuming tests and database deletion endpoint
  - **Kept**: 6 useful endpoints for development/debugging (ping, check-generated-images, basic-tier-status, etc.)
  - **Safety**: Eliminated accidental credit consumption and data loss risks from test endpoints

#### Dashboard Performance and UI Improvements (2025-07-01)
- **Enhanced Photo Generation Success Messaging**: Added celebration-style success notifications
  - **Feature**: Shows count of newly generated photos with animated success card
  - **UI**: Modern gradient design with bounce animation and direct gallery navigation
  - **UX**: Clear feedback when generation completes with easy next-step guidance
- **Fixed Photo Generation Progress UI**: Replaced stuck 90% progress with realistic time-based estimation
  - **Problem**: Progress bar would get stuck at 90% during photo generation
  - **Solution**: Implemented time-based progress estimation (15% to 85% over expected duration)
  - **Improvement**: Users see realistic progress indication instead of stuck progress bar
- **Improved Continue in Background Button**: Fixed styling and ensured proper functionality
  - **Styling**: Updated CSS specificity to override Bootstrap classes properly
  - **Functionality**: Confirmed navigation to gallery works correctly
  - **Theme**: Matches modern subtle design system consistently

#### Payment Simulation System (2025-06-27)
- **Payment Integration Stabilization**: Complete payment simulation system for development
  - Added `/api/credit/create-payment-intent` placeholder endpoint with mock responses
  - Added `/api/credit/payment-config` endpoint for frontend configuration checking
  - Updated `StripeService` to conditionally load Stripe.js based on simulation settings
  - Implemented 2-second payment simulation workflow in credit-packages component
  - Added development mode UI notices and simulation status indicators
  - Eliminated all console errors from Stripe.js loading in development environment
  - Credits properly added to user accounts during payment simulation
  - Easy toggle between simulation and real Stripe integration via configuration

#### UI Component Unification (2025-06-27)
- **Header Navigation Consolidation**: Eliminated duplicate code across components
  - Created shared `HeaderNavigationComponent` with unified HTML, TypeScript, and styling
  - Consolidated header code from dashboard, gallery, settings, premium, and photo-enhancement components
  - Reduced header-related code duplication by ~90% (100+ lines of duplicate code removed)
  - Unified theme toggle, logout, and user info display functionality
  - Consistent navigation experience and styling across all pages

#### Previous Infrastructure Improvements
- **Photo Enhancement Integration**: Complete end-to-end photo enhancement workflow now functional
  - Fixed UI integration from demo mode to real Replicate API calls
  - Updated enhancement options from Professional/Portrait/LinkedIn to Background Remover/Social Media/Cartoon
  - Enhanced file upload service with proper response parsing for single image uploads
  - Fixed JPEG file validation to support multiple variants (JFIF, EXIF, SPIFF, raw)
  - Improved prediction status endpoint to handle both array and string outputs from Flux Kontext Pro
  - Added absolute URL conversion for Replicate API compatibility
- **Terminology Update**: All "Free tier" references updated to "Basic tier" throughout codebase
- **Database Schema**: `FreeCredits` column renamed to `Credits` in UserProfile table
- **Service Refactoring**: FreeTierService → BasicTierService, all related interfaces updated
- **API Endpoints**: `/generate/free` → `/generate/basic`, `/free-tier-status` → `/basic-tier-status`
- **Flux Integration**: Added Flux Kontext Pro model for photo enhancement (text-based prompts)
- **Credit System**: Weekly reset system with 3 credits per user per week
- **UI Components**: Complete terminology update across Angular components and services

### AI Model Configuration
- **Training**: Uses `replicate/fast-flux-trainer` for custom model training (premium tier)
- **Styled Generation**: Uses `black-forest-labs/flux-dev` for image generation with trained models (premium tier)
- **Enhancement**: Uses `black-forest-labs/flux-kontext-pro` for photo enhancement (basic tier)

## Development Best Practices

### Code Organization and Structure
- **Component Size Limits**: Keep Angular components under 400 lines. If larger, break into smaller components or extract logic into services
- **Service Separation**: Business logic should be in services, not components. Components should only handle UI logic and user interaction
- **File Organization**: Follow the principle of single responsibility - one class/interface per file
- **Avoid Code Duplication**: Extract common functionality into shared services, utilities, or components

### Backend (.NET) Best Practices
- **Controller Responsibility**: Controllers should be thin and delegate business logic to services
- **Service Layer**: Use dedicated service classes for complex business operations (e.g., `ImageDownloadService`, `BasicTierService`)
- **Interface Segregation**: Create focused interfaces (e.g., `IImageDownloadService`) rather than large monolithic ones
- **Error Handling**: Use try-catch blocks with proper logging and return appropriate HTTP status codes
- **Dependency Injection**: Register all services in `Program.cs` and use constructor injection

### Frontend (Angular) Best Practices
- **Component Decomposition**: Break large components into smaller, focused components
- **State Management**: Use services for shared state, avoid complex state in components
- **Service Injection**: Inject services through constructor, not in methods
- **Type Safety**: Always use TypeScript interfaces for API responses and data models
- **Observable Patterns**: Use RxJS observables for asynchronous operations, avoid nested subscriptions

### API Design Principles
- **Batch Operations**: Use single API calls with parameters (e.g., `numOutputs`) instead of multiple individual calls
- **Consistent Response Format**: All APIs should return `{ success: boolean, data?: any, error?: any }`
- **Resource Organization**: Group related endpoints under logical controllers
- **Validation**: Use DTOs with proper validation attributes

### File and Resource Management
- **Local Storage**: Store generated images locally in `/generated/{userId}/` for better control and reduced external dependencies
- **Static File Serving**: Configure proper static file serving for all resource directories
- **Cleanup Policies**: Implement automated retention policies with background services
- **URL Management**: Use relative URLs for local resources, absolute URLs for external resources

### Performance Considerations
- **Image Processing**: Download and store images asynchronously to avoid blocking operations
- **Database Operations**: Use appropriate indexes and avoid N+1 queries
- **Background Services**: Use hosted services for long-running operations (e.g., credit resets, image cleanup)
- **HTTP Clients**: Use `HttpClientFactory` for efficient HTTP client management

### Security Best Practices
- **Authentication**: Use JWT tokens with proper validation
- **Authorization**: Apply `[Authorize]` attributes to protected endpoints
- **Input Validation**: Validate all user inputs and API parameters
- **Secret Management**: Use configuration and user secrets for sensitive data
- **File Access**: Validate file paths and prevent directory traversal attacks