#!/bin/bash

# Database Migration Script for Docker/Container Deployment
# Supports comprehensive database migration and validation

set -e

# Configuration
ASSEMBLY_NAME="AI.ProfilePhotoMaker.API.dll"
TIMEOUT_SECONDS=300
MAX_RETRIES=3
RETRY_DELAY=10

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to run migration command with retry logic
run_migration_command() {
    local command="$1"
    local description="$2"
    local retry_count=0
    
    log_info "Starting: $description"
    
    while [ $retry_count -lt $MAX_RETRIES ]; do
        if timeout $TIMEOUT_SECONDS dotnet "$ASSEMBLY_NAME" "$command"; then
            log_success "$description completed successfully"
            return 0
        else
            retry_count=$((retry_count + 1))
            if [ $retry_count -lt $MAX_RETRIES ]; then
                log_warning "$description failed (attempt $retry_count/$MAX_RETRIES). Retrying in ${RETRY_DELAY}s..."
                sleep $RETRY_DELAY
            else
                log_error "$description failed after $MAX_RETRIES attempts"
                return 1
            fi
        fi
    done
}

# Main migration process
main() {
    log_info "Starting database migration process"
    log_info "Assembly: $ASSEMBLY_NAME"
    log_info "Environment: ${ASPNETCORE_ENVIRONMENT:-Production}"
    
    # Step 1: Check database connection
    log_info "Step 1/5: Testing database connectivity"
    if ! run_migration_command "--check-db-connection" "Database connection test"; then
        log_error "Cannot connect to database. Check connection string and database availability."
        exit 1
    fi
    
    # Step 2: Check migration status
    log_info "Step 2/5: Checking migration status"
    if ! run_migration_command "--migration-status" "Migration status check"; then
        log_warning "Migration status check failed. Continuing with migration attempt..."
    fi
    
    # Step 3: Apply migrations
    log_info "Step 3/5: Applying database migrations"
    if ! run_migration_command "--apply-migrations" "Database migration"; then
        log_error "Database migration failed"
        exit 1
    fi
    
    # Step 4: Verify migrations
    log_info "Step 4/5: Verifying migrations"
    if ! run_migration_command "--verify-migrations" "Migration verification"; then
        log_error "Migration verification failed"
        exit 1
    fi
    
    # Step 5: Validate database
    log_info "Step 5/5: Validating database structure and data"
    if ! run_migration_command "--validate-database" "Database validation"; then
        log_warning "Database validation found issues, but migration is complete"
    fi
    
    log_success "Database migration process completed successfully"
    
    # Optional: Display database health
    log_info "Final health check..."
    run_migration_command "--database-health" "Database health check" || log_warning "Health check failed, but migration is complete"
}

# Handle script arguments
case "${1:-migrate}" in
    "migrate")
        main
        ;;
    "check")
        run_migration_command "--check-db-connection" "Database connection test"
        ;;
    "status")
        run_migration_command "--migration-status" "Migration status check"
        ;;
    "validate")
        run_migration_command "--validate-database" "Database validation"
        ;;
    "health")
        run_migration_command "--database-health" "Database health check"
        ;;
    "help"|"--help"|"-h")
        echo "Database Migration Script"
        echo ""
        echo "Usage: $0 [command]"
        echo ""
        echo "Commands:"
        echo "  migrate   - Run complete migration process (default)"
        echo "  check     - Test database connection only"
        echo "  status    - Show migration status"
        echo "  validate  - Validate database structure and data"
        echo "  health    - Show comprehensive database health"
        echo "  help      - Show this help message"
        echo ""
        echo "Environment Variables:"
        echo "  ASPNETCORE_ENVIRONMENT - Set the environment (Development/Staging/Production)"
        echo "  ConnectionStrings__DefaultConnection - Database connection string"
        echo ""
        echo "Exit Codes:"
        echo "  0 - Success"
        echo "  1 - Migration or validation failed"
        ;;
    *)
        log_error "Unknown command '$1'. Use 'help' for usage information."
        exit 1
        ;;
esac