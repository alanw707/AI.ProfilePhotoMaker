# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AI.ProfilePhotoMaker is a full-stack application that generates professional profile photos using AI. Users upload selfies to train custom AI models through Replicate.com's FLUX API, then generate styled professional photos.

**Tech Stack:**
- Backend: .NET 8 Web API with Entity Framework Core, ASP.NET Identity, JWT auth
- Frontend: Angular 19 with TypeScript and SASS
- Database: SQL Server
- AI: Replicate.com FLUX.1 models
- Storage: Local filesystem (Azure Blob planned)

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
- **Testing**: `/api/test/*` (various development/testing endpoints)

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