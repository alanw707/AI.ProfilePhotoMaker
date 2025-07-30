# AI Profile Photo Maker - Development Guide

## 🚀 **Quick Start Commands**

### **Local Development (Recommended for daily development)**
```bash
# Frontend only
npm run dev:local

# Full-stack local development (Angular + API)
npm run dev:fullstack:local
```

### **ngrok Development (For external access, OAuth testing, team sharing)**
```bash
# Install dependencies first
npm install

# Start full-stack with ngrok tunneling
npm run dev:fullstack:ngrok

# Or step by step:
# 1. Start both tunnels
npm run tunnel:start

# 2. Start frontend with ngrok config
npm run dev:ngrok

# 3. Start backend (in separate terminal)
cd ../AI.ProfilePhotoMaker.API && dotnet run
```

---

## 🌍 **Environment Configurations**

### **1. Local Development** 
- **Frontend**: `http://localhost:4200`
- **Backend**: `http://localhost:5035`
- **Proxy**: All API calls proxied to localhost backend
- **Use for**: Daily development, fastest iteration

### **2. ngrok Development**
- **Frontend**: `https://awlocaldev.ngrok.app` 
- **Backend**: `https://awlocaldev-api.ngrok.app`
- **Proxy**: All API calls proxied to ngrok backend
- **Use for**: OAuth testing, external access, team sharing

### **3. Test Environment**
- **Frontend**: Test domain
- **Backend**: Test API domain  
- **Use for**: Staging, pre-production testing

### **4. Production**
- **Frontend**: Production domain
- **Backend**: Production API
- **Use for**: Live application

---

## 📋 **Available Scripts**

### **Environment-Specific Development**
- `dev:local` - Local development (localhost:4200)
- `dev:ngrok` - ngrok development with tunneling
- `dev:test` - Test environment configuration

### **ngrok Tunneling**
- `tunnel:start` - Start both frontend and backend tunnels
- `tunnel:frontend` - Start only frontend tunnel
- `tunnel:backend` - Start only backend tunnel

### **Full-Stack Development**
- `dev:fullstack:local` - Local Angular + API simultaneously  
- `dev:fullstack:ngrok` - ngrok Angular + API + tunnels simultaneously

### **Build Commands**
- `build:dev` - Development build
- `build:ngrok` - ngrok optimized build
- `build:test` - Test environment build
- `build:prod` - Production build

### **Legacy Commands (for compatibility)**
- `start` - Same as `dev:local`
- `start:ngrok` - Same as `dev:ngrok`  
- `ngrok` - Same as `tunnel:frontend`

---

## 🔧 **Setup Instructions**

### **1. First Time Setup**
```bash
# Install dependencies
npm install

# Install concurrently for multi-command execution
# (should be auto-installed from package.json)
```

### **2. ngrok Setup** 
```bash
# Ensure ngrok is installed globally
npm install -g ngrok

# Auth token is already configured in /ngrok.yml
# Verify domains are accessible:
# - awlocaldev.ngrok.app (frontend)
# - awlocaldev-api.ngrok.app (backend)
```

### **3. Backend Setup**
```bash
cd ../AI.ProfilePhotoMaker.API
dotnet restore
dotnet run
```

---

## 🔍 **Troubleshooting**

### **Common Issues**

1. **404 Errors on Images**
   - **Cause**: Environment mismatch (UI using ngrok, API on localhost)
   - **Solution**: Use matching environment scripts

2. **CORS Errors**  
   - **Cause**: Incorrect proxy configuration
   - **Solution**: Verify proxy config matches environment

3. **ngrok Tunnel Errors**
   - **Cause**: Domain conflicts or auth issues
   - **Solution**: Check ngrok.yml configuration and auth token

4. **Concurrently Not Found**
   - **Cause**: Missing dependency  
   - **Solution**: `npm install` or `npm install concurrently --save-dev`

5. **Grid Layout Issues (RESOLVED)**
   - **Issue**: Green cards going off grid lines in "Review Selected Images"
   - **Root Cause**: Aggressive grid-first approach caused container overflow
   - **Solution**: Adopted conservative flexbox-first strategy (Fixed in commit 6d5be33)
   - **Technical Fix**: Modified `.selected-files-grid-enhanced` to use flexbox at 768px breakpoint

### **Debug Commands**
```bash
# Check running processes
ps aux | grep ngrok
ps aux | grep ng
ps aux | grep dotnet

# Check ngrok status
ngrok status

# View logs
tail -f ../AI.ProfilePhotoMaker.API/api.log
```

---

## 🏗️ **Architecture Overview**

### **Proxy Configuration**
- **Local**: `proxy.conf.json` → localhost:5035
- **ngrok**: `proxy.conf.ngrok.json` → awlocaldev-api.ngrok.app
- **Test**: `proxy.conf.test.json` → test-api.yourcompany.com
- **Prod**: `proxy.conf.prod.json` → api.yourcompany.com

### **Environment Files**
- `environment.ts` - Local development
- `environment.ngrok.ts` - ngrok development  
- `environment.test.ts` - Test environment
- `environment.prod.ts` - Production

### **Angular Configurations**
Each environment has its own:
- Build configuration
- Serve configuration  
- Proxy configuration
- Environment file replacement

---

## 🎯 **Development Workflow**

### **For Daily Development**
1. `npm run dev:local` 
2. Develop and test locally
3. Use browser dev tools

### **For OAuth/External Testing**
1. `npm run dev:fullstack:ngrok`
2. Share ngrok URL with team
3. Test OAuth flows with external URLs

### **For Team Collaboration**
1. Start ngrok: `npm run tunnel:start`
2. Share URLs with team
3. Multiple developers can access same environment

### **For Production Deployment**
1. `npm run build:prod`
2. Deploy dist/ folder to production
3. Ensure environment variables are set

---

## 📚 **Further Reading**

- [ngrok Documentation](https://ngrok.com/docs)
- [Angular CLI Configuration](https://angular.io/cli)
- [Angular Environment Configuration](https://angular.io/guide/build#configure-target-specific-file-replacements)