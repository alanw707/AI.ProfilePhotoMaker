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

### Recent Updates
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
