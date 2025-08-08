# AI Profile Photo Maker - Code Style & Conventions

## .NET Backend Conventions
- **Target Framework**: .NET 8.0
- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Naming**: PascalCase for classes, methods, properties; camelCase for local variables
- **Project Structure**:
  - `Controllers/` - API controllers
  - `Services/` - Business logic services
  - `Models/` - Data models and DTOs
  - `Data/` - Entity Framework contexts and configurations
  - `Migrations/` - EF migrations
  - `Extensions/` - Extension methods
  - `Filters/` - Action filters
  - `Constants/` - Application constants

## Angular Frontend Conventions
- **TypeScript**: Strict mode enabled
- **Styling**: Tailwind CSS with utility-first approach
- **Component Architecture**: Standalone components preferred
- **File Naming**: kebab-case for files, PascalCase for classes
- **Code Quality Tools**:
  - ESLint for linting with custom configuration
  - Prettier for code formatting
  - EditorConfig for consistent editor settings

## Configuration Management
- **Environment-specific configs**: Multiple proxy configurations and build configs
- **Development**: Local, ngrok, test, hybrid configurations
- **Production**: Staging and production build configurations
- **Secrets**: User secrets for development, environment variables for production

## Testing Conventions
- **Unit Tests**: Jasmine/Karma for Angular, likely xUnit for .NET
- **Integration Tests**: Separate karma configuration
- **E2E Tests**: Playwright with comprehensive test scenarios
- **Test Organization**: Separate test projects and configurations

## Documentation Standards
- **README files**: Multiple README files for different aspects
- **Inline Documentation**: Comprehensive commenting expected
- **API Documentation**: Swagger/OpenAPI integration
- **Deployment Guides**: Detailed deployment and setup documentation