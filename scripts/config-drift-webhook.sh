#!/bin/bash

# =============================================================================
# Configuration Drift Monitoring Webhook Integration
# =============================================================================
# Sends alerts when configuration drift is detected
# Supports Slack, Microsoft Teams, Discord, and generic webhooks
# =============================================================================

set -euo pipefail

# Configuration
DRIFT_LEVEL="${1:-warning}"  # critical, warning, info
TARGET_ENV="${2:-Production}"
CRITICAL_COUNT="${3:-0}"
WARNING_COUNT="${4:-0}"

# Webhook URLs from environment variables
SLACK_WEBHOOK_URL="${SLACK_WEBHOOK_URL:-}"
TEAMS_WEBHOOK_URL="${TEAMS_WEBHOOK_URL:-}"
DISCORD_WEBHOOK_URL="${DISCORD_WEBHOOK_URL:-}"
GENERIC_WEBHOOK_URL="${GENERIC_WEBHOOK_URL:-}"

# Colors for different platforms
declare -A COLORS=(
    ["slack_critical"]="#FF0000"
    ["slack_warning"]="#FFA500"
    ["slack_info"]="#36A64F"
    ["teams_critical"]="attention"
    ["teams_warning"]="warning"
    ["teams_info"]="good"
)

# Icons for different drift levels
declare -A ICONS=(
    ["critical"]="🚨"
    ["warning"]="⚠️"
    ["info"]="ℹ️"
    ["success"]="✅"
)

# Generate timestamp
TIMESTAMP=$(date -u '+%Y-%m-%d %H:%M:%S UTC')

# =============================================================================
# HELPER FUNCTIONS
# =============================================================================

log_info() { 
    echo "[INFO] $1" >&2
}

log_error() { 
    echo "[ERROR] $1" >&2
}

get_severity_message() {
    local level="$1"
    case "$level" in
        "critical")
            echo "Critical configuration drift detected that may cause deployment failures"
            ;;
        "warning")
            echo "Configuration drift warnings detected that should be addressed"
            ;;
        "info")
            echo "Configuration drift check completed with informational items"
            ;;
        "success")
            echo "No configuration drift detected - all systems aligned"
            ;;
        *)
            echo "Configuration drift check completed"
            ;;
    esac
}

get_action_message() {
    local level="$1"
    case "$level" in
        "critical")
            echo "Immediate action required to prevent deployment failures"
            ;;
        "warning")
            echo "Review and address warnings to maintain optimal alignment"
            ;;
        "info")
            echo "Review informational items for potential improvements"
            ;;
        "success")
            echo "No action required - continue monitoring"
            ;;
        *)
            echo "Review drift detection results"
            ;;
    esac
}

# =============================================================================
# SLACK WEBHOOK
# =============================================================================

send_slack_notification() {
    if [[ -z "$SLACK_WEBHOOK_URL" ]]; then
        return 0
    fi
    
    local color="${COLORS[slack_${DRIFT_LEVEL}]:-#808080}"
    local icon="${ICONS[$DRIFT_LEVEL]:-🔍}"
    local severity_msg=$(get_severity_message "$DRIFT_LEVEL")
    local action_msg=$(get_action_message "$DRIFT_LEVEL")
    
    local payload=$(cat << EOF
{
    "username": "Config Drift Monitor",
    "icon_emoji": ":warning:",
    "attachments": [
        {
            "color": "$color",
            "title": "$icon Configuration Drift Alert - $TARGET_ENV",
            "fields": [
                {
                    "title": "Environment",
                    "value": "$TARGET_ENV",
                    "short": true
                },
                {
                    "title": "Severity",
                    "value": "$DRIFT_LEVEL",
                    "short": true
                },
                {
                    "title": "Critical Issues",
                    "value": "$CRITICAL_COUNT",
                    "short": true
                },
                {
                    "title": "Warnings",
                    "value": "$WARNING_COUNT",
                    "short": true
                },
                {
                    "title": "Detection Time",
                    "value": "$TIMESTAMP",
                    "short": false
                },
                {
                    "title": "Summary",
                    "value": "$severity_msg",
                    "short": false
                },
                {
                    "title": "Action Required",
                    "value": "$action_msg",
                    "short": false
                }
            ],
            "footer": "AI Profile Photo Maker - Config Drift Monitor",
            "ts": $(date +%s)
        }
    ]
}
EOF
)
    
    if curl -s -X POST "$SLACK_WEBHOOK_URL" \
        -H "Content-Type: application/json" \
        -d "$payload" > /dev/null; then
        log_info "Slack notification sent successfully"
    else
        log_error "Failed to send Slack notification"
    fi
}

# =============================================================================
# MICROSOFT TEAMS WEBHOOK
# =============================================================================

