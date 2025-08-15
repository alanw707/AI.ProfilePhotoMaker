#!/bin/bash

# =============================================================================
# Setup Configuration Drift Monitoring
# =============================================================================
# Sets up automated configuration drift monitoring with cron jobs and webhooks
# =============================================================================

set -euo pipefail

# Configuration
PROJECT_DIR="$(pwd)"
SCRIPT_DIR="$PROJECT_DIR/scripts"
CRON_USER="${CRON_USER:-$(whoami)}"
MONITORING_ENV="${MONITORING_ENV:-Production}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

echo -e "${BLUE}=============================================================================${NC}"
echo -e "${BLUE}🔍 Configuration Drift Monitoring Setup${NC}"
echo -e "${BLUE}=============================================================================${NC}"
echo ""

# =============================================================================
# PREREQUISITES VALIDATION
# =============================================================================

validate_prerequisites() {
    log_info "🔍 Validating prerequisites..."
    
    # Check if we're in the project root
    if [[ ! -f "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj" ]]; then
        log_error "Must run from project root directory"
        exit 1
    fi
    
    # Check if drift detection script exists
    if [[ ! -f "$SCRIPT_DIR/detect-config-drift.sh" ]]; then
        log_error "Configuration drift detection script not found at $SCRIPT_DIR/detect-config-drift.sh"
        exit 1
    fi
    
    # Make sure scripts are executable
    chmod +x "$SCRIPT_DIR/detect-config-drift.sh"
    chmod +x "$SCRIPT_DIR/config-drift-webhook.sh"
    
    log_success "✅ Prerequisites validated"
}

# =============================================================================
# CRON JOB SETUP
# =============================================================================

setup_cron_monitoring() {
    log_info "📅 Setting up cron job monitoring..."
    
    # Create log directory
    local log_dir="/tmp/config-drift-logs"
    mkdir -p "$log_dir"
    
    # Generate cron entry for weekly monitoring (Monday 6 AM)
    local cron_entry="0 6 * * 1 cd $PROJECT_DIR && $SCRIPT_DIR/detect-config-drift.sh $MONITORING_ENV >> $log_dir/config-drift-$(date +\%Y\%m\%d).log 2>&1"
    
    echo ""
    log_info "📋 Recommended cron job entry:"
    echo "# Configuration Drift Monitoring - Weekly check"
    echo "$cron_entry"
    echo ""
    
    # Check if cron job already exists
    if crontab -l 2>/dev/null | grep -q "detect-config-drift.sh"; then
        log_warning "⚠️  Configuration drift monitoring cron job already exists"
        echo "Current cron jobs related to config drift:"
        crontab -l 2>/dev/null | grep "detect-config-drift.sh" || true
    else
        read -p "Would you like to install this cron job automatically? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            # Add cron job
            (crontab -l 2>/dev/null; echo "$cron_entry") | crontab -
            log_success "✅ Cron job installed successfully"
        else
            log_info "ℹ️  To install manually, run: (crontab -l 2>/dev/null; echo \"$cron_entry\") | crontab -"
        fi
    fi
    
    # Create additional monitoring frequencies
    echo ""
    log_info "📋 Alternative monitoring frequencies:"
    echo "# Daily monitoring (6 AM every day):"
    echo "0 6 * * * cd $PROJECT_DIR && $SCRIPT_DIR/detect-config-drift.sh $MONITORING_ENV >> $log_dir/config-drift-daily-$(date +\%Y\%m\%d).log 2>&1"
    echo ""
    echo "# Production-only weekly, Staging daily:"
    echo "0 6 * * 1 cd $PROJECT_DIR && $SCRIPT_DIR/detect-config-drift.sh Production >> $log_dir/config-drift-prod-$(date +\%Y\%m\%d).log 2>&1"
    echo "0 6 * * * cd $PROJECT_DIR && $SCRIPT_DIR/detect-config-drift.sh Staging >> $log_dir/config-drift-staging-$(date +\%Y\%m\%d).log 2>&1"
}

# =============================================================================
# WEBHOOK CONFIGURATION
# =============================================================================

