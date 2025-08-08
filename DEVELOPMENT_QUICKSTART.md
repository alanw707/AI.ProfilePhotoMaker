# 🚀 Development Quick Start

## **Recommended: Localhost Development** (Simple & Fast)

### One Command Setup:
```bash
cd AI.ProfilePhotoMaker.UI
npm run dev:fullstack:local
```

### Manual Setup:
```bash
# Terminal 1: Backend
cd AI.ProfilePhotoMaker.API && dotnet run

# Terminal 2: Frontend  
cd AI.ProfilePhotoMaker.UI && npm start
```

### ✅ Access Your App:
- **Application**: http://localhost:4200
- **API/Swagger**: http://localhost:5035/swagger

---

## **Optional: External Access** (When Needed)

For mobile testing, webhooks, or sharing:
```bash
npm run tunnel:start  # Adds ngrok to existing localhost setup
```

**External URLs:**
- Frontend: https://awlocaldev.ngrok.app  
- API: https://awlocaldev-api.ngrok.app

---

## **Why Localhost First?**
- ✅ **10x Faster** (no tunnel latency)
- ✅ **More Reliable** (no external dependencies)  
- ✅ **Simpler Debugging** (direct connections)
- ✅ **Google OAuth Works** (localhost is officially supported)
- ✅ **Standard Approach** (industry best practice)

---

## **Google OAuth Setup**
Add to Google Cloud Console:
```
Origins: http://localhost:4200, https://localhost:4200
Redirects: http://localhost:4200/signin-google
```

---

## **Troubleshooting**
- **Port in use?** `lsof -i:4200` then `kill [PID]`
- **API not working?** Check http://localhost:5035/swagger
- **Dashboard loading?** Verify backend is running

📚 **Full Guide**: See [LOCALHOST_DEVELOPMENT.md](./LOCALHOST_DEVELOPMENT.md)