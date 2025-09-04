# AI Profile Photo Maker

A modern web application for creating professional AI-generated profile photos using Replicate's AI models with a simplified local build workflow.

## 🚀 Quick Start (Local Build Workflow)

This project uses a simplified local build approach for faster development and more reliable deployments.

### Prerequisites
- Docker Desktop installed and running
- Azure CLI installed (`az --version`) 
- Logged in to Azure (`az login`)
- Node.js 20.x (LTS) and .NET 8

### Deploy in 3 Steps

```bash
# 1. Build images locally (30-60 seconds)
./scripts/build-local.sh

# 2. Push to Azure Container Registry
./scripts/push-to-acr.sh

# 3. Deploy infrastructure (triggers automatically)
git push origin main
```

## 📖 Documentation

- **[Local Build Workflow](docs/LOCAL-BUILD-WORKFLOW.md)** - Complete guide to the local build process
- [Project Plan](docs/development/PROJECT_PLAN.md) - Overall plan and milestones
- [Development Backlog](docs/development/DEVELOPMENT_BACKLOG.md) - Detailed task list and status
- [Setup Guide](docs/ENVIRONMENT_SETUP.md) - Development environment setup
- [Architecture Overview](docs/architecture/ARCHITECTURE_OVERVIEW.md) - System architecture and design
- [Private Blob Storage via API Proxy](docs/PRIVATE_BLOB_STORAGE.md) - Switch to private containers using the API proxy

## Overview

AI.ProfilePhotoMaker is a web application that allows users to create professional profile photos using AI. Users can upload selfies, which are used to train a custom AI model through Replicate.com's FLUX API. The application then generates high-quality professional profile photos in various styles selected by the user.

## Key Features

- **User Authentication**: Secure registration and login
- **Image Upload**: Upload up to 10 selfies for training
- **Style Selection**: Choose from multiple professional photo styles
- **AI Processing**: Custom model training and image generation with webhook-based processing
- **Photo Enhancement**: Real-time photo enhancement with 75-85% faster response times
- **Results Gallery**: View, download, and manage generated photos
- **Subscription Plans**: Access features based on subscription level

## Technology Stack

### Backend
- .NET 8 Web API
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- JWT Authentication

### Frontend
- Angular 19 + TypeScript
- Angular CLI build system
- Modern CSS with Angular styling
- Responsive design

### Cloud Infrastructure
- Azure Container Apps (hosting)
- Azure Container Registry (images)
- Azure SQL Database (data)
- Azure Storage Account (files)
- Azure Key Vault (secrets)
- Application Insights (monitoring)

### Blob Storage Access Modes
- Default (public blob): Images are served directly from Azure Blob URLs. No API proxy is used; CORS for blobs must allow the frontend domains. This is the current production setup.
- Private (API proxy): To serve images via the API (supporting private containers and unified caching), enable `Storage:ProxyBlobRequests`.
  - Set `Storage:ProxyBlobRequests=true` in configuration (env var `Storage__ProxyBlobRequests=true`).
  - The API will expose images at `/profile-images/{storagePath}` and return proxied URLs based on `AppBaseUrl`.
  - Consider turning off public access on the storage account/containers and using SAS only for backend-to-backend flows.
  - Enhanced proxy adds cache headers (`Cache-Control: immutable`) for better client performance.

### External Services
- Replicate.com FLUX AI (webhook-based integration)
- Stripe Payments (planned)

## Getting Started

For detailed setup instructions, see the [Setup Guide](docs/SETUP.md).

### Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/AI.ProfilePhotoMaker.git
   cd AI.ProfilePhotoMaker
   ```

2. **Set up the backend**
   ```bash
   cd AI.ProfilePhotoMaker.API
   dotnet restore
   dotnet run
   ```

3. **Set up the frontend**
   ```bash
   cd AI.ProfilePhotoMaker.UI
   npm install
   ng serve
   ```

4. Open your browser and navigate to `http://localhost:4200`

## Project Structure

```
AI.ProfilePhotoMaker/
├── .github/                    # GitHub workflows and templates
├── AI.ProfilePhotoMaker.API/   # .NET 8 Web API
│   ├── Controllers/            # API endpoints
│   ├── Data/                   # Database context and repositories
│   ├── Models/                 # Data models and DTOs
│   ├── Services/               # Business logic and integrations
│   └── Program.cs              # Application entry point
├── AI.ProfilePhotoMaker.UI/    # Angular frontend
│   ├── src/                    # Source code
│   │   ├── app/                # Angular components and services
│   │   ├── assets/             # Static assets
│   │   └── environments/       # Environment configurations
│   └── angular.json            # Angular CLI configuration
└── docs/                       # Project documentation
```

## API Endpoints

For a detailed API reference, run the application and visit `/swagger` endpoint.

### Key Endpoints

- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Authenticate user
- `POST /api/profile/upload` - Upload selfie images
- `GET /api/profile/styles` - Get available styles
- `POST /api/profile/generate` - Generate images with styles
- `GET /api/profile/images` - Get user's processed images

## Development Workflow

1. Choose a task from the [Tasks](docs/TASKS.md) document
2. Create a feature branch (`feature/your-feature-name`)
3. Implement the feature
4. Add tests
5. Create a pull request

## License

This project is licensed under the MIT License - see the LICENSE.txt file for details.

## Contributing

Contributions are welcome! Before opening a PR, please review the repository guidelines in [AGENTS.md](AGENTS.md) for project structure, local dev commands, coding style, testing, and PR requirements.

## Acknowledgments

- [Replicate.com](https://replicate.com) for their FLUX AI model
- [Angular](https://angular.io) and [.NET](https://dotnet.microsoft.com) communities

---

---

## Additional Documentation

- [Enhanced Photo Webhook Migration](docs/ENHANCE_PHOTO_WEBHOOK_MIGRATION.md) - Migration summary and architecture improvements (August 22, 2025)
- [API Webhook Integration Guide](docs/API_WEBHOOK_INTEGRATION.md) - Comprehensive webhook architecture and integration guide
- [Refactoring Documentation](docs/REFACTOR.md) - Comprehensive refactoring process and architecture improvements
- [OAuth Troubleshooting](docs/OAUTH_TROUBLESHOOTING.md) - OAuth implementation and troubleshooting guide
- [Product Requirements (PRD)](docs/product/PRD.md) - Product goals, scope, and milestones

*Last updated: August 22, 2025*