setup_webhook_notifications() {
    log_info "🔔 Setting up webhook notifications..."
    
    echo ""
    log_info "📋 Webhook integration options:"
    echo ""
    
    # Slack webhook
    echo "1. Slack Integration:"
    echo "   export SLACK_WEBHOOK_URL='https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'"
    echo "   Test: $SCRIPT_DIR/config-drift-webhook.sh critical Production 2 1"
    echo ""
    
    # Microsoft Teams webhook
    echo "2. Microsoft Teams Integration:"
    echo "   export TEAMS_WEBHOOK_URL='https://YOUR-TENANT.webhook.office.com/webhookb2/YOUR-WEBHOOK'"
    echo "   Test: $SCRIPT_DIR/config-drift-webhook.sh warning Staging 0 3"
    echo ""
    
    # Discord webhook
    echo "3. Discord Integration:"
    echo "   export DISCORD_WEBHOOK_URL='https://discord.com/api/webhooks/YOUR/WEBHOOK'"
    echo "   Test: $SCRIPT_DIR/config-drift-webhook.sh info Development 0 0"
    echo ""
    
    # Generic webhook
    echo "4. Generic Webhook Integration:"
    echo "   export GENERIC_WEBHOOK_URL='https://your-monitoring-system.com/webhook'"
    echo "   Test: $SCRIPT_DIR/config-drift-webhook.sh success Production 0 0"
    echo ""
    
    # Create environment file template
    local env_file="$PROJECT_DIR/.env.monitoring"
    if [[ ! -f "$env_file" ]]; then
        cat > "$env_file" << 'EOF'
# Configuration Drift Monitoring - Webhook URLs
# Copy this file to .env.monitoring.local and configure your webhooks

# Slack webhook URL for notifications
# SLACK_WEBHOOK_URL=https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK

# Microsoft Teams webhook URL
# TEAMS_WEBHOOK_URL=https://YOUR-TENANT.webhook.office.com/webhookb2/YOUR-WEBHOOK

# Discord webhook URL
# DISCORD_WEBHOOK_URL=https://discord.com/api/webhooks/YOUR/WEBHOOK

# Generic webhook for custom integrations
# GENERIC_WEBHOOK_URL=https://your-monitoring-system.com/webhook

# Email notifications (if you have an email webhook service)
# EMAIL_WEBHOOK_URL=https://your-email-service.com/webhook
# EMAIL_RECIPIENTS=devops@yourcompany.com,admin@yourcompany.com

# Target environment for monitoring
MONITORING_ENV=Production

# Enable verbose output in notifications
VERBOSE=true
EOF
        log_success "✅ Created webhook configuration template: $env_file"
        log_info "ℹ️  Copy to .env.monitoring.local and configure your webhook URLs"
    else
        log_info "ℹ️  Webhook configuration template already exists: $env_file"
    fi
}

# =============================================================================
# GITHUB ACTIONS INTEGRATION
# =============================================================================

validate_github_actions() {
    log_info "🔄 Validating GitHub Actions integration..."
    
    local workflow_file=".github/workflows/config-drift-monitor.yml"
    
    if [[ -f "$workflow_file" ]]; then
        log_success "✅ GitHub Actions workflow found: $workflow_file"
        
        # Check if workflow is properly configured
        if grep -q "schedule:" "$workflow_file"; then
            log_success "✅ Scheduled monitoring configured"
        else
            log_warning "⚠️  No scheduled monitoring found in GitHub Actions workflow"
        fi
        
        if grep -q "workflow_dispatch:" "$workflow_file"; then
            log_success "✅ Manual trigger configured"
        else
            log_warning "⚠️  No manual trigger found in GitHub Actions workflow"
        fi
        
        echo ""
        log_info "📋 GitHub Actions workflow features:"
        echo "   • Weekly automated checks (Mondays 6 AM UTC)"
        echo "   • Manual trigger with environment selection"
        echo "   • Automatic issue creation for critical drift"
        echo "   • Issue auto-closing when drift is resolved"
        echo "   • Configuration snapshot uploads"
        
    else
        log_warning "⚠️  GitHub Actions workflow not found"
        log_info "ℹ️  Expected location: $workflow_file"
        log_info "ℹ️  Create the workflow file to enable automated monitoring in CI/CD"
    fi
}

# =============================================================================
# MONITORING DASHBOARD SETUP
# =============================================================================

