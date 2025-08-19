#!/bin/bash

# AI Profile Photo Maker - Local Development Stop Script

echo "🛑 Stopping AI Profile Photo Maker Local Development Environment"
echo "================================================================="

# Stop API process
if [ -f "logs/api.pid" ]; then
    API_PID=$(cat logs/api.pid)
    echo "🔧 Stopping API server (PID: $API_PID)..."
    kill $API_PID 2>/dev/null || true
    rm -f logs/api.pid
fi

# Stop Frontend process
if [ -f "logs/frontend.pid" ]; then
    FRONTEND_PID=$(cat logs/frontend.pid)
    echo "🎨 Stopping Frontend server (PID: $FRONTEND_PID)..."
    kill $FRONTEND_PID 2>/dev/null || true
    rm -f logs/frontend.pid
fi

# Stop ngrok process
if [ -f "logs/ngrok.pid" ]; then
    NGROK_PID=$(cat logs/ngrok.pid)
    echo "🔗 Stopping ngrok tunnel (PID: $NGROK_PID)..."
    kill $NGROK_PID 2>/dev/null || true
    rm -f logs/ngrok.pid
fi

# Kill any remaining processes
echo "🔍 Cleaning up remaining processes..."
pkill -f "dotnet.*AI.ProfilePhotoMaker.API" 2>/dev/null || true
pkill -f "ng serve" 2>/dev/null || true
pkill -f "ngrok.*5032" 2>/dev/null || true

echo ""
echo "✅ LOCAL DEVELOPMENT ENVIRONMENT STOPPED"
echo "========================================"