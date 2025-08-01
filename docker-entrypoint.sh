#!/bin/sh
set -e

# Function to inject environment variables into Angular app
inject_env_vars() {
    echo "🔧 Injecting runtime environment variables..."
    
    # Define the environment file path
    ENV_FILE="/usr/share/nginx/html/assets/env.js"
    
    # Create the environment configuration
    cat > "$ENV_FILE" << EOF
window.env = {
  apiUrl: '${API_URL:-https://localhost:5001}',
  environment: '${ENVIRONMENT:-staging}'
};
EOF
    
    echo "✅ Environment variables injected:"
    echo "  - API_URL: ${API_URL:-https://localhost:5001}"
    echo "  - ENVIRONMENT: ${ENVIRONMENT:-staging}"
}

# Inject environment variables
inject_env_vars

# Update the main index.html to load the environment configuration
if [ -f "/usr/share/nginx/html/index.html" ]; then
    # Insert script tag before </head> if not already present
    if ! grep -q "assets/env.js" /usr/share/nginx/html/index.html; then
        sed -i 's|</head>|  <script src="assets/env.js"></script>\n</head>|' /usr/share/nginx/html/index.html
        echo "✅ Added environment script to index.html"
    fi
fi

# Create assets directory if it doesn't exist
mkdir -p /usr/share/nginx/html/assets

echo "🚀 Starting nginx..."
exec "$@"