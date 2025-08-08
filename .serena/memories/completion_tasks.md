# AI Profile Photo Maker - Task Completion Commands

## Frontend Development Workflow

### Code Quality & Linting
```bash
# Run linting (required before builds)
npm run lint

# Fix linting issues automatically
npm run lint:fix

# Run linting with cache for faster execution
npm run lint:cache
```

### Testing Commands
```bash
# Run unit tests
npm run test

# Run integration tests
npm run test:integration

# Run integration tests headless
npm run test:integration:headless

# Run E2E tests with Playwright
npm run test:e2e

# Run E2E tests in headed mode
npm run test:e2e:staging:headed

# Install Playwright browsers
npm run playwright:install
```

### Build Commands
```bash
# Development build (includes linting)
npm run build:dev

# Production build (includes linting)
npm run build:prod

# Test environment build
npm run build:test

# Staging build
npm run build:staging
```

## Backend Development Workflow

### .NET Commands
```bash
# Run the API server
cd AI.ProfilePhotoMaker.API && dotnet run

# Build the project
dotnet build

# Run tests (if available)
dotnet test

# Create EF migration
dotnet ef migrations add <MigrationName>

# Update database
dotnet ef database update
```

## Full Development Environment

### Start Complete Development Stack
```bash
# Start all services (ngrok + frontend + backend)
./start-dev.sh

# Start only frontend
./start-dev.sh -f

# Start only backend  
./start-dev.sh -b

# Start without ngrok
./start-dev.sh -n

# Restart backend only
./start-dev.sh --restart-backend
```

### Individual Services
```bash
# Start ngrok tunnels
npm run tunnel:start

# Start frontend with ngrok
npm run dev:ngrok

# Start frontend locally
npm run dev:local

# Start backend
cd AI.ProfilePhotoMaker.API && dotnet run
```

## Deployment Workflow

### Local Build Process
```bash
# Build Docker images locally
./scripts/build-local.sh

# Push to Azure Container Registry
./scripts/push-to-acr.sh

# Deploy via git push (triggers CI/CD)
git push origin main
```

## Quality Gates
1. **Linting**: Must pass `npm run lint` before builds
2. **Testing**: Run unit and integration tests before deployment
3. **Build**: Successful build required for deployment
4. **E2E Testing**: Playwright tests for critical user flows

## Required Tools Validation
- Node.js 18+ and npm
- .NET 8 SDK
- Docker Desktop
- Azure CLI (logged in)
- ngrok (configured with authtoken)