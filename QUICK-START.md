# Quick Start Guide - AI Profile Photo Maker

## 🚀 Start Development Environment

```bash
# 1. Start ngrok tunnels
npm run tunnel:start

# 2. Start frontend (in UI directory)
npm run dev:ngrok

# 3. Start backend (new terminal)
cd ../AI.ProfilePhotoMaker.API && dotnet run
```

## 🔗 Access Points
- **Frontend**: https://awlocaldev.ngrok.app
- **Backend**: https://awlocaldev-api.ngrok.app
- **Ngrok Dashboard**: http://localhost:4040

## ⚡ Quick Diagnostics

```bash
# Check if everything is running
ps aux | grep -E "(ng serve|dotnet run|ngrok)"

# Test endpoints
curl https://awlocaldev.ngrok.app
curl https://awlocaldev-api.ngrok.app/api/health

# Check tunnel status
curl -s http://127.0.0.1:4040/api/tunnels
```

## 🛠️ Common Fixes

### "Page isn't working"
```bash
npm run dev:ngrok
```

### "API endpoint offline"
```bash
cd ../AI.ProfilePhotoMaker.API && dotnet run
```

### "Multiple ngrok sessions"
```bash
pkill -f ngrok && npm run tunnel:start
```

## 📋 Development Checklist
- [ ] Both ngrok tunnels active
- [ ] Angular dev server running (port 4200)
- [ ] .NET API server running (port 5035)
- [ ] Frontend loads at ngrok URL
- [ ] Backend health check passes

---
**For detailed troubleshooting**: See [DEV-ENVIRONMENT.md](./DEV-ENVIRONMENT.md)