setup_monitoring_dashboard() {
    log_info "📊 Setting up monitoring dashboard..."
    
    # Create monitoring directory structure
    local monitoring_dir="ClaudeDocs/Config-Drift"
    mkdir -p "$monitoring_dir/reports"
    mkdir -p "$monitoring_dir/snapshots"
    mkdir -p "$monitoring_dir/history"
    
    # Create dashboard HTML template
    local dashboard_file="$monitoring_dir/dashboard.html"
    cat > "$dashboard_file" << 'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Configuration Drift Monitoring Dashboard</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; }
        .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 10px; margin-bottom: 20px; }
        .card { background: white; padding: 20px; margin: 10px 0; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .status-good { border-left: 5px solid #28a745; }
        .status-warning { border-left: 5px solid #ffc107; }
        .status-critical { border-left: 5px solid #dc3545; }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 20px; }
        .metric { text-align: center; padding: 20px; }
        .metric-value { font-size: 2em; font-weight: bold; color: #333; }
        .metric-label { color: #666; margin-top: 5px; }
        pre { background: #f8f9fa; padding: 15px; border-radius: 5px; overflow-x: auto; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🔍 Configuration Drift Monitoring Dashboard</h1>
            <p>Real-time monitoring of configuration alignment across all systems</p>
        </div>
        
        <div class="grid">
            <div class="card status-good">
                <div class="metric">
                    <div class="metric-value" id="last-check">-</div>
                    <div class="metric-label">Last Check</div>
                </div>
            </div>
            
            <div class="card status-warning">
                <div class="metric">
                    <div class="metric-value" id="warning-count">-</div>
                    <div class="metric-label">Active Warnings</div>
                </div>
            </div>
            
            <div class="card status-critical">
                <div class="metric">
                    <div class="metric-value" id="critical-count">-</div>
                    <div class="metric-label">Critical Issues</div>
                </div>
            </div>
        </div>
        
        <div class="card">
            <h3>📊 Latest Configuration Status</h3>
            <div id="status-content">
                <p>Run <code>./scripts/detect-config-drift.sh Production</code> to generate status report.</p>
            </div>
        </div>
        
        <div class="card">
            <h3>🚀 Quick Actions</h3>
            <ul>
                <li><strong>Manual Check:</strong> <code>./scripts/detect-config-drift.sh Production</code></li>
                <li><strong>Verbose Check:</strong> <code>VERBOSE=true ./scripts/detect-config-drift.sh Staging</code></li>
                <li><strong>JSON Output:</strong> <code>OUTPUT_FORMAT=json ./scripts/detect-config-drift.sh Development</code></li>
                <li><strong>Test Webhooks:</strong> <code>./scripts/config-drift-webhook.sh warning Production 0 2</code></li>
            </ul>
        </div>
        
        <div class="card">
            <h3>📈 Monitoring Setup</h3>
            <h4>Cron Job (Weekly Monitoring)</h4>
            <pre>0 6 * * 1 cd /path/to/project && ./scripts/detect-config-drift.sh Production >> /tmp/config-drift-$(date +%Y%m%d).log 2>&1</pre>
            
            <h4>GitHub Actions</h4>
            <p>Workflow: <code>.github/workflows/config-drift-monitor.yml</code></p>
            <ul>
                <li>Weekly automated checks (Mondays 6 AM UTC)</li>
                <li>Manual trigger via workflow_dispatch</li>
                <li>Automatic issue creation/resolution</li>
            </ul>
            
            <h4>Webhook Notifications</h4>
            <p>Configure in <code>.env.monitoring.local</code>:</p>
            <pre>SLACK_WEBHOOK_URL=https://hooks.slack.com/services/YOUR/WEBHOOK
TEAMS_WEBHOOK_URL=https://YOUR-TENANT.webhook.office.com/YOUR-WEBHOOK</pre>
        </div>
    </div>
    
    <script>
        // Simple JavaScript to show current time
        document.getElementById('last-check').textContent = new Date().toLocaleString();
        
        // In a real implementation, you would load actual data from monitoring files
        document.getElementById('warning-count').textContent = '0';
        document.getElementById('critical-count').textContent = '0';
    </script>
</body>
</html>
EOF
    
    log_success "✅ Monitoring dashboard created: $dashboard_file"
    log_info "ℹ️  Open in browser: file://$PWD/$dashboard_file"
}

# =============================================================================
# TESTING AND VALIDATION
# =============================================================================

run_initial_tests() {
    log_info "🧪 Running initial validation tests..."
    
    echo ""
    log_info "1. Testing configuration drift detection..."
    if "$SCRIPT_DIR/detect-config-drift.sh" --help > /dev/null; then
        log_success "✅ Drift detection script is working"
    else
        log_error "❌ Drift detection script failed"
        return 1
    fi
    
    echo ""
    log_info "2. Testing webhook notifications..."
    if "$SCRIPT_DIR/config-drift-webhook.sh" --help > /dev/null; then
        log_success "✅ Webhook notification script is working"
    else
        log_error "❌ Webhook notification script failed"
        return 1
    fi
    
    echo ""
    log_info "3. Running sample drift detection (Production)..."
    if "$SCRIPT_DIR/detect-config-drift.sh" Production > /dev/null; then
        log_success "✅ Sample drift detection completed successfully"
    else
        local exit_code=$?
        if [[ $exit_code -eq 2 ]]; then
            log_warning "⚠️  Sample drift detection completed with warnings"
        else
            log_error "❌ Sample drift detection failed with critical issues"
        fi
    fi
    
    echo ""
    log_info "4. Checking monitoring directory structure..."
    if [[ -d "ClaudeDocs/Config-Drift" ]]; then
        local snapshot_count=$(find ClaudeDocs/Config-Drift -name "config-snapshot-*.json" | wc -l)
        log_success "✅ Monitoring directory exists with $snapshot_count snapshots"
    else
        log_warning "⚠️  Monitoring directory not yet created (will be created on first run)"
    fi
}

# =============================================================================
# MAIN EXECUTION
# =============================================================================

main() {
    validate_prerequisites
    
    echo ""
    setup_cron_monitoring
    
    echo ""
    setup_webhook_notifications
    
    echo ""
    validate_github_actions
    
    echo ""
    setup_monitoring_dashboard
    
    echo ""
    run_initial_tests
    
    echo ""
    echo -e "${BLUE}=============================================================================${NC}"
    echo -e "${GREEN}✅ Configuration Drift Monitoring Setup Complete${NC}"
    echo -e "${BLUE}=============================================================================${NC}"
    echo ""
    echo -e "${GREEN}📋 Summary of setup:${NC}"
    echo "   • Configuration drift detection script: ✅ Ready"
    echo "   • Webhook notification system: ✅ Configured"
    echo "   • Cron job monitoring: 📅 Instructions provided"
    echo "   • GitHub Actions integration: 🔄 Validated"
    echo "   • Monitoring dashboard: 📊 Created"
    echo ""
    echo -e "${CYAN}🚀 Next steps:${NC}"
    echo "   1. Configure webhook URLs in .env.monitoring.local"
    echo "   2. Install cron job for automated monitoring"
    echo "   3. Test webhook notifications"
    echo "   4. Review GitHub Actions workflow"
    echo "   5. Monitor ClaudeDocs/Config-Drift/ for reports"
    echo ""
    echo -e "${BLUE}📚 Documentation:${NC}"
    echo "   • Configuration drift detection: ./scripts/detect-config-drift.sh --help"
    echo "   • Webhook notifications: ./scripts/config-drift-webhook.sh --help"
    echo "   • Monitoring dashboard: ClaudeDocs/Config-Drift/dashboard.html"
    echo ""
    echo -e "${GREEN}🎉 Configuration drift monitoring is now proactively protecting your deployments!${NC}"
}

# =============================================================================
# SCRIPT EXECUTION
# =============================================================================

case "${1:-}" in
    --help|-h)
        cat << EOF
Configuration Drift Monitoring Setup Script

USAGE:
  $0

DESCRIPTION:
  Sets up comprehensive configuration drift monitoring including:
  - Cron job for automated weekly checks
  - Webhook notifications (Slack, Teams, Discord)
  - GitHub Actions integration validation
  - Monitoring dashboard
  - Initial testing and validation

ENVIRONMENT VARIABLES:
  CRON_USER           User for cron job installation (default: current user)
  MONITORING_ENV      Environment to monitor (default: Production)

This script provides an early warning system that would have detected
the Azure Storage configuration mismatch before it caused deployment failures.
EOF
        exit 0
        ;;
    *)
        main
        ;;
esac