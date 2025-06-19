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
1. **Generation**: User requests basic generation → API checks available credits → Generates casual headshot using base FLUX model (no training required)
2. **Enhancement**: User uploads photo → API enhances using Flux Kontext Pro model with text-based prompts
3. Credit consumed (1 per generation/enhancement) and tracked in UsageLog
4. Weekly background service resets credits every 7 days (3 credits per week)

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
- **Image Generation**: `/api/replicate/generate` (paid tier), `/api/replicate/generate/basic` (basic tier)
- **Photo Enhancement**: `/api/replicate/enhance` (uses Flux Kontext Pro)
- **Credit Management**: `/api/test/basic-tier-status`, `/api/test/reset-credits`
- **Testing**: `/api/test/*` (various development/testing endpoints)

### Key Services
- **BasicTierService**: Manages credit system, weekly resets, basic tier functionality
- **BasicTierBackgroundService**: Background service for automated credit resets
- **ReplicateApiClient**: Handles all Replicate.com API integration (training, generation, enhancement)
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
  "AppBaseUrl": "https://057a-71-38-148-86.ngrok-free.app",
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
- **Training**: Uses `replicate/fast-flux-trainer` for custom model training (paid tier)
- **Generation**: Uses `black-forest-labs/flux-dev` for image generation (paid tier)
- **Basic Generation**: Uses `black-forest-labs/flux-dev` for basic tier (no training)
- **Enhancement**: Uses `black-forest-labs/flux-kontext-pro` for photo enhancement (basic tier)