# Security Implementation Notes

## DiagnosticController Security

### Environment-Based Exclusion Implementation

The `DiagnosticController` has been secured with **multiple layers of protection**:

#### 1. Conditional Compilation Protection
```csharp
#if DEBUG || DEVELOPMENT
public class DiagnosticController : ControllerBase
{
    // ... controller implementation
}
#endif
```

**Effect**: Controller is **completely excluded** from Production builds at compile time.

#### 2. Runtime Environment Validation
```csharp
public DiagnosticController(IWebHostEnvironment environment)
{
    if (_environment.IsProduction())
    {
        throw new InvalidOperationException("DiagnosticController is not available in Production environment");
    }
}
```

**Effect**: Even if somehow compiled into Production, constructor throws exception preventing instantiation.

#### 3. Per-Endpoint Security Gates
```csharp
private ActionResult ValidateEnvironment()
{
    if (_environment.IsProduction())
    {
        return NotFound("Diagnostic endpoints are not available in Production");
    }
    return null;
}
```

**Effect**: Every endpoint validates environment before execution, returning 404 for Production.

### Production Build Verification

To verify DiagnosticController exclusion in Production:

```bash
# Build for Production
dotnet build -c Release -p:DefineConstants="RELEASE"

# Check if controller exists in build output
# Controller should be completely missing from compiled assembly
```

### Development vs Production Behavior

| Environment | Controller Status | Endpoint Response |
|-------------|------------------|-------------------|
| Development | ✅ Available | Normal operation |
| Staging | ✅ Available (if DEVELOPMENT defined) | Normal operation |
| Production | ❌ Excluded | 404 Not Found |

### Security Benefits

1. **Attack Surface Reduction**: Controller doesn't exist in Production builds
2. **Defense in Depth**: Multiple validation layers prevent accidental exposure
3. **Clear Logging**: Security violations are logged with critical level
4. **Zero Configuration**: Automatic based on environment detection

### Deployment Guidelines

- **Development/Staging**: No special configuration needed
- **Production**: Ensure `ASPNETCORE_ENVIRONMENT=Production` is set
- **Container Builds**: Use Production configuration in Dockerfile
- **CI/CD**: Verify controller exclusion in production deployment tests

### Monitoring

Watch for these log entries indicating security issues:
- `🚨 SECURITY: DiagnosticController accessed in Production environment!`
- `DiagnosticController is not available in Production environment`

These should **never appear** in Production logs if properly configured.

## Logging Hygiene

- All controllers, middleware, hubs, and services route log arguments through `Infrastructure/Logging/LoggingSanitizer.cs`. Controllers inherit `S(...)` / `Sid(...)` helpers from `BaseController`; services add local helpers when logging values that may originate from users or external systems.
- Sanitization trims control characters, normalizes empty values to `[redacted]`, and caps length (256 by default, 128 for IDs). Use `S` for general strings and `Sid` for identifiers (user IDs, Stripe IDs, transaction IDs, SQL sources).
- Never interpolate raw exception messages, URLs, SQL fragments, request payloads, or IDs directly into log templates. Wrap them in `S`/`Sid` before logging—even for debug-level output or dev-only diagnostics.
- When adding new logging outside controllers:
  - `using AI.ProfilePhotoMaker.API.Infrastructure.Logging;`
  - define local helpers: `private static string S(string? value) => LoggingSanitizer.Sanitize(value);`
  - sanitize *all* dynamic arguments prior to logging.
- Existing coverage:
  - Stripe flows (`CreditController`, `StripeWebhookService`, `StripePaymentService`) sanitize IDs and API errors.
  - Replicate and image processing services sanitize model IDs, URLs, styles.
  - Deployment/health/storage diagnostics sanitize connection details, dependency names, and exception text.
- Tests:
  - Sanitizer behavior: `AI.ProfilePhotoMaker.API.Tests/Infrastructure/Logging/LoggingSanitizerTests.cs`
  - Stripe regressions: `StripeWebhookServiceTests`, `CreditControllerPaymentConfigTests`
  - Recommended quick check:
    ```bash
    dotnet test AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj \
      --configuration Release \
      --filter "LoggingSanitizerTests|StripeWebhookServiceTests|CreditControllerPaymentConfigTests"
    ```
- Full regression: `dotnet test AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj --configuration Release`
