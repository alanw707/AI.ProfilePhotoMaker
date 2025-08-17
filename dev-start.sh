#!/bin/bash

# AI Profile Photo Maker - Local Development Startup Script
# KISS Principle Implementation with Auto-Delegation

set -e

echo "🚀 Starting AI Profile Photo Maker Local Development Environment"
echo "=============================================================="

# Ensure logs directory exists
mkdir -p logs

# Stop any existing processes
echo "🔍 Checking for existing processes..."
pkill -f "dotnet.*AI.ProfilePhotoMaker.API" 2>/dev/null || true
pkill -f "ng serve" 2>/dev/null || true
sleep 2

# Do NOT source .env or export secrets here. The API reads user-secrets/appsettings.
# Provide only non-sensitive dev defaults where necessary.
export OAUTH_BASE_URL=${OAUTH_BASE_URL:-http://localhost:5032}
export REPLICATE_API_TOKEN=${REPLICATE_API_TOKEN:-r8_dev_dummy_1234567890}
export REPLICATE_WEBHOOK_SECRET=${REPLICATE_WEBHOOK_SECRET:-whsec_dev_dummy_1234567890}

# Start ngrok with reserved domain (headless, log to file)
echo "🔗 Starting ngrok tunnel (headless)..."
pkill -f "ngrok http" 2>/dev/null || true
sleep 1
nohup ngrok http 5032 \
  --domain=clear-anteater-usually.ngrok-free.app \
  --log=stdout \
  --log-level=info \
  --log-format=logfmt \
  > logs/ngrok.log 2>&1 &
NGROK_PID=$!
echo $NGROK_PID > logs/ngrok.pid
echo "⏳ Waiting for ngrok tunnel to be ready..."
sleep 3

# Start SQL Server container (if not running)
echo "🗄️  Starting SQL Server container..."
docker-compose up sql-server -d

# Wait for SQL Server container health (no password needed)
echo "⏳ Waiting for SQL Server to be healthy..."
for i in {1..60}; do
  STATUS=$(docker inspect --format '{{.State.Health.Status}}' aipm-sqlserver 2>/dev/null || echo "unknown")
  if [ "$STATUS" = "healthy" ]; then
    echo " ✅ SQL Server healthy!"
    break
  fi
  echo -n "."
  sleep 2
done

# Start API in background
echo "🔧 Starting API server (localhost:5032)..."
cd AI.ProfilePhotoMaker.API
ASPNETCORE_ENVIRONMENT=Development \
OAUTH_BASE_URL="$OAUTH_BASE_URL" \
REPLICATE_API_TOKEN="$REPLICATE_API_TOKEN" \
REPLICATE_WEBHOOK_SECRET="$REPLICATE_WEBHOOK_SECRET" \
nohup dotnet run --no-build --launch-profile https > ../logs/api.log 2>&1 &
API_PID=$!
echo $API_PID > ../logs/api.pid
cd ..

# Wait for API to be ready
echo "⏳ Waiting for API to be ready..."
for i in {1..60}; do
  if curl -fsS http://localhost:5032/api/health/live >/dev/null 2>&1; then
    echo " ✅ API ready!"
    break
  fi
  if curl -fsS http://localhost:5032/swagger/v1/swagger.json >/dev/null 2>&1; then
    echo " ✅ API ready (Swagger reachable)!"
    break
  fi
  echo -n "."
  sleep 2
done

# Start Frontend in background
echo "🎨 Starting Frontend server (localhost:4200)..."
cd AI.ProfilePhotoMaker.UI
nohup npm start > ../logs/frontend.log 2>&1 &
FRONTEND_PID=$!
echo $FRONTEND_PID > ../logs/frontend.pid
cd ..

# Wait for Frontend to be ready
echo "⏳ Waiting for Frontend to be ready..."
for i in {1..60}; do
  if curl -s http://localhost:4200 >/dev/null 2>&1; then
    echo " ✅ Frontend ready!"
    break
  fi
  echo -n "."
  sleep 3
done

echo ""
echo "🎉 LOCAL DEVELOPMENT ENVIRONMENT READY!"
echo "======================================="
echo "📱 Frontend:  http://localhost:4200"
echo "🔧 API:       http://localhost:5032"
echo "🔧 API Docs:  http://localhost:5032/swagger"
echo "🔗 Ngrok:     https://clear-anteater-usually.ngrok-free.app"
echo "🗄️  Database: localhost:1433 (sa/Dev123456!)"
echo ""
echo "📊 Process IDs saved in logs/ directory"
echo "📝 Logs available in logs/ directory"
echo ""
echo "💡 Use './dev-stop.sh' to stop all services"
echo "💡 Use './dev-test.sh' to run tests"
echo "💡 Use './dev-monitor.sh' to monitor services"