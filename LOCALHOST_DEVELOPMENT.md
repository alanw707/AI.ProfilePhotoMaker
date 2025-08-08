# 🏠 Localhost Development Guide

**Recommended Approach**: Simple, fast, reliable localhost development without external dependencies.

## 🚀 Quick Start (Recommended)

### Option 1: Automatic Full-Stack Setup
```bash
cd AI.ProfilePhotoMaker.UI
npm run dev:fullstack:local
```
**What it does**: Starts both frontend (port 4200) and backend (port 5035) automatically

### Option 2: Manual Setup
```bash
# Terminal 1: Start Backend API
cd AI.ProfilePhotoMaker.API
dotnet run

# Terminal 2: Start Frontend
cd AI.ProfilePhotoMaker.UI
npm start  # (same as npm run dev:local)
```

### ✅ That's it! Access your app:
- **Frontend**: http://localhost:4200
- **API**: http://localhost:5035
- **Swagger**: http://localhost:5035/swagger

## 🎯 Why Localhost Development?

### **Benefits**:
- ✅ **Faster**: No tunneling latency
- ✅ **More Reliable**: No external service dependencies
- ✅ **Simpler Debugging**: Direct connections, better error messages
- ✅ **Standard Approach**: Industry-standard local development
- ✅ **No Account Limits**: No ngrok session restrictions
- ✅ **Works Offline**: No internet required for development

### **Google OAuth Support**:
Google OAuth **fully supports** localhost development:
- ✅ `http://localhost:4200` is a valid OAuth origin
- ✅ No SSL certificate required for localhost
- ✅ Standard OAuth flow works identically

## 🔧 Configuration Details

### Frontend Configuration
- **Environment**: Uses `environment.ts` (localhost default)
- **API Proxy**: `proxy.conf.json` routes `/api` to `localhost:5035`
- **Development Script**: `npm run dev:local` (default for `npm start`)

### Backend Configuration
- **CORS**: Pre-configured for `localhost:4200` and `localhost:4200` (HTTPS)
- **Database**: SQLite for development (no external database required)
- **URLs**: Binds to `http://localhost:5035` and `https://localhost:5035`

## 🌐 When to Use ngrok (Optional)

ngrok is **not required** for normal development, but can be useful for:

### Specific Use Cases:
- **Mobile Device Testing**: Test on actual mobile devices
- **External Webhooks**: Services that need to call your local API
- **Remote Collaboration**: Share development instance with others
- **Cross-Platform Testing**: Test across different devices/networks

### How to Use ngrok (When Needed):
```bash
# Option 1: Full ngrok setup
npm run dev:fullstack:ngrok

# Option 2: Manual ngrok (keep localhost development running)
npm run tunnel:start  # Adds tunnels to existing localhost setup
```

## 🛠️ Development Workflow

### Daily Development (Recommended):
1. `npm run dev:fullstack:local` (or manual setup)
2. Code, test, debug at http://localhost:4200
3. Use browser dev tools normally
4. API available at http://localhost:5035

### When You Need External Access:
1. Keep localhost development running
2. `npm run tunnel:start` (adds ngrok tunnels)
3. Access via ngrok URLs when needed
4. Return to localhost for normal development

## 📱 Google OAuth Setup for Localhost

### Required Configuration in Google Cloud Console:
```
Authorized JavaScript origins:
- http://localhost:4200
- https://localhost:4200

Authorized redirect URIs:
- http://localhost:4200/signin-google
- https://localhost:4200/signin-google
```

### Verify OAuth Setup:
1. Start localhost development
2. Navigate to http://localhost:4200
3. Click "Login" → "Continue with Google"
4. OAuth flow should work seamlessly

## 🧪 Testing & Validation

### Quick Health Check:
```bash
# Check if everything is running
curl -I http://localhost:4200          # Frontend
curl -I http://localhost:5035/api/health # API Health
```

### Full Workflow Test:
1. **Registration**: Create new account
2. **Dashboard**: Verify data loads (not "Loading...")
3. **Upload**: Test image upload functionality
4. **API**: Check browser console for API responses (should be 200 OK)

## 🔍 Troubleshooting

### Common Issues & Solutions:

#### Port Already in Use:
```bash
# Check what's using the port
lsof -i:4200  # or 5035
# Kill the process
kill [PID]
```

#### API Calls Failing:
- ✅ Verify backend is running: http://localhost:5035/swagger
- ✅ Check proxy configuration in `proxy.conf.json`
- ✅ Verify CORS policy in browser console

#### OAuth Not Working:
- ✅ Verify Google Cloud Console has localhost origins
- ✅ Check redirect URIs include localhost
- ✅ Confirm no browser security restrictions

### Log Locations:
- **Frontend**: Browser console (F12)
- **Backend**: Terminal running `dotnet run`
- **API Requests**: Browser Network tab

## 🚀 Advanced Tips

### Hot Reloading:
Both frontend and backend support hot reloading:
- **Frontend**: Automatic reload on file changes
- **Backend**: `dotnet watch run` for auto-restart

### Database Reset:
```bash
cd AI.ProfilePhotoMaker.API
dotnet ef database drop --force
dotnet ef database update
```

### Performance Optimization:
- Use `--source-map=false` for faster builds
- Enable `--aot` for production-like testing
- Use `--optimization` for performance testing

## 📊 Performance Comparison

| Aspect | Localhost | ngrok |
|--------|-----------|-------|
| **Latency** | ~1ms | ~50-200ms |
| **Reliability** | 99.9% | ~95% (network dependent) |
| **Setup Time** | 30 seconds | 2-3 minutes |
| **Dependencies** | None | ngrok account, internet |
| **Debugging** | Direct | Through tunnel |

## 💡 Best Practices

### Development Workflow:
1. **Start with localhost** for all development work
2. **Use ngrok** only for specific external access needs
3. **Test OAuth flow** regularly with localhost
4. **Monitor browser console** for API health

### Code Quality:
- Run linting: `npm run lint`
- Run tests: `npm test`
- Build check: `npm run build`

### Database Management:
- Use migrations for schema changes
- Regular database resets to test clean state
- Keep seed data current

---

## ✅ Summary

**Default Development**: `npm run dev:fullstack:local`  
**Access Application**: http://localhost:4200  
**API Documentation**: http://localhost:5035/swagger  
**External Access (Optional)**: `npm run tunnel:start`

This localhost-first approach provides the **simplest, fastest, and most reliable** development experience while keeping ngrok available for edge cases when external access is specifically needed.