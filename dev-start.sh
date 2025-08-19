#!/bin/bash

# AI Profile Photo Maker - Local Development Startup Script
# KISS Principle Implementation with Auto-Delegation

set -e

echo "🚀 Starting AI Profile Photo Maker Local Development Environment"
echo "=============================================================="

# Stop any existing processes
echo "🔍 Checking for existing processes..."
pkill -f "dotnet.*AI.ProfilePhotoMaker.API" 2>/dev/null || true
pkill -f "ng serve" 2>/dev/null || true
sleep 2

# Start SQL Server and Azurite containers (if not running)
echo "🗄️  Starting SQL Server container..."
docker-compose up sql-server -d

echo "☁️  Starting Azurite (Azure Storage Emulator)..."
docker-compose up azurite -d

# Wait for SQL Server to be ready
echo "⏳ Waiting for SQL Server to be ready..."
while ! docker exec aipm-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Dev123456! -C -Q "SELECT 1" &>/dev/null; do
  echo -n "."
  sleep 2
done
echo " ✅ SQL Server ready!"

# Wait for Azurite to be ready
echo "⏳ Waiting for Azurite to be ready..."
for i in {1..15}; do
  if curl -s http://localhost:10000/ >/dev/null 2>&1; then
    echo " ✅ Azurite ready!"
    break
  fi
  echo -n "."
  sleep 2
done

# Start ngrok tunnel for webhook development
echo "🔗 Starting ngrok tunnel (webhook development)..."
nohup ngrok http 5032 --domain=clear-anteater-usually.ngrok-free.app --log=stdout --log-level=info --log-format=logfmt > logs/ngrok.log 2>&1 &
NGROK_PID=$!
echo $NGROK_PID > logs/ngrok.pid

# Wait for ngrok to be ready
echo "⏳ Waiting for ngrok tunnel to be ready..."
for i in {1..20}; do
  if curl -s http://localhost:4040/api/tunnels >/dev/null 2>&1; then
    echo " ✅ ngrok tunnel ready!"
    NGROK_URL=$(curl -s http://localhost:4040/api/tunnels | jq -r '.tunnels[0].public_url' 2>/dev/null || echo "")
    if [ ! -z "$NGROK_URL" ]; then
      echo "🔗 Webhook URL: $NGROK_URL"
    fi
    break
  fi
  echo -n "."
  sleep 2
done

# Start API in background
echo "🔧 Starting API server (localhost:5032)..."
cd AI.ProfilePhotoMaker.API
ASPNETCORE_ENVIRONMENT=Development nohup dotnet run --urls "http://localhost:5032" > ../logs/api.log 2>&1 &
API_PID=$!
echo $API_PID > ../logs/api.pid
cd ..

# Wait for API to be ready
echo "⏳ Waiting for API to be ready..."
for i in {1..30}; do
  if curl -s http://localhost:5032/api/health >/dev/null 2>&1; then
    echo " ✅ API ready!"
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
echo "🔗 ngrok:     https://clear-anteater-usually.ngrok-free.app"
echo "🗄️  Database: localhost:1433 (sa/Dev123456!)"
echo "☁️  Azurite:  http://localhost:10000 (Azure Storage Emulator)"
echo ""
echo "📊 Process IDs saved in logs/ directory"
echo "📝 Logs available in logs/ directory"
echo ""
echo "💡 Use './dev-stop.sh' to stop all services"
echo "💡 Use './dev-test.sh' to run tests"
echo "💡 Use './dev-monitor.sh' to monitor services"