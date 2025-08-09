# Technical Decisions - AI.ProfilePhotoMaker Project

## Authentication & Security Architecture

### OAuth Integration Strategy (2025-01-31)
- **Decision**: Implement Google OAuth with ASP.NET Core Identity + JWT Bearer tokens
- **Rationale**: Provides secure authentication while maintaining API flexibility
- **Implementation**: 
  - OAuth for web interface authentication
  - JWT tokens for API access
  - Hybrid approach supporting both cookie and token auth

### Database Migration Approach (2025-08-08)
- **Decision**: Disable automatic migrations for MVP simplicity
- **Rationale**: Manual control over production schema changes
- **Configuration**: `AutoMigrateOnStartup: false` in production settings

### SQL Server Password Management (2025-08-08)
- **Decision**: Multi-location secure password storage strategy
- **Storage Locations**:
  - .NET User Secrets (development)
  - GitHub Repository Secrets (CI/CD)
  - Azure Key Vault (production)
  - Azure SQL Server (actual authentication)
- **Password Policy**: Complex passwords avoiding username similarity
- **Selected Password**: `Database!2024#Secure9$` (meets Azure complexity requirements)

## Development Environment

### Local SQL Server Strategy (Updated 2025-08-08)
- **Decision**: Docker SQL Server 2022 for local development
- **Configuration**: 
  - Container: `mcr.microsoft.com/mssql/server:2022-latest`
  - Credentials: `sa` / `Dev123456!`
  - Port: `1433`
- **Rationale**: Consistent local environment matching production SQL Server

### VS Code MSSQL Extension Configuration (2025-08-08)
- **Decision**: Use Connection String method over Browse Azure
- **Rationale**: More reliable connection, avoids Azure authentication token issues
- **Prevention Settings**: 
  - `maxRecentConnections: 2`
  - `savePassword: false`
  - Locked connection history file to prevent duplicates

### Connection Profile Design (2025-08-08)
- **Decision**: Emoji-based profile naming for easy identification
- **Profiles**:
  - 🐳 Local Development (Docker SQL Server)
  - ☁️ Production Azure (Azure SQL Database)
- **Rationale**: Visual distinction prevents connection errors

## Production Infrastructure

### Azure SQL Database Configuration (Current)
- **Server**: `aipm-sql-v1-6j74jubocuukg.database.windows.net`
- **Database**: `aipmdb` 
- **Admin User**: `sqladmin`
- **Tier**: Basic (suitable for MVP)
- **Encryption**: Mandatory TLS with certificate validation

### Azure Key Vault Integration (2025-08-08)
- **Key Vault**: `aipm-kv-v1-6j74jubocuukg`
- **Secret Management**: Centralized secret storage for production
- **Access Control**: RBAC with Key Vault Secrets Officer role
- **Integration**: Used by Container Apps via Managed Identity

### Firewall Strategy (2025-08-08)
- **Approach**: IP-based access control with Azure Services allowlist
- **Current Rules**: 
  - AllowAzureServices (0.0.0.0 range)
  - Client IP allowlisting for development access
- **Management**: Dynamic rule creation for development IPs

## Code Quality & Maintenance

### Cleanup Strategy (2025-08-08)
- **Principle**: "Keep it simple" - remove temporary artifacts
- **Approach**: Systematic cleanup of troubleshooting scripts after resolution
- **Validation**: Ensure functionality preserved after cleanup
- **Documentation**: Capture knowledge before removing temporary tools

### Connection Management Philosophy
- **Principle**: Minimal, clean configuration over complex setups
- **Prevention**: Proactive measures to prevent configuration pollution
- **Troubleshooting**: Nuclear cleanup approach when incremental fixes fail
- **Validation**: Multi-layer testing (network, auth, application level)

## Architecture Patterns

### Database Provider Architecture (Current)
- **Decision**: Hardcoded SQL Server provider with retry policies
- **Configuration**: Centralized database service configuration
- **Settings**: Environment-specific timeout and retry configurations
- **Health Checks**: Built-in database connectivity validation

### Secret Management Pattern
- **Layered Approach**: Different secrets storage for different environments
- **Synchronization**: Manual sync required between secret stores and actual systems
- **Validation**: Multi-system testing to ensure consistency
- **Security**: Principle of least privilege with RBAC

## Lessons Learned

### Password vs Secret Storage (2025-08-08)
- **Key Insight**: Secret storage ≠ actual system password
- **Implication**: Must update both secret stores AND target system
- **Prevention**: Always validate end-to-end authentication, not just secret existence

### VS Code Extension Behavior (2025-08-08)
- **Observation**: Extensions can aggressively pollute configuration
- **Strategy**: Implement prevention settings alongside cleanup tools
- **Method Selection**: Prefer simpler, more reliable connection methods

### Troubleshooting Methodology
- **Network First**: Always verify basic connectivity before diving into authentication
- **Systematic Isolation**: Use built-in app testing to isolate specific issues
- **Clean State Recovery**: Sometimes complete reset more effective than incremental fixes

### Multi-System Integration Complexity
- **Challenge**: Synchronizing state across 4+ different systems
- **Approach**: Step-by-step validation at each integration point
- **Documentation**: Capture complex multi-system procedures for future reference

## Future Considerations

### Scalability Preparations
- **Database**: Basic tier suitable for MVP, prepared for upgrade
- **Connection Management**: Clean patterns established for scaling
- **Secret Rotation**: Infrastructure prepared for password rotation procedures

### Security Enhancements
- **Managed Identity**: Consider migrating from SQL authentication to Managed Identity
- **Certificate-based Auth**: Explore certificate authentication for enhanced security
- **Secret Rotation**: Implement automated secret rotation procedures

### Development Experience
- **Automation**: Consider scripting common database operations
- **Testing**: Expand automated connection testing capabilities
- **Documentation**: Maintain clear connection procedures for team onboarding