send_teams_notification() {
    if [[ -z "$TEAMS_WEBHOOK_URL" ]]; then
        return 0
    fi
    
    local theme_color=""
    case "$DRIFT_LEVEL" in
        "critical") theme_color="FF0000" ;;
        "warning") theme_color="FFA500" ;;
        "info") theme_color="36A64F" ;;
        *) theme_color="808080" ;;
    esac
    
    local icon="${ICONS[$DRIFT_LEVEL]:-🔍}"
    local severity_msg=$(get_severity_message "$DRIFT_LEVEL")
    local action_msg=$(get_action_message "$DRIFT_LEVEL")
    
    local payload=$(cat << EOF
{
    "@type": "MessageCard",
    "@context": "http://schema.org/extensions",
    "themeColor": "$theme_color",
    "summary": "Configuration Drift Alert - $TARGET_ENV",
    "sections": [
        {
            "activityTitle": "$icon Configuration Drift Alert",
            "activitySubtitle": "$TARGET_ENV Environment",
            "activityImage": "https://github.com/microsoft/teams-ai/raw/main/assets/icon.png",
            "facts": [
                {
                    "name": "Environment",
                    "value": "$TARGET_ENV"
                },
                {
                    "name": "Severity",
                    "value": "$DRIFT_LEVEL"
                },
                {
                    "name": "Critical Issues",
                    "value": "$CRITICAL_COUNT"
                },
                {
                    "name": "Warnings",
                    "value": "$WARNING_COUNT"
                },
                {
                    "name": "Detection Time",
                    "value": "$TIMESTAMP"
                }
            ],
            "markdown": true
        },
        {
            "text": "**Summary:** $severity_msg"
        },
        {
            "text": "**Action Required:** $action_msg"
        }
    ],
    "potentialAction": [
        {
            "@type": "OpenUri",
            "name": "View GitHub Repository",
            "targets": [
                {
                    "os": "default",
                    "uri": "https://github.com/alanw707/AI.ProfilePhotoMaker"
                }
            ]
        }
    ]
}
EOF
)
    
    if curl -s -X POST "$TEAMS_WEBHOOK_URL" \
        -H "Content-Type: application/json" \
        -d "$payload" > /dev/null; then
        log_info "Teams notification sent successfully"
    else
        log_error "Failed to send Teams notification"
    fi
}

# =============================================================================
# DISCORD WEBHOOK
# =============================================================================

send_discord_notification() {
    if [[ -z "$DISCORD_WEBHOOK_URL" ]]; then
        return 0
    fi
    
    local color=""
    case "$DRIFT_LEVEL" in
        "critical") color="16711680" ;;  # Red
        "warning") color="16753920" ;;   # Orange
        "info") color="3570816" ;;       # Green
        *) color="8421504" ;;            # Gray
    esac
    
    local icon="${ICONS[$DRIFT_LEVEL]:-🔍}"
    local severity_msg=$(get_severity_message "$DRIFT_LEVEL")
    local action_msg=$(get_action_message "$DRIFT_LEVEL")
    
    local payload=$(cat << EOF
{
    "username": "Config Drift Monitor",
    "embeds": [
        {
            "title": "$icon Configuration Drift Alert - $TARGET_ENV",
            "description": "$severity_msg",
            "color": $color,
            "fields": [
                {
                    "name": "Environment",
                    "value": "$TARGET_ENV",
                    "inline": true
                },
                {
                    "name": "Severity",
                    "value": "$DRIFT_LEVEL",
                    "inline": true
                },
                {
                    "name": "Critical Issues",
                    "value": "$CRITICAL_COUNT",
                    "inline": true
                },
                {
                    "name": "Warnings",
                    "value": "$WARNING_COUNT",
                    "inline": true
                },
                {
                    "name": "Detection Time",
                    "value": "$TIMESTAMP",
                    "inline": false
                },
                {
                    "name": "Action Required",
                    "value": "$action_msg",
                    "inline": false
                }
            ],
            "footer": {
                "text": "AI Profile Photo Maker - Config Drift Monitor"
            },
            "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
        }
    ]
}
EOF
)
    
    if curl -s -X POST "$DISCORD_WEBHOOK_URL" \
        -H "Content-Type: application/json" \
        -d "$payload" > /dev/null; then
        log_info "Discord notification sent successfully"
    else
        log_error "Failed to send Discord notification"
    fi
}

# =============================================================================
# GENERIC WEBHOOK
# =============================================================================

send_generic_notification() {
    if [[ -z "$GENERIC_WEBHOOK_URL" ]]; then
        return 0
    fi
    
    local severity_msg=$(get_severity_message "$DRIFT_LEVEL")
    local action_msg=$(get_action_message "$DRIFT_LEVEL")
    
    local payload=$(cat << EOF
{
    "event": "configuration_drift_detected",
    "timestamp": "$TIMESTAMP",
    "environment": "$TARGET_ENV",
    "severity": "$DRIFT_LEVEL",
    "metrics": {
        "critical_issues": $CRITICAL_COUNT,
        "warning_issues": $WARNING_COUNT
    },
    "summary": "$severity_msg",
    "action_required": "$action_msg",
    "source": "AI.ProfilePhotoMaker.ConfigDriftMonitor"
}
EOF
)
    
    if curl -s -X POST "$GENERIC_WEBHOOK_URL" \
        -H "Content-Type: application/json" \
        -d "$payload" > /dev/null; then
        log_info "Generic webhook notification sent successfully"
    else
        log_error "Failed to send generic webhook notification"
    fi
}

