# Code Patterns - AI.ProfilePhotoMaker

## Database Connection Patterns

### Connection String Management (Updated 2025-08-08)
```csharp
// Production Azure SQL Database
"Server=tcp:aipm-sql-v1-6j74jubocuukg.database.windows.net,1433;Initial Catalog=aipmdb;User ID=sqladmin;Password={secure_password};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

// Local Development SQL Server  
"Server=localhost,1433;Database=AIProfileMaker;User Id=sa;Password=Dev123456!;TrustServerCertificate=true;MultipleActiveResultSets=true;"
```

### Database Provider Configuration Pattern
```csharp
// Services/Database/DatabaseProviderService.cs
public void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder<TContext> options, string? connectionString = null) 
    where TContext : DbContext
{
    var connString = connectionString ?? GetConnectionString();
    options.UseSqlServer(connString, sqlServerOptions =>
    {
        sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: config.MaxRetryCount,
            maxRetryDelay: config.MaxRetryDelay,
            errorNumbersToAdd: null);
        sqlServerOptions.CommandTimeout(config.CommandTimeout);
    });
}
```

### Connection Testing Pattern  
```csharp
// Built-in database connectivity testing
public async Task<bool> CanConnectAsync()
{
    try
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        ConfigureDbContextOptions(optionsBuilder);
        
        using var context = new ApplicationDbContext(optionsBuilder.Options);
        return await context.Database.CanConnectAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Database connectivity test failed");
        return false;
    }
}
```

## Configuration Management Patterns

### Environment-Specific Configuration (Updated 2025-08-08)
```json
// appsettings.Development.json - Local development
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=AIProfileMaker;User Id=sa;Password=Dev123456!;TrustServerCertificate=true;MultipleActiveResultSets=true;"
  },
  "Database": {
    "AutoMigrateOnStartup": true,
    "ValidateOnStartup": true,
    "EnableSensitiveDataLogging": true
  }
}

// appsettings.Production.json - Production environment
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:aipm-sql-v1-6j74jubocuukg.database.windows.net,1433;Initial Catalog=aipmdb;User ID=sqladmin;Password=REPLACE_WITH_SECURE_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Database": {
    "AutoMigrateOnStartup": false,
    "ValidateOnStartup": true,
    "EnableSensitiveDataLogging": false
  }
}
```

### Secret Management Pattern (New 2025-08-08)
```bash
# .NET User Secrets for local development
dotnet user-secrets set "ConnectionStrings:ProductionConnection" "Server=tcp:...;Password={secure_password};..."

# GitHub Secrets for CI/CD
gh secret set SQL_ADMIN_PASSWORD --body "{secure_password}"

# Azure Key Vault for production
az keyvault secret set --vault-name "{key-vault-name}" --name "SQL-ADMIN-PASSWORD" --value "{secure_password}"
```

## VS Code Configuration Patterns (New 2025-08-08)

### Clean MSSQL Extension Configuration
```json
// .vscode/settings.json
{
    "mssql.connections": [
        {
            "server": "localhost,1433",
            "database": "AIProfileMaker",
            "authenticationType": "SqlLogin",
            "user": "sa",
            "password": "",
            "profileName": "🐳 Local Development",
            "savePassword": false,
            "groupId": "local-dev"
        },
        {
            "server": "aipm-sql-v1-6j74jubocuukg.database.windows.net,1433",
            "database": "aipmdb", 
            "authenticationType": "SqlLogin",
            "user": "sqladmin",
            "password": "",
            "profileName": "☁️ Production Azure",
            "savePassword": false,
            "groupId": "production"
        }
    ],
    "mssql.maxRecentConnections": 2,
    "mssql.enableConnectionTimeout": true,
    "mssql.connectionTimeout": 30
}
```

### Connection Prevention Pattern
- **Profile Naming**: Use emojis (🐳, ☁️) for visual distinction
- **Password Policy**: `savePassword: false` to prevent storage issues
- **History Limits**: `maxRecentConnections: 2` to prevent accumulation
- **Group Organization**: `groupId` for logical organization
- **Method Selection**: Prefer "Connection String" over "Browse Azure" for reliability

## Command-line Patterns

