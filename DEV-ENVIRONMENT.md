# AI Profile Photo Maker - Development Environment Guide

Complete guide for setting up, managing, and troubleshooting the development environment.

## Table of Contents
- [Quick Start](#quick-start)
- [Architecture Overview](#architecture-overview)
- [Ngrok Configuration](#ngrok-configuration)
- [Server Management](#server-management)
- [Troubleshooting](#troubleshooting)
- [Common Issues](#common-issues)

## Quick Start

### Prerequisites
- Node.js and npm installed
- .NET 8 SDK installed
- ngrok account with authtoken configured

### Start Development Environment
```bash
# 1. Start both ngrok tunnels
npm run tunnel:start

# 2. Start Angular frontend (ngrok config)
npm run dev:ngrok

# 3. Start .NET backend API (separate terminal)
cd ../AI.ProfilePhotoMaker.API
dotnet run

# 4. Access application
# Frontend: https://awlocaldev.ngrok.app
# Backend: https://awlocaldev-api.ngrok.app
```

## Architecture Overview

### Service Stack
```
┌─────────────────────────────────────────────────────────────┐
│                    Development Environment                   │
├─────────────────────────────────────────────────────────────┤
│ Frontend (Angular)     │ Backend (.NET)     │ Tunneling    │
│ ├─ localhost:4200     │ ├─ localhost:5035  │ ├─ ngrok     │
│ ├─ awlocaldev         │ ├─ awlocaldev-api  │ ├─ tunnels   │
│ └─ .ngrok.app         │ └─ .ngrok.app      │ └─ config    │
└─────────────────────────────────────────────────────────────┘
```

### Port Allocation
- **4200**: Angular Development Server
- **5035**: .NET Core API Server
- **4040**: ngrok Web Interface (local)

### Domain Mapping
- **Frontend**: `https://awlocaldev.ngrok.app` → `localhost:4200`
- **Backend**: `https://awlocaldev-api.ngrok.app` → `localhost:5035`

## Ngrok Configuration

### Configuration File Location
```
/home/alanw/projects/AI.ProfilePhotoMaker/ngrok.yml
```

### Tunnel Definitions
```yaml
tunnels:
  frontend:
    addr: 4200
    proto: http
    domain: awlocaldev.ngrok.app
    inspect: false
    compression: true
    
  backend:
    addr: 5035
    proto: http
    domain: awlocaldev-api.ngrok.app
    inspect: false
    compression: true
```

### Available Commands
```bash
# Start both tunnels (recommended)
npm run tunnel:start

# Individual tunnel commands (not recommended - use config instead)
npm run tunnel:frontend  # Single tunnel for frontend
npm run tunnel:backend   # Single tunnel for backend

# Check tunnel status
curl -s http://127.0.0.1:4040/api/tunnels
```

### Ngrok Limitations
- **Free Tier**: 1 simultaneous agent session
- **Solution**: Use configuration file with `--all` flag
- **Error**: ERR_NGROK_108 indicates multiple agent sessions

## Server Management

### Frontend (Angular)

#### Available Scripts
```bash
# Development environments
npm run dev:local     # Local only (localhost:4200)
npm run dev:ngrok     # Ngrok configuration
npm run dev:test      # Test environment

# Legacy aliases
npm start             # Alias for dev:local
npm run start:ngrok   # Alias for dev:ngrok

# Full-stack development
npm run dev:fullstack:local  # Local frontend + backend
npm run dev:fullstack:ngrok  # Ngrok frontend + backend
```

#### Configuration Files
- **Local**: Uses default Angular configuration
- **Ngrok**: Uses ngrok-specific configuration with `--disable-host-check`
- **Test**: Uses test environment configuration

#### Build Verification
```bash
# Check if Angular compiles
npm run build

# Quick syntax check
ng build --dry-run
```

### Backend (.NET API)

#### Start Backend Server
```bash
cd AI.ProfilePhotoMaker.API
dotnet run
```

#### Health Check
```bash
# Local health check
curl http://localhost:5035/api/health

# Ngrok health check
curl https://awlocaldev-api.ngrok.app/api/health
```

#### Common Backend Issues
- **Port 5035 in use**: Check for existing dotnet processes
- **Database connection**: Verify connection strings
- **Missing packages**: Run `dotnet restore`

## Troubleshooting

### Diagnostic Commands

#### Check Running Processes
```bash
# Check Angular dev server
ps aux | grep "ng serve"

# Check .NET API server
ps aux | grep "dotnet run"

# Check ngrok processes
ps aux | grep ngrok
```

#### Network Diagnostics
```bash
# Test local servers
curl http://localhost:4200/
curl http://localhost:5035/api/health

# Test ngrok tunnels
curl https://awlocaldev.ngrok.app/
curl https://awlocaldev-api.ngrok.app/api/health

# Check tunnel status
curl -s http://127.0.0.1:4040/api/tunnels | grep public_url
```

#### Port Availability
```bash
# Check if ports are available
netstat -tlnp | grep :4200
netstat -tlnp | grep :5035
netstat -tlnp | grep :4040
```

### Recovery Procedures

#### Complete Environment Restart
```bash
# 1. Stop all processes
pkill -f "ng serve"
pkill -f "dotnet run" 
pkill -f ngrok

# 2. Start ngrok tunnels
npm run tunnel:start

# 3. Start frontend
npm run dev:ngrok

# 4. Start backend (separate terminal)
cd ../AI.ProfilePhotoMaker.API && dotnet run
```

#### Frontend Only Restart
```bash
# Stop Angular
pkill -f "ng serve"

# Restart with ngrok config
npm run dev:ngrok
```

#### Backend Only Restart
```bash
# Stop .NET API
pkill -f "dotnet run"

# Restart API
cd ../AI.ProfilePhotoMaker.API && dotnet run
```

## Common Issues

### Issue: "This page isn't working right now"

**Symptoms**: Browser shows generic error page
**Cause**: Angular dev server is down while ngrok tunnel is active
**Solution**:
```bash
# Check if Angular is running
ps aux | grep "ng serve"

# If not running, restart
npm run dev:ngrok
```

### Issue: ERR_NGROK_3200 - Endpoint Offline

**Symptoms**: Specific ngrok domain shows as offline
**Cause**: Target service (frontend/backend) is not running
**Solution**:
```bash
# For frontend (awlocaldev.ngrok.app)
npm run dev:ngrok

# For backend (awlocaldev-api.ngrok.app)
cd ../AI.ProfilePhotoMaker.API && dotnet run
```

### Issue: ERR_NGROK_108 - Multiple Agent Sessions

**Symptoms**: Cannot start additional ngrok tunnels
**Cause**: Trying to run multiple ngrok agents on free tier
**Solution**:
```bash
# Stop all ngrok processes
pkill -f ngrok

# Use configuration file instead
npm run tunnel:start
```

### Issue: Upload Fails - Response Parsing Error

**Symptoms**: Photo enhancement upload fails with parsing errors
**Cause**: Frontend expects different response format than API returns
**Solution**: 
- Verify both frontend and backend are running
- Check console logs for specific parsing errors
- Ensure TypeScript code changes are compiled (restart Angular)

### Issue: Enhancement Polling 404 Errors

**Symptoms**: Upload succeeds but enhancement fails with 404 polling errors
**Cause**: `/enhanced` path not configured in Angular proxy
**Solution**:
```bash
# Ensure proxy.conf.ngrok.json includes /enhanced path
# Restart Angular dev server after proxy changes
npm run dev:ngrok
```

### Issue: Authentication Failures

**Symptoms**: API returns 401/403 errors
**Cause**: Missing or invalid authentication tokens
**Solution**:
```bash
# Check if user is logged in
# Verify localStorage contains 'auth_token'
# Clear browser cache if needed
```

### Issue: CORS Errors

**Symptoms**: Browser blocks API requests
**Cause**: CORS configuration issues between domains
**Solution**: 
- Ensure backend CORS is configured for ngrok domains
- Verify ngrok tunnels are using HTTPS
- Check API configuration for allowed origins

## Development Workflow

### Typical Development Session
1. **Start tunnels**: `npm run tunnel:start`
2. **Start frontend**: `npm run dev:ngrok`
3. **Start backend**: `cd ../AI.ProfilePhotoMaker.API && dotnet run`
4. **Verify health**: Check both ngrok URLs are responsive
5. **Develop**: Make changes and test
6. **Debug**: Use browser dev tools and server logs

### Code Changes Workflow
1. **Frontend changes**: Auto-reload via Angular dev server
2. **Backend changes**: Manual restart required for .NET
3. **Configuration changes**: May require full restart
4. **Ngrok changes**: Restart tunnel service only

### Testing Checklist
- [ ] Frontend loads at `https://awlocaldev.ngrok.app`
- [ ] Backend health check passes at `https://awlocaldev-api.ngrok.app/api/health`
- [ ] Authentication flow works
- [ ] Photo upload and enhancement works
- [ ] Gallery loads correctly
- [ ] All ngrok tunnels show in `curl -s http://127.0.0.1:4040/api/tunnels`

## Best Practices

### Development
- Always use ngrok configuration for development (not local)
- Keep both frontend and backend running during development
- Monitor console logs for errors and warnings
- Use browser dev tools network tab for API debugging

### Troubleshooting
- Check service status before investigating complex issues
- Use health check endpoints to verify service availability
- Review recent commits when issues arise after code changes
- Document new issues and solutions in this guide

### Performance
- ngrok tunnels have optimized configuration for development
- Compression and inspection are optimized for performance
- Use `console_ui: false` and `log_level: warn` for reduced overhead

---

**Last Updated**: July 14, 2025  
**Environment**: WSL2 Ubuntu with ngrok tunneling  
**Framework Versions**: Angular 19.2.12, .NET 8