# =============================================================================
# EMAIL NOTIFICATION (via webhook)
# =============================================================================

send_email_notification() {
    local email_webhook="${EMAIL_WEBHOOK_URL:-}"
    if [[ -z "$email_webhook" ]]; then
        return 0
    fi
    
    local severity_msg=$(get_severity_message "$DRIFT_LEVEL")
    local action_msg=$(get_action_message "$DRIFT_LEVEL")
    local icon="${ICONS[$DRIFT_LEVEL]:-🔍}"
    
    local subject="$icon Configuration Drift Alert - $TARGET_ENV Environment"
    local body=$(cat << EOF
Configuration Drift Detection Alert

Environment: $TARGET_ENV
Severity: $DRIFT_LEVEL
Detection Time: $TIMESTAMP

Summary:
$severity_msg

Metrics:
- Critical Issues: $CRITICAL_COUNT
- Warning Issues: $WARNING_COUNT

Action Required:
$action_msg

This is an automated alert from the AI Profile Photo Maker Configuration Drift Monitor.

To investigate:
1. Review the latest GitHub Actions workflow run
2. Check scripts/detect-config-drift.sh output
3. Compare application configuration with infrastructure definitions
4. Address any naming mismatches or missing variables

For more information, visit: https://github.com/alanw707/AI.ProfilePhotoMaker
EOF
)
    
    local email_payload=$(cat << EOF
{
    "to": "${EMAIL_RECIPIENTS:-devops@example.com}",
    "subject": "$subject",
    "body": "$body",
    "priority": "$(if [[ "$DRIFT_LEVEL" == "critical" ]]; then echo "high"; else echo "normal"; fi)"
}
EOF
)
    
    if curl -s -X POST "$email_webhook" \
        -H "Content-Type: application/json" \
        -d "$email_payload" > /dev/null; then
        log_info "Email notification sent successfully"
    else
        log_error "Failed to send email notification"
    fi
}

# =============================================================================
# MAIN EXECUTION
# =============================================================================

main() {
    log_info "Sending configuration drift notifications..."
    log_info "Level: $DRIFT_LEVEL, Environment: $TARGET_ENV"
    log_info "Critical: $CRITICAL_COUNT, Warnings: $WARNING_COUNT"
    
    local notifications_sent=0
    
    # Send to all configured platforms
    if [[ -n "$SLACK_WEBHOOK_URL" ]]; then
        send_slack_notification
        ((notifications_sent++))
    fi
    
    if [[ -n "$TEAMS_WEBHOOK_URL" ]]; then
        send_teams_notification
        ((notifications_sent++))
    fi
    
    if [[ -n "$DISCORD_WEBHOOK_URL" ]]; then
        send_discord_notification
        ((notifications_sent++))
    fi
    
    if [[ -n "$GENERIC_WEBHOOK_URL" ]]; then
        send_generic_notification
        ((notifications_sent++))
    fi
    
    if [[ -n "$EMAIL_WEBHOOK_URL" ]]; then
        send_email_notification
        ((notifications_sent++))
    fi
    
    if [[ $notifications_sent -eq 0 ]]; then
        log_info "No webhook URLs configured - no notifications sent"
        echo "To enable notifications, set one or more of:"
        echo "  SLACK_WEBHOOK_URL"
        echo "  TEAMS_WEBHOOK_URL"
        echo "  DISCORD_WEBHOOK_URL"
        echo "  GENERIC_WEBHOOK_URL"
        echo "  EMAIL_WEBHOOK_URL"
    else
        log_info "Sent notifications to $notifications_sent platform(s)"
    fi
}

# =============================================================================
# SCRIPT EXECUTION
# =============================================================================

case "${1:-}" in
    --help|-h)
        cat << EOF
Configuration Drift Monitoring Webhook Integration

USAGE:
  $0 [DRIFT_LEVEL] [TARGET_ENV] [CRITICAL_COUNT] [WARNING_COUNT]

PARAMETERS:
  DRIFT_LEVEL      Severity level (critical, warning, info, success)
  TARGET_ENV       Target environment (Production, Staging, etc.)
  CRITICAL_COUNT   Number of critical issues detected
  WARNING_COUNT    Number of warning issues detected

ENVIRONMENT VARIABLES:
  SLACK_WEBHOOK_URL      Slack incoming webhook URL
  TEAMS_WEBHOOK_URL      Microsoft Teams webhook URL
  DISCORD_WEBHOOK_URL    Discord webhook URL
  GENERIC_WEBHOOK_URL    Generic webhook URL for custom integrations
  EMAIL_WEBHOOK_URL      Email service webhook URL
  EMAIL_RECIPIENTS       Email recipients (comma-separated)

EXAMPLES:
  $0 critical Production 3 1
  $0 warning Staging 0 2
  $0 success Production 0 0

This script sends formatted notifications to various platforms when
configuration drift is detected by the drift detection system.
EOF
        exit 0
        ;;
    *)
        main
        ;;
esac