### Database Connectivity Testing
```bash
# Test with environment variables
export ConnectionStrings__DefaultConnection="Server=tcp:...;Password={password};..."
export ASPNETCORE_ENVIRONMENT="Production"
dotnet run --check-db-connection

# Direct connection testing
timeout 10 bash -c "</dev/tcp/{server}/1433" && echo "✅ Port 1433 reachable"
```

### Azure CLI Integration
```bash
# SQL Server management
az sql server update --name {server-name} --resource-group {rg} --admin-password "{new_password}"
az sql server firewall-rule create --server {server} --resource-group {rg} --name "ClientIP-$(date +%Y-%m-%d-%H-%M)" --start-ip-address {ip} --end-ip-address {ip}

# Key Vault management
az role assignment create --assignee {user-id} --role "Key Vault Secrets Officer" --scope {key-vault-scope}
az keyvault secret set --vault-name {vault} --name "SQL-ADMIN-PASSWORD" --value "{password}"
```

## Authentication Patterns (Updated)

### OAuth Integration Pattern (Existing)
```csharp
// Hybrid authentication supporting both cookie and JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options => { /* cookie configuration */ })
.AddJwtBearer(options => { /* JWT configuration */ })
.AddGoogle(options => { /* Google OAuth configuration */ });
```

### Database Authentication Pattern (Updated 2025-08-08)
- **Local**: SQL Server authentication (`sa` user for development)
- **Production**: SQL Server authentication (`sqladmin` user with secure password)
- **Future**: Consider migrating to Managed Identity for enhanced security

## Error Handling Patterns

### Connection Retry Pattern
```csharp
// Built into DatabaseProviderService
sqlServerOptions.EnableRetryOnFailure(
    maxRetryCount: config.MaxRetryCount,      // Default: 5
    maxRetryDelay: config.MaxRetryDelay,      // Default: 30 seconds  
    errorNumbersToAdd: null);
```

### Connection Timeout Handling
```csharp
sqlServerOptions.CommandTimeout(config.CommandTimeout); // Default: 30 seconds
```

## Troubleshooting Patterns (New 2025-08-08)

### Multi-Layer Validation Approach
1. **Network**: Test port connectivity (`nc -z server 1433`)
2. **Authentication**: Test with known credentials
3. **Application**: Use built-in app testing (`dotnet run --check-db-connection`)
4. **End-to-End**: Test full application flow

### Configuration Cleanup Pattern
1. **Nuclear Approach**: Complete reset when incremental fixes fail
2. **Process Management**: Kill relevant processes before cleanup
3. **Data Cleanup**: Clear extension data directories
4. **Prevention Settings**: Apply settings to prevent recurrence
5. **Validation**: Multi-system testing after changes

### Secret Synchronization Pattern
1. **Generate**: Create secure password meeting all requirements
2. **Distribute**: Update all storage locations (secrets, Key Vault, etc.)
3. **Apply**: Update actual target system (SQL Server password)
4. **Validate**: Test authentication end-to-end
5. **Document**: Record password location and update procedures

## Performance Patterns

### Database Configuration Optimization
```json
{
  "Database": {
    "MaxRetryCount": 5,
    "MaxRetryDelaySeconds": 30, 
    "CommandTimeoutSeconds": 30,
    "EnableSensitiveDataLogging": false, // Production
    "EnableDetailedErrors": false        // Production
  }
}
```

### Connection Pooling (Implicit)
- Entity Framework handles connection pooling automatically
- MultipleActiveResultSets enabled for local development
- Connection timeout configured for both local and production

## Security Patterns (Enhanced 2025-08-08)

### Password Complexity Requirements
- **Minimum 12 characters**
- **Mixed case letters, numbers, special characters**  
- **Avoid similarity to username** (critical for Azure SQL)
- **Example**: `Database!2024#Secure9$` (meets all requirements)

### Multi-Location Secret Storage
1. **Development**: .NET User Secrets (encrypted local storage)
2. **CI/CD**: GitHub Repository Secrets (encrypted at rest)
3. **Production**: Azure Key Vault (enterprise-grade secret management)
4. **Target System**: Direct password on SQL Server instance

### Access Control Pattern
- **Principle of Least Privilege**: Grant minimal required permissions
- **RBAC**: Use role-based access control (Key Vault Secrets Officer)
- **IP Restrictions**: Azure SQL firewall rules for network-level security
- **Encryption**: Mandatory TLS for all database connections