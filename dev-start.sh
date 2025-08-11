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

# Start SQL Server container (if not running)
echo "🗄️  Starting SQL Server container..."
docker-compose up sql-server -d

# Wait for SQL Server to be ready
echo "⏳ Waiting for SQL Server to be ready..."
while ! docker exec aipm-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Dev123456! -C -Q "SELECT 1" &>/dev/null; do
  echo -n "."
  sleep 2
done
echo " ✅ SQL Server ready!"

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
echo "🗄️  Database: localhost:1433 (sa/Dev123456!)"
echo ""
echo "📊 Process IDs saved in logs/ directory"
echo "📝 Logs available in logs/ directory"
echo ""
echo "💡 Use './dev-stop.sh' to stop all services"
echo "💡 Use './dev-test.sh' to run tests"
echo "💡 Use './dev-monitor.sh' to monitor services"