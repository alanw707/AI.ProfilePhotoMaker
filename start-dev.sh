#!/bin/bash

# AI Profile Photo Maker - Development Environment Startup Script
# This script starts the complete development environment with selective service control

set -e

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UI_DIR="$SCRIPT_DIR/AI.ProfilePhotoMaker.UI"
API_DIR="$SCRIPT_DIR/AI.ProfilePhotoMaker.API"

# Service control flags
START_NGROK=true
START_FRONTEND=true
START_BACKEND=true
RESTART_BACKEND=false

# Function to show usage
show_usage() {
    cat << EOF
🚀 AI Profile Photo Maker - Development Environment Control

Usage: $0 [OPTIONS]

OPTIONS:
    -h, --help           Show this help message
    -f, --frontend-only  Start only the frontend (Angular)
    -b, --backend-only   Start only the backend (.NET API)
    -n, --no-ngrok      Skip ngrok tunnel setup
    -r, --restart-backend Restart the backend server
    --restart-backend   Restart only the backend server (keeps frontend running)

EXAMPLES:
    $0                   Start all services (default)
    $0 -f               Start only frontend
    $0 -b               Start only backend
    $0 -r               Restart backend server
    $0 -f -n            Start frontend without ngrok
    $0 --restart-backend Restart backend only

SERVICES:
    Frontend: Angular dev server (port 4200)
    Backend:  .NET API server (port 5035)
    Ngrok:    Tunnel service (port 4040)

EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            show_usage
            exit 0
            ;;
        -f|--frontend-only)
            START_FRONTEND=true
            START_BACKEND=false
            ;;
        -b|--backend-only)
            START_FRONTEND=false
            START_BACKEND=true
            ;;
        -n|--no-ngrok)
            START_NGROK=false
            ;;
        -r|--restart-backend)
            RESTART_BACKEND=true
            START_FRONTEND=false
            START_BACKEND=true
            ;;
        *)
            echo "❌ Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
    shift
done

# Show startup configuration
echo "🚀 Starting AI Profile Photo Maker Development Environment"
echo "=============================================="
echo "Configuration:"
echo "  Frontend: $([ "$START_FRONTEND" = true ] && echo "✅ Starting" || echo "❌ Skipping")"
echo "  Backend:  $([ "$START_BACKEND" = true ] && echo "✅ Starting" || echo "❌ Skipping")"
echo "  Ngrok:    $([ "$START_NGROK" = true ] && echo "✅ Starting" || echo "❌ Skipping")"
echo "  Restart:  $([ "$RESTART_BACKEND" = true ] && echo "✅ Restarting backend" || echo "❌ Normal startup")"
echo ""

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

# Function to stop a service by name
stop_service() {
    local service_name=$1
    local process_pattern=$2
    
    echo "🛑 Stopping $service_name..."
    
    # Find and kill processes
    local pids=$(pgrep -f "$process_pattern" 2>/dev/null || true)
    
    if [ -n "$pids" ]; then
        echo "   Found processes: $pids"
        kill $pids 2>/dev/null || true
        sleep 3
        
        # Force kill if still running
        local remaining_pids=$(pgrep -f "$process_pattern" 2>/dev/null || true)
        if [ -n "$remaining_pids" ]; then
            echo "   Force killing remaining processes: $remaining_pids"
            kill -9 $remaining_pids 2>/dev/null || true
        fi
        
        echo "✅ $service_name stopped"
    else
        echo "   $service_name was not running"
    fi
}

# Function to restart backend server
restart_backend() {
    echo "🔄 Restarting backend server..."
    
    # Stop existing backend
    stop_service "Backend" "dotnet run.*AI.ProfilePhotoMaker.API"
    
    # Start new backend
    echo "🔷 Starting .NET backend..."
    cd "$API_DIR"
    dotnet run >/dev/null 2>&1 &
    DOTNET_PID=$!
    
    # Wait for backend to be ready
    echo "⏳ Waiting for backend to be ready..."
    sleep 5
    
    if ! wait_for_service "http://localhost:5035/api/credit/packages"; then
        echo "❌ Backend failed to restart"
        return 1
    fi
    
    echo "✅ Backend restarted successfully (PID: $DOTNET_PID)"
    return 0
}

