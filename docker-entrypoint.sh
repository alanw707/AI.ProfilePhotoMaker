#!/bin/sh

# Docker entrypoint script for Angular application
# This script handles environment variable substitution and other startup tasks

set -e

# Default environment variables
API_URL=${API_URL:-https://aiprofilephotomakerapi.azurewebsites.net/api}
BASE_URL=${BASE_URL:-https://aiprofilephotomakerapi.azurewebsites.net}
ENVIRONMENT=${ENVIRONMENT:-production}

# Function to substitute environment variables in JavaScript files
substitute_env_vars() {
    echo "Substituting environment variables..."
    
    # Find and replace in main JavaScript files
    find /usr/share/nginx/html -name "*.js" -exec sed -i \
        -e "s|https://aiprofilephotomakerapi.azurewebsites.net/api|${API_URL}|g" \
        -e "s|https://aiprofilephotomakerapi.azurewebsites.net|${BASE_URL}|g" \
        {} +
    
    echo "Environment variables substituted successfully"
}

# Function to update nginx configuration with environment variables
update_nginx_config() {
    echo "Updating nginx configuration with environment variables..."
    
    # Replace API URL in nginx configuration
    sed -i "s|https://aiprofilephotomakerapi.azurewebsites.net|${BASE_URL}|g" /etc/nginx/conf.d/default.conf
    
    echo "Nginx configuration updated successfully"
}

# Function to validate configuration
validate_config() {
    echo "Validating configuration..."
    
    # Test nginx configuration
    nginx -t
    
    # Check if required files exist
    if [ ! -f "/usr/share/nginx/html/index.html" ]; then
        echo "Error: index.html not found"
        exit 1
    fi
    
    echo "Configuration validation completed"
}

# Function to setup logging
setup_logging() {
    echo "Setting up logging..."
    
    # Create log directory if it doesn't exist
    mkdir -p /var/log/nginx
    
    # Link nginx logs to stdout/stderr for container logging
    ln -sf /dev/stdout /var/log/nginx/access.log
    ln -sf /dev/stderr /var/log/nginx/error.log
    
    echo "Logging setup completed"
}

# Main execution
main() {
    echo "Starting AI Profile Photo Maker Frontend..."
    echo "Environment: ${ENVIRONMENT}"
    echo "API URL: ${API_URL}"
    echo "Base URL: ${BASE_URL}"
    
    # Run setup functions
    substitute_env_vars
    update_nginx_config
    validate_config
    setup_logging
    
    echo "Startup completed successfully"
    echo "Starting nginx..."
    
    # Execute the main command
    exec "$@"
}

# Run main function
main "$@"