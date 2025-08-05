# =============================================================================
# Multi-Stage Production Dockerfile
# Builds: API Container, Migration Container, Frontend Container
# =============================================================================

# =============================================================================
# STAGE 1: .NET Backend Build
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj", "AI.ProfilePhotoMaker.API/"]
COPY ["AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj", "AI.ProfilePhotoMaker.API.Tests/"]

# Restore dependencies
RUN dotnet restore "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj"
RUN dotnet restore "AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/AI.ProfilePhotoMaker.API"
RUN dotnet build "AI.ProfilePhotoMaker.API.csproj" -c Release -o /app/build

# Run tests with proper error handling
RUN dotnet test "/src/AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj" \
    --no-build --configuration Release --logger trx --results-directory /testresults \
    || (echo "Tests failed - check /testresults for details" && exit 1)

# Publish the application
RUN dotnet publish "AI.ProfilePhotoMaker.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =============================================================================
# STAGE 2: Frontend Build
# =============================================================================
FROM node:18-alpine AS frontend-build
WORKDIR /app

# Copy package files
COPY AI.ProfilePhotoMaker.UI/package*.json ./

# Install dependencies with clean install
RUN npm ci --only=production && npm cache clean --force

# Copy source code
COPY AI.ProfilePhotoMaker.UI/ ./

# Build the Angular application for production
RUN npm run build --if-present || npm run build:prod

# =============================================================================
# STAGE 3: API Runtime Container
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api-runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=backend-build /app/publish .

# Create required directories with proper permissions
RUN mkdir -p uploads training-zips style-previews enhanced generated logs && \
    chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 80
EXPOSE 443

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "AI.ProfilePhotoMaker.API.dll"]

# =============================================================================
# STAGE 4: Migration Container
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS migration-runner
WORKDIR /app

# Install curl for connectivity checks
RUN apt-get update && apt-get install -y curl netcat-openbsd && rm -rf /var/lib/apt/lists/*

# Copy published application (same as API but different entrypoint)
COPY --from=backend-build /app/publish .

# Copy migration execution script
COPY <<EOF /app/run-migrations.sh
#!/bin/bash
set -e

echo "Starting database migration process..."
echo "Connection String Check: \${CONNECTION_STRING:0:50}..."

# Wait for database to be ready
echo "Waiting for database connectivity..."
timeout=300
counter=0

while [ \$counter -lt \$timeout ]; do
    if dotnet AI.ProfilePhotoMaker.API.dll --check-db-connection 2>/dev/null; then
        echo "Database is ready!"
        break
    fi
    echo "Database not ready, waiting... (\$counter/\$timeout)"
    sleep 5
    counter=\$((counter + 5))
done

if [ \$counter -ge \$timeout ]; then
    echo "ERROR: Database connection timeout after \$timeout seconds"
    exit 1
fi

# Apply migrations
echo "Applying database migrations..."
dotnet ef database update --verbose

# Verify migration success
echo "Verifying migration success..."
if dotnet AI.ProfilePhotoMaker.API.dll --verify-migrations; then
    echo "Migration completed successfully!"
    exit 0
else
    echo "ERROR: Migration verification failed!"
    exit 1
fi
EOF

RUN chmod +x /app/run-migrations.sh

# Install Entity Framework tool
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

ENTRYPOINT ["/app/run-migrations.sh"]

# =============================================================================
# STAGE 5: Frontend Container
# =============================================================================
FROM nginx:alpine AS frontend-runtime

# Install curl for health checks
RUN apk add --no-cache curl

# Copy custom nginx configuration
COPY <<EOF /etc/nginx/conf.d/default.conf
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html index.htm;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript application/javascript application/xml+rss application/json;

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "no-referrer-when-downgrade" always;
    add_header Content-Security-Policy "default-src 'self' http: https: data: blob: 'unsafe-inline'" always;

    # Handle Angular routing
    location / {
        try_files \$uri \$uri/ /index.html;
    }

    # API proxy (if needed)
    location /api/ {
        proxy_pass http://backend:80/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }

    # Static assets caching
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
EOF

# Copy built application (Angular CLI v19+ uses browser/ subdirectory)
COPY --from=frontend-build /app/dist/ai.profile-photo-maker.ui/browser /usr/share/nginx/html/

# Copy environment configuration script
COPY <<EOF /docker-entrypoint.sh
#!/bin/sh
set -e

# Replace environment variables in config files
if [ -f /usr/share/nginx/html/assets/config.json ]; then
    envsubst < /usr/share/nginx/html/assets/config.json.template > /usr/share/nginx/html/assets/config.json
fi

# Start nginx
exec nginx -g "daemon off;"
EOF

RUN chmod +x /docker-entrypoint.sh

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/ || exit 1

ENTRYPOINT ["/docker-entrypoint.sh"]