# Function to get service status
get_service_status() {
    local service_name=$1
    local process_pattern=$2
    local port=$3
    
    local pid=$(pgrep -f "$process_pattern" 2>/dev/null | head -1)
    local port_status=""
    
    if [ -n "$pid" ]; then
        if [ -n "$port" ] && port_in_use "$port"; then
            port_status=" (port $port active)"
        fi
        echo "   $service_name: ✅ Running (PID: $pid)$port_status"
    else
        echo "   $service_name: ❌ Not running"
    fi
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

# Handle restart backend option
if [ "$RESTART_BACKEND" = true ]; then
    echo "🔄 Restart backend mode activated"
    
    # Check current service status
    echo "🔍 Current service status:"
    get_service_status "Frontend" "ng serve" "4200"
    get_service_status "Backend" "dotnet run.*AI.ProfilePhotoMaker.API" "5035"
    get_service_status "Ngrok" "ngrok" "4040"
    echo ""
    
    # Restart backend
    if restart_backend; then
        echo ""
        echo "🎉 Backend restarted successfully!"
        echo "=============================================="
        echo "Backend:  http://localhost:5035"
        echo "Test endpoint: http://localhost:5035/api/credit/packages"
        echo ""
        echo "Press Ctrl+C to stop, or run this script again to restart"
        
        # Keep script running
        trap "echo 'Goodbye!'; exit 0" INT TERM
        while true; do
            sleep 60
        done
    else
        echo "❌ Failed to restart backend"
        exit 1
    fi
fi

# Check if services are already running
echo "🔍 Checking existing services..."

if [ "$START_FRONTEND" = true ] && port_in_use 4200; then
    echo "⚠️  Port 4200 is already in use (Angular dev server)"
    echo "   Use 'pkill -f \"ng serve\"' to stop existing server"
fi

if [ "$START_BACKEND" = true ] && port_in_use 5035; then
    echo "⚠️  Port 5035 is already in use (.NET API server)"
    echo "   Use 'pkill -f \"dotnet run\"' to stop existing server"
fi

if [ "$START_NGROK" = true ] && port_in_use 4040; then
    echo "⚠️  Port 4040 is already in use (ngrok web interface)"
    echo "   Use 'pkill -f ngrok' to stop existing ngrok processes"
fi

# Navigate to UI directory
cd "$UI_DIR"

echo "📁 Working directory: $(pwd)"

# Process IDs for cleanup
NGROK_PID=""
ANGULAR_PID=""
DOTNET_PID=""

# Start ngrok tunnels
if [ "$START_NGROK" = true ]; then
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
fi

# Start Angular frontend
if [ "$START_FRONTEND" = true ]; then
    echo "⚛️  Starting Angular frontend..."
    if [ "$START_NGROK" = true ]; then
        npm run dev:ngrok >/dev/null 2>&1 &
    else
        npm run dev:local >/dev/null 2>&1 &
    fi
    ANGULAR_PID=$!
fi

# Start .NET backend
if [ "$START_BACKEND" = true ]; then
    echo "🔷 Starting .NET backend..."
    cd "$API_DIR"
    dotnet run >/dev/null 2>&1 &
    DOTNET_PID=$!
fi

# Return to original directory
cd "$SCRIPT_DIR"

# Wait for services to be ready
echo "⏳ Waiting for services to be ready..."

# Wait for frontend
if [ "$START_FRONTEND" = true ]; then
    if [ "$START_NGROK" = true ]; then
        if ! wait_for_service "https://awlocaldev.ngrok.app"; then
            echo "❌ Frontend failed to start"
            kill $ANGULAR_PID $DOTNET_PID $NGROK_PID 2>/dev/null || true
            exit 1
        fi
    else
        if ! wait_for_service "http://localhost:4200"; then
            echo "❌ Frontend failed to start"
            kill $ANGULAR_PID $DOTNET_PID $NGROK_PID 2>/dev/null || true
            exit 1
        fi
    fi
fi

# Wait for backend
if [ "$START_BACKEND" = true ]; then
    if [ "$START_NGROK" = true ]; then
        if ! wait_for_service "https://awlocaldev-api.ngrok.app/api/credit/packages"; then
            echo "❌ Backend failed to start"
            kill $ANGULAR_PID $DOTNET_PID $NGROK_PID 2>/dev/null || true
            exit 1
        fi
    else
        if ! wait_for_service "http://localhost:5035/api/credit/packages"; then
            echo "❌ Backend failed to start"
            kill $ANGULAR_PID $DOTNET_PID $NGROK_PID 2>/dev/null || true
            exit 1
        fi
    fi
fi

echo ""
echo "🎉 Development environment is ready!"
echo "=============================================="

# Show running services
if [ "$START_FRONTEND" = true ]; then
    if [ "$START_NGROK" = true ]; then
        echo "Frontend: https://awlocaldev.ngrok.app"
    else
        echo "Frontend: http://localhost:4200"
    fi
fi

if [ "$START_BACKEND" = true ]; then
    if [ "$START_NGROK" = true ]; then
        echo "Backend:  https://awlocaldev-api.ngrok.app"
    else
        echo "Backend:  http://localhost:5035"
    fi
fi

if [ "$START_NGROK" = true ]; then
    echo "Ngrok UI: http://localhost:4040"
fi

echo ""
echo "📊 Process IDs:"
[ -n "$NGROK_PID" ] && echo "   ngrok:   $NGROK_PID"
[ -n "$ANGULAR_PID" ] && echo "   Angular: $ANGULAR_PID"
[ -n "$DOTNET_PID" ] && echo "   .NET:    $DOTNET_PID"
echo ""

# Show stop command
PIDS_TO_KILL=""
[ -n "$NGROK_PID" ] && PIDS_TO_KILL="$PIDS_TO_KILL $NGROK_PID"
[ -n "$ANGULAR_PID" ] && PIDS_TO_KILL="$PIDS_TO_KILL $ANGULAR_PID"  
[ -n "$DOTNET_PID" ] && PIDS_TO_KILL="$PIDS_TO_KILL $DOTNET_PID"

if [ -n "$PIDS_TO_KILL" ]; then
    echo "🛑 To stop all services:"
    echo "   kill$PIDS_TO_KILL"
fi

echo ""
echo "📖 For troubleshooting, see DEV-ENVIRONMENT.md"
echo "💡 Use './start-dev.sh --help' for more options"
echo "Press Ctrl+C to stop all services, or close this terminal"

# Keep script running and handle cleanup on exit
cleanup() {
    echo ""
    echo "🛑 Stopping all services..."
    
    # Stop services gracefully
    [ -n "$NGROK_PID" ] && kill $NGROK_PID 2>/dev/null || true
    [ -n "$ANGULAR_PID" ] && kill $ANGULAR_PID 2>/dev/null || true
    [ -n "$DOTNET_PID" ] && kill $DOTNET_PID 2>/dev/null || true
    
    # Wait a moment for graceful shutdown
    sleep 2
    
    # Force kill any remaining processes
    [ -n "$NGROK_PID" ] && kill -9 $NGROK_PID 2>/dev/null || true
    [ -n "$ANGULAR_PID" ] && kill -9 $ANGULAR_PID 2>/dev/null || true
    [ -n "$DOTNET_PID" ] && kill -9 $DOTNET_PID 2>/dev/null || true
    
    echo "✅ All services stopped"
}

trap cleanup EXIT INT TERM

# Wait for user to stop
wait