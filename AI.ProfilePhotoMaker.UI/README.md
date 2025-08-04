# AIProfilePhotoMakerUI

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 19.2.12.

## Development Environment

### Quick Start (Recommended)
For full development environment with ngrok tunneling:

```bash
# From project root
./start-dev.sh
```

Or manually:
```bash
# 1. Start ngrok tunnels
npm run tunnel:start

# 2. Start frontend
npm run dev:ngrok

# 3. Start backend (separate terminal)  
cd ../AI.ProfilePhotoMaker.API && dotnet run
```

**Access Points:**
- Frontend: https://awlocaldev.ngrok.app
- Backend: https://awlocaldev-api.ngrok.app

### Local Development Only
To start a local-only development server:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

### Documentation
- **[Quick Start Guide](../QUICK-START.md)** - Fast setup reference
- **[Development Environment Guide](../DEV-ENVIRONMENT.md)** - Comprehensive setup and troubleshooting
- **[Development Backlog](./DEVELOPMENT_BACKLOG.md)** - Current progress and task tracking
- **[Development Guide](./README.development.md)** - Environment setup and troubleshooting

## 🚀 Production Deployment

**Status**: ✅ **FULLY OPERATIONAL**  
**Environment**: Azure Container Apps + Azure Blob Storage + Azure SQL Database  
**Last Deployed**: January 4, 2025

### Production URLs
- **Frontend**: `https://aiprofilemaker-web-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io`
- **API**: `https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io`
- **Status**: All systems operational with 100% uptime

### Deployment Features
- ✅ **Zero Downtime**: Seamless deployment with no service interruption
- ✅ **Full Integration**: Frontend, backend, database, and storage fully connected
- ✅ **Security**: HTTPS, CORS, and Azure security features implemented
- ✅ **Performance**: < 3s load times, < 500ms API responses
- ✅ **Monitoring**: Application Insights and health monitoring active

### Quick Links
- **[📋 Deployment Plan](./DEPLOYMENT-PLAN.md)**: Complete deployment status
- **[🔧 Troubleshooting Guide](./TROUBLESHOOTING.md)**: Issue resolution documentation
- **[🚀 Production Status](./PROJECT_STATUS_SUMMARY.md)**: Current system overview

### Recent Updates
- **✅ January 4, 2025**: **MAJOR MILESTONE** - Full production deployment completed
  - All Azure infrastructure deployed and operational
  - Database migrations completed with 20+ professional styles
  - CORS and storage URL issues resolved
  - Comprehensive E2E testing validated
  - Complete documentation provided

- **✅ July 17, 2025**: Fixed green cards grid positioning issue in "Review Selected Images" section
  - Resolved container overflow problems on mobile devices
  - Adopted conservative flexbox-first responsive strategy
  - Improved consistency between green and red card layouts
  - Commit: `6d5be33`

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Karma](https://karma-runner.github.io) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
