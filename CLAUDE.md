# AI Profile Photo Maker - Claude Code Instructions

## Important Rules
- Never create new deployment scripts unless explicitly asked
- When Debugging, never make assumption and always look for evidence to backup a decision
- When refactoring, don't preserve legacy code - always remove it
- Apply YAGNI principle - keep implementation simple
- Use Playwright tests instead of curl for web applications UI verificaiton and tests
- Verify work before marking complete
- Never directly commit to main branch

## Tech Stack

### Backend (.NET 8)
- **Framework**: ASP.NET Core 8.0 Web API
- **ORM**: Entity Framework Core 8 with SQL Server
- **Auth**: JWT + Google OAuth (Microsoft.AspNetCore.Authentication)
- **Storage**: Azure Blob Storage (Azure.Storage.Blobs)
- **Payments**: Stripe.net
- **AI APIs**: Replicate (tryAGI.Replicate), OpenAI
- **Logging**: Serilog + Application Insights
- **Security**: AspNetCoreRateLimit, TurnstileVerification
- **Health**: Microsoft.Extensions.Diagnostics.HealthChecks

### Frontend (Angular 19)
- **Framework**: Angular 19 with standalone components
- **Styling**: Tailwind CSS 3.4
- **Auth**: @abacritt/angularx-social-login
- **Payments**: @stripe/stripe-js
- **Face Detection**: face-api.js
- **Linting**: ESLint 9 + angular-eslint + Prettier
- **Git Hooks**: Husky + lint-staged

### Infrastructure
- **Hosting**: Azure Container Apps
- **Database**: Azure SQL Database
- **Storage**: Azure Blob Storage (profile-images container)
- **Registry**: Azure Container Registry
- **IaC**: Bicep (infrastructure/simple-deploy.bicep)
- **CI/CD**: GitHub Actions

### Testing
| Type | Backend | Frontend |
|------|---------|----------|
| Unit | xUnit, Moq, FluentAssertions, AutoFixture | Karma, Jasmine |
| E2E | - | Playwright |
| Performance | NBomber, BenchmarkDotNet | - |

## Project Structure
```
AI.ProfilePhotoMaker.API/           # .NET 8 Web API
├── Controllers/                    # API endpoints
├── Services/                       # Business logic
│   ├── Authentication/
│   ├── ImageProcessing/            # Replicate/OpenAI integration
│   ├── Payments/                   # Stripe integration
│   ├── Storage/                    # Azure Blob/Local storage
│   └── Notifications/              # Email services
├── Data/                           # EF Core DbContext
├── Migrations/                     # Database migrations
└── Configuration/                  # Environment config

AI.ProfilePhotoMaker.UI/            # Angular 19 SPA
├── src/app/
│   ├── auth/                       # Login, register, OAuth
│   ├── dashboard/                  # Main dashboard
│   ├── components/                 # Shared components
│   ├── services/                   # Angular services
│   ├── guards/                     # Route guards
│   └── pages/                      # Page components
└── tests/                          # Playwright E2E tests
```

## Development Commands

### Local Development
```bash
# Full rebuild (API + UI)
./dev-rebuild.sh

# API only
./dev-rebuild.sh --api-only

# Docker containers
./dev-rebuild.sh --docker
```

### Backend (.NET)
```bash
# Run API
cd AI.ProfilePhotoMaker.API && dotnet run

# Run tests (exclude Integration/Performance)
dotnet test AI.ProfilePhotoMaker.API.Tests --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Performance"

# Add migration
dotnet ef migrations add MigrationName --project AI.ProfilePhotoMaker.API

# Apply migrations
dotnet ef database update --project AI.ProfilePhotoMaker.API
```

### Frontend (Angular)
```bash
cd AI.ProfilePhotoMaker.UI

# Development server
npm run dev:local

# Build production
npm run build:mvp-v1

# Lint
npm run lint:fix

# Unit tests
npm test -- --watch=false --browsers=ChromeHeadless

# E2E tests
npx playwright test
```

### Ngrok (Webhooks)
```bash
# ALWAYS use reserved domain for webhooks
ngrok http 5032 --domain=clear-anteater-usually.ngrok-free.app
```

## Required Secrets

All validated by `./scripts/validate-secrets.sh`:

