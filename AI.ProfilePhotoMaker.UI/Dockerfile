# Simple Angular UI Dockerfile with nginx
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./
RUN npm ci --ignore-scripts

# Copy source and build
COPY . .
RUN npm run build:mvp-v1

# Serve with nginx
FROM nginx:alpine AS runtime
COPY --from=build /app/dist/ai.profile-photo-maker.ui/browser /usr/share/nginx/html

# Comprehensive nginx config for SPA with proper MIME types
RUN echo 'server { \
    listen 80; \
    server_name localhost; \
    root /usr/share/nginx/html; \
    index index.html; \
    \
    # Proper MIME types for Angular assets \
    location ~* \\.js$ { \
        add_header Content-Type application/javascript; \
        expires 1y; \
        add_header Cache-Control "public, immutable"; \
    } \
    \
    location ~* \\.(css|ico|png|jpg|jpeg|gif|svg|woff|woff2|ttf|eot)$ { \
        expires 1y; \
        add_header Cache-Control "public, immutable"; \
    } \
    \
    # Angular SPA fallback \
    location / { \
        try_files $uri $uri/ /index.html; \
        add_header X-Frame-Options "SAMEORIGIN" always; \
        add_header X-Content-Type-Options "nosniff" always; \
    } \
    \
    # Health check endpoint \
    location /health { \
        return 200 "healthy"; \
        add_header Content-Type text/plain; \
    } \
}' > /etc/nginx/conf.d/default.conf

# Ensure nginx includes proper MIME types
RUN echo 'include /etc/nginx/mime.types;' > /etc/nginx/conf.d/mime.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]