# AI.ProfilePhotoMaker

An AI-powered profile photo generator with a .NET 8 Web API backend and Angular frontend.

## Project Documentation

- [Project Plan](docs/PROJECT_PLAN.md) - Overall plan and milestones
- [Tasks](docs/TASKS.md) - Detailed task list and status
- [Setup Guide](docs/SETUP.md) - Development environment setup
- [Architecture](docs/ARCHITECTURE.md) - System architecture and design

> **Note:** The project uses semantic search tools to assist with code navigation, onboarding, and automated documentation updates. This enables faster onboarding and more accurate code suggestions.

## Overview

AI.ProfilePhotoMaker is a web application that allows users to create professional profile photos using AI. Users can upload selfies, which are used to train a custom AI model through Replicate.com's FLUX API. The application then generates high-quality professional profile photos in various styles selected by the user.

## Key Features

- **User Authentication**: Secure registration and login
- **Image Upload**: Upload up to 10 selfies for training
- **Style Selection**: Choose from multiple professional photo styles
- **AI Processing**: Custom model training and image generation
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
- Angular 19
- TypeScript
- SASS
- Reactive Forms

### External Services
- Replicate.com FLUX AI
- Azure Blob Storage (planned)
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

Contributions are welcome! Please feel free to submit a Pull Request.

## Acknowledgments

- [Replicate.com](https://replicate.com) for their FLUX AI model
- [Angular](https://angular.io) and [.NET](https://dotnet.microsoft.com) communities

---

---

## Additional Documentation

- [Refactoring Documentation](docs/REFACTOR.md) - Comprehensive refactoring process and architecture improvements
- [OAuth Troubleshooting](docs/OAUTH_TROUBLESHOOTING.md) - OAuth implementation and troubleshooting guide

*Last updated: June 28, 2025*