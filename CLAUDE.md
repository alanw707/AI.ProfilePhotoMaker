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
  - `Controllers/` - API endpoints (Auth, Profile, Replicate, Webhooks)
  - `Services/` - Business logic (Auth, Image Processing, Replicate integration)
  - `Data/` - EF Core DbContext and repositories
  - `Models/` - Entity models and DTOs
  - `Migrations/` - EF Core database migrations

- `AI.ProfilePhotoMaker.UI/` - Angular 19 frontend
  - `src/app/` - Angular components, services, and routing

### Key Integrations

**Replicate.com Workflow:**
1. User uploads selfies → API compresses into ZIP → Sends to Replicate for model training
2. Webhook receives training completion → Updates database with model ID
3. User selects styles → API generates images using trained model
4. Webhook receives generation completion → Stores image URLs

**Authentication Flow:**
- ASP.NET Identity with JWT tokens
- Protected endpoints require `[Authorize]` attribute
- Frontend includes JWT in Authorization header

### Database Schema
- `ApplicationUser` (ASP.NET Identity extended)
- `UserProfile` (user demographics, has many ProcessedImages)
- `ProcessedImage` (original/processed URLs, style, timestamps)
- `Subscription` & `SubscriptionPlan` (payment features, planned)

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

- The solution file only includes the API project; the UI is managed separately with Angular CLI
- Database migrations should be created when model changes are made
- Replicate API requires internet connectivity and valid credits
- JWT secret should be secure in production environments
- CORS is configured to allow all origins in development (`AllowAll` policy)