| Secret | Format | Purpose |
|--------|--------|---------|
| JWT_SECRET | 32+ chars | Auth tokens |
| REPLICATE_API_TOKEN | `r8_*` | AI model API |
| REPLICATE_WEBHOOK_SECRET | `whsec_*` | Webhook validation |
| GOOGLE_CLIENT_ID | `*.apps.googleusercontent.com` | OAuth |
| GOOGLE_CLIENT_SECRET | `GOCSPX-*` | OAuth |
| STRIPE_SECRET_KEY | `sk_*` | Payments |
| STRIPE_PUBLISHABLE_KEY | `pk_*` | Frontend payments |
| STRIPE_WEBHOOK_SECRET | `whsec_*` | Payment webhooks |
| MSSQL_SA_PASSWORD | 8+ chars, complex | Database |
| AZURE_STORAGE_CONNECTION_STRING | Connection string | Blob storage |
| AZURE_STORAGE_CONTAINER_NAME | `profile-images` | Storage container |
| OPENAI_API_KEY | `sk-*` | DALL-E 3 enhancement |
| TURNSTILE_SECRET_KEY | - | Bot protection |

## Key Services

| Service | Purpose |
|---------|---------|
| `RetentionPolicyBackgroundService` | Image cleanup every 6 hours (30-day retention) |
| `TrainingPollingBackgroundService` | Model training status polling |
| `BasicTierBackgroundService` | Basic tier credit management |
| `TurnstileVerificationService` | Bot protection |
| `ReplicateSignatureValidationAttribute` | Webhook signature validation |
| `AzureBlobStorageService` | Image storage (production) |
| `LocalStorageService` | Image storage (development fallback) |

## Code Patterns

### Backend Patterns
```csharp
// Service registration pattern
services.AddScoped<IStorageService, AzureBlobStorageService>();

// Controller pattern with authorization
[Authorize]
[Route("api/[controller]")]
public class PhotoController : ControllerBase

// Background service pattern
public class RetentionPolicyBackgroundService : BackgroundService
```

### Frontend Patterns
```typescript
// Standalone component pattern (Angular 19)
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html'
})

// Service with HttpClient
@Injectable({ providedIn: 'root' })
export class AuthService extends BaseHttpService
```

## Testing Guidelines

### Backend Tests
- Unit tests in `AI.ProfilePhotoMaker.API.Tests/Unit/`
- Integration tests in `AI.ProfilePhotoMaker.API.Tests/Integration/`
- Use `FluentAssertions` for assertions
- Use `Moq` for mocking
- Use `AutoFixture` for test data generation

### Frontend Tests
- Unit tests: `*.spec.ts` alongside components
- E2E tests: `AI.ProfilePhotoMaker.UI/tests/*.spec.ts`
- Run E2E with: `npx playwright test`

## Recommended Skills

### Development & Quality
- `/sc:build` - Build and compile with framework detection
- `/sc:test` - Run tests with coverage analysis
- `/sc:analyze` - Comprehensive code analysis (quality, security, performance)
- `/sc:improve` - Apply systematic code improvements
- `/sc:cleanup` - Remove dead code, optimize structure

### Implementation
- `/sc:implement` - Feature implementation with intelligent persona activation
- `/sc:troubleshoot` - Diagnose and resolve issues
- `/sc:git` - Git operations with intelligent commit messages

### Documentation & Planning
- `/sc:document` - Generate focused documentation
- `/sc:estimate` - Provide development estimates
- `/sc:workflow` - Generate implementation workflows from requirements

### Session Management
- `/sc:load` - Load project context for session
- `/sc:save` - Save session context and progress

### BMAD Workflows (Advanced)
- `/bmad:bmm:workflows:code-review` - Adversarial code review
- `/bmad:bmm:workflows:quick-dev` - Flexible development workflow
- `/bmad:bmm:workflows:quick-spec` - Generate tech specifications
- `/bmad:bmm:agents:tea` - Test automation engineer

## Security Considerations

- All webhooks require HTTPS and signature validation
- JWT tokens expire and require refresh
- Rate limiting enabled on sensitive endpoints
- Turnstile verification on signup/photo transform
- Azure Storage uses private access when proxy enabled
- No PII in logs (sanitized by Serilog)

## Data Retention

**30-day retention** for all images (aligned with Replicate model persistence).

`RetentionPolicyBackgroundService` runs every 6 hours:
- Deletes expired images
- Sends deletion warnings (14 and 7 days before)
- Cleans up orphaned enhanced images
- Removes Replicate models when users have no remaining headshots

## Deployment

```bash
# Validate secrets before deployment
./scripts/validate-secrets.sh Production

# Deployment is automated via GitHub Actions on push to main
# Manual workflow dispatch available at .github/workflows/simple-deploy.yml
```

Infrastructure outputs after deployment:
- Frontend: `https://aipm-web-v1.*.azurecontainerapps.io`
- Backend: `https://aipm-api-v1.*.azurecontainerapps.io`
- Production: `https://aiprofilephotomaker.com`
