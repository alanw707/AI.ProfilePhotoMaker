#!/bin/bash

# AI Profile Photo Maker - Development Environment Startup Script
# This script starts the complete development environment

set -e

echo "🚀 Starting AI Profile Photo Maker Development Environment"
echo "=============================================="

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to check if a port is in use
port_in_use() {
    netstat -tlnp 2>/dev/null | grep -q ":$1 "
}

# Function to wait for service to be ready
wait_for_service() {
    local url=$1
    local max_attempts=30
    local attempt=1
    
    echo "Waiting for $url to be ready..."
    while [ $attempt -le $max_attempts ]; do
        if curl -s "$url" >/dev/null 2>&1; then
            echo "✅ $url is ready"
            return 0
        fi
        echo "⏳ Attempt $attempt/$max_attempts - waiting for $url..."
        sleep 2
        attempt=$((attempt + 1))
    done
    
    echo "❌ $url failed to start after $max_attempts attempts"
    return 1
}

# Check prerequisites
echo "📋 Checking prerequisites..."

if ! command_exists node; then
    echo "❌ Node.js is not installed"
    exit 1
fi

if ! command_exists npm; then
    echo "❌ npm is not installed"
    exit 1
fi

if ! command_exists dotnet; then
    echo "❌ .NET SDK is not installed"
    exit 1
fi

if ! command_exists ngrok; then
    echo "❌ ngrok is not installed"
    exit 1
fi

echo "✅ All prerequisites are available"

# Check if services are already running
echo "🔍 Checking existing services..."

if port_in_use 4200; then
    echo "⚠️  Port 4200 is already in use (Angular dev server)"
    echo "   Use 'pkill -f \"ng serve\"' to stop existing server"
fi

if port_in_use 5035; then
    echo "⚠️  Port 5035 is already in use (.NET API server)"
    echo "   Use 'pkill -f \"dotnet run\"' to stop existing server"
fi

if port_in_use 4040; then
    echo "⚠️  Port 4040 is already in use (ngrok web interface)"
    echo "   Use 'pkill -f ngrok' to stop existing ngrok processes"
fi

# Navigate to UI directory
cd "$(dirname "$0")/AI.ProfilePhotoMaker.UI"

echo "📁 Working directory: $(pwd)"

# Start ngrok tunnels
echo "🌐 Starting ngrok tunnels..."
npm run tunnel:start >/dev/null 2>&1 &
NGROK_PID=$!

# Wait a moment for ngrok to start
sleep 5

# Check if ngrok started successfully
if ! curl -s http://127.0.0.1:4040/api/tunnels >/dev/null 2>&1; then
    echo "❌ Failed to start ngrok tunnels"
    exit 1
fi

echo "✅ Ngrok tunnels started"

# Start Angular frontend
echo "⚛️  Starting Angular frontend..."
npm run dev:ngrok >/dev/null 2>&1 &
ANGULAR_PID=$!

# Start .NET backend
echo "🔷 Starting .NET backend..."
cd ../AI.ProfilePhotoMaker.API
dotnet run >/dev/null 2>&1 &
DOTNET_PID=$!

# Return to original directory
cd ../

# Wait for services to be ready
echo "⏳ Waiting for services to be ready..."

# Wait for frontend
if ! wait_for_service "https://awlocaldev.ngrok.app"; then
    echo "❌ Frontend failed to start"
    kill $ANGULAR_PID $DOTNET_PID $NGROK_PID 2>/dev/null || true
    exit 1
fi

# Wait for backend
if ! wait_for_service "https://awlocaldev-api.ngrok.app/api/health"; then
    echo "❌ Backend failed to start"
    kill $ANGULAR_PID $DOTNET_PID $NGROK_PID 2>/dev/null || true
    exit 1
fi

echo ""
echo "🎉 Development environment is ready!"
echo "=============================================="
echo "Frontend: https://awlocaldev.ngrok.app"
echo "Backend:  https://awlocaldev-api.ngrok.app"
echo "Ngrok UI: http://localhost:4040"
echo ""
echo "📊 Process IDs:"
echo "   ngrok:   $NGROK_PID"
echo "   Angular: $ANGULAR_PID" 
echo "   .NET:    $DOTNET_PID"
echo ""
echo "🛑 To stop all services:"
echo "   kill $NGROK_PID $ANGULAR_PID $DOTNET_PID"
echo ""
echo "📖 For troubleshooting, see DEV-ENVIRONMENT.md"
echo "Press Ctrl+C to stop all services, or close this terminal"

# Keep script running and handle cleanup on exit
cleanup() {
    echo ""
    echo "🛑 Stopping all services..."
    kill $NGROK_PID $ANGULAR_PID $DOTNET_PID 2>/dev/null || true
    echo "✅ All services stopped"
}

trap cleanup EXIT INT TERM

# Wait for user to stop
wait