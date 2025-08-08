# AI Profile Photo Maker - Technology Stack

## Backend (.NET 8 Web API)
- **Framework**: ASP.NET Core 8.0 Web API
- **Language**: C# with nullable reference types enabled
- **Database**: 
  - SQL Server (production)
  - SQLite (development/testing)
- **ORM**: Entity Framework Core 8.0
- **Authentication**: 
  - ASP.NET Core Identity
  - JWT Bearer tokens
  - Social auth (Google, Facebook)
- **External Services**:
  - Replicate AI API for image generation
  - Azure Blob Storage for image storage
  - Stripe for payment processing
- **API Documentation**: Swagger/OpenAPI

## Frontend (Angular 18)
- **Framework**: Angular 18 with TypeScript
- **Styling**: Tailwind CSS + PostCSS
- **Testing**: 
  - Jasmine/Karma for unit tests
  - Playwright for E2E testing
- **Build Tools**: Angular CLI
- **Code Quality**: ESLint + Prettier

## Development Environment
- **Tunneling**: ngrok for local development with public URLs
- **Containerization**: Docker (Dockerfile for both frontend/backend)
- **Environment Management**: Multiple proxy configurations
- **Development Scripts**: Comprehensive npm scripts for different environments

## Infrastructure
- **Cloud**: Azure (Container Registry, App Services)
- **CI/CD**: GitHub Actions
- **Local Development**: Docker + ngrok tunneling
- **Build Strategy**: Local build workflow with Azure Container Registry

## Key Dependencies
- **Backend**: Entity Framework, Stripe.NET, Azure.Storage.Blobs, tryAGI.Replicate
- **Frontend**: Angular Material, RxJS, Angular Router
- **Development**: Concurrently for running multiple services, Husky for git hooks