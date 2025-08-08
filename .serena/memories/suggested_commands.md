# AI Profile Photo Maker - Suggested Development Commands

## Quick Start Development
```bash
# Start complete development environment
./start-dev.sh

# Access the application
# Frontend: https://awlocaldev.ngrok.app
# Backend: https://awlocaldev-api.ngrok.app
# Ngrok dashboard: http://localhost:4040
```

## Daily Development Commands

### Start Services
```bash
# Full stack development (recommended)
./start-dev.sh

# Frontend only
./start-dev.sh -f

# Backend only
./start-dev.sh -b

# Without ngrok (local only)
./start-dev.sh -n
```

### Code Quality
```bash
# Frontend linting (run before commits)
cd AI.ProfilePhotoMaker.UI && npm run lint

# Fix linting issues
cd AI.ProfilePhotoMaker.UI && npm run lint:fix

# Format code with Prettier
cd AI.ProfilePhotoMaker.UI && npm run format
```

### Testing
```bash
# Frontend unit tests
cd AI.ProfilePhotoMaker.UI && npm run test

# Integration tests
cd AI.ProfilePhotoMaker.UI && npm run test:integration:headless

# E2E tests
cd AI.ProfilePhotoMaker.UI && npm run test:e2e

# Backend tests
cd AI.ProfilePhotoMaker.API && dotnet test
```

### Build & Deploy
```bash
# Build for production
cd AI.ProfilePhotoMaker.UI && npm run build:prod

# Local Docker build
./scripts/build-local.sh

# Push to Azure Container Registry
./scripts/push-to-acr.sh

# Deploy (via git)
git push origin main
```

## Service Management

### Restart Services
```bash
# Restart backend only (keeps frontend running)
./start-dev.sh --restart-backend

# Restart frontend only
./start-dev.sh --restart-frontend

# Kill all development processes
pkill -f "ng serve|dotnet run|ngrok"
```

### Service Status & Debugging
```bash
# Check running processes
ps aux | grep -E "(ng serve|dotnet run|ngrok)"

# Test endpoints
curl https://awlocaldev.ngrok.app
curl https://awlocaldev-api.ngrok.app/api/credit/packages

# View ngrok status
curl http://127.0.0.1:4040/api/tunnels
```

## Database Management
```bash
# Create migration
cd AI.ProfilePhotoMaker.API && dotnet ef migrations add <MigrationName>

# Update database
cd AI.ProfilePhotoMaker.API && dotnet ef database update

# View database status
cd AI.ProfilePhotoMaker.API && dotnet ef migrations list
```

## Environment Switching
```bash
# Development with ngrok
cd AI.ProfilePhotoMaker.UI && npm run dev:ngrok

# Local development
cd AI.ProfilePhotoMaker.UI && npm run dev:local

# Test environment
cd AI.ProfilePhotoMaker.UI && npm run dev:test
```

## Troubleshooting Commands
```bash
# Install npm dependencies
cd AI.ProfilePhotoMaker.UI && npm install

# Clean Angular build
cd AI.ProfilePhotoMaker.UI && ng cache clean

# Restore .NET packages
cd AI.ProfilePhotoMaker.API && dotnet restore

# Install Playwright browsers
cd AI.ProfilePhotoMaker.UI && npm run playwright:install

# Check Docker status
docker ps

# View service logs (if containerized)
docker logs <container-name>
```

## Git Workflow
```bash
# Standard development flow
git add .
git commit -m "feature: description"
git push origin main

# Create feature branch
git checkout -b feature/feature-name
git push origin feature/feature-name
```

## Quick References
- **Development Guide**: DEV-ENVIRONMENT.md
- **Quick Start**: QUICK-START.md  
- **Deployment**: docs/LOCAL-BUILD-WORKFLOW.md
- **Project Status**: AI.ProfilePhotoMaker.UI/PROJECT_STATUS_SUMMARY.md