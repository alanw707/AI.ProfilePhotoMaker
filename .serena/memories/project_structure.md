# AI Profile Photo Maker - Project Structure

## Root Directory Structure
```
AI.ProfilePhotoMaker/
├── AI.ProfilePhotoMaker.API/          # .NET 8 Web API backend
├── AI.ProfilePhotoMaker.UI/           # Angular 18 frontend  
├── AI.ProfilePhotoMaker.API.Tests/    # Backend test project
├── infrastructure/                    # Infrastructure as code
├── scripts/                          # Build and deployment scripts
├── docs/                             # Project documentation
├── .github/                          # GitHub Actions CI/CD
├── .vscode/                          # VSCode configuration
├── .husky/                           # Git hooks
├── start-dev.sh                      # Development environment script
├── AI.ProfilePhotoMaker.sln          # Visual Studio solution
└── docker-compose files & configs
```

## Backend Structure (AI.ProfilePhotoMaker.API/)
```
Controllers/          # API controllers (REST endpoints)
Services/            # Business logic services  
Models/              # Data models and DTOs
Data/                # Entity Framework contexts
Migrations/          # EF database migrations
Extensions/          # Extension methods
Filters/             # Action filters
Constants/           # Application constants
Properties/          # Assembly properties
style-previews/      # Style preview assets
tests/               # Unit tests
```

## Frontend Structure (AI.ProfilePhotoMaker.UI/)
```
src/
├── app/             # Angular application code
├── assets/          # Static assets
├── environments/    # Environment configurations
└── styles/          # Global styles (Tailwind CSS)

public/              # Public static files
e2e/                 # Playwright E2E tests
cypress/             # Cypress tests (if used)
screenshots/         # Test screenshots
.well-known/         # Web standards files
```

## Key Configuration Files

### Backend Configuration
- `appsettings.json` - Main configuration
- `appsettings.Development.json` - Development settings
- `appsettings.Test.json` - Test environment settings
- `appsettings.Staging.json` - Staging configuration
- `AI.ProfilePhotoMaker.API.csproj` - Project file

### Frontend Configuration  
- `angular.json` - Angular workspace configuration
- `package.json` - npm dependencies and scripts
- `proxy.conf.*.json` - Proxy configurations for different environments
- `tailwind.config.js` - Tailwind CSS configuration
- `eslint.config.js` - ESLint configuration
- `playwright.config.ts` - Playwright E2E test configuration

### Development Environment
- `ngrok.yml` - ngrok tunnel configuration
- `start-dev.sh` - Complete development environment startup
- `Dockerfile.backend` & `Dockerfile.frontend` - Container configurations
- `.env.example` - Environment variables template

## Documentation Structure
```
docs/
├── LOCAL-BUILD-WORKFLOW.md    # Local build process guide
├── PROJECT_PLAN.md            # Overall project plan
├── TASKS.md                   # Task tracking
├── SETUP.md                   # Development setup
└── ARCHITECTURE.md            # System architecture
```

## Build & Deployment
```
scripts/
├── build-local.sh             # Local Docker image building
├── push-to-acr.sh            # Push to Azure Container Registry
└── other deployment scripts

.github/workflows/             # GitHub Actions CI/CD pipelines
infrastructure/                # Infrastructure as Code (likely Terraform/ARM)
```

## Environment Configurations
The project supports multiple environment configurations:
- **Development**: Local development with hot reload
- **Ngrok**: Development with public URLs via ngrok tunnels
- **Test**: Testing environment configuration
- **Staging**: Pre-production environment
- **Production**: Production deployment configuration
- **Hybrid**: Mixed local/remote configuration

## Key Features by Directory
- **Authentication**: Implemented in API Controllers + Identity
- **AI Integration**: Services layer with Replicate API integration  
- **File Storage**: Azure Blob Storage integration
- **Payment**: Stripe integration in Services
- **Database**: Entity Framework with SQL Server/SQLite
- **Frontend**: Angular with Tailwind CSS styling
- **Testing**: Comprehensive unit, integration, and E2E test coverage