# AI Profile Photo Maker - Clean Deployment Plan

## 🎯 Current Working State ✅

**Resource Group**: `aiprofilemaker-staging`
**Location**: East US / East US 2

### ✅ Deployed Infrastructure
```bash
# Container Registry
acrapmsimple78215.azurecr.io

# SQL Database  
sql-apm-1754278427.database.windows.net/aiprofilemaker

# API Container App (RUNNING)
api-apm-simple.nicestone-1ec028d4.eastus.azurecontainerapps.io

# Container Environment
env-apm-simple

# Log Analytics
workspace-aiprofilemakerstagingDeT0
```

### ✅ Working Images
- **API**: `acrapmsimple78215.azurecr.io/api:v3` (migration disabled, static file fixes applied)
- **UI**: Building required

---

## 🚀 Next Steps - Simple Sequential Execution

### 1️⃣ Storage Account (REQUIRED)
```bash
# Create storage for blob operations
STORAGE_NAME="apmstorage$(date +%s | tail -c 6)"
az storage account create \
  --name $STORAGE_NAME \
  --resource-group aiprofilemaker-staging \
  --location eastus \
  --sku Standard_LRS

# Get connection string for container apps
az storage account show-connection-string --name $STORAGE_NAME --resource-group aiprofilemaker-staging
```

### 2️⃣ Database Migration  
```bash
# Run EF migrations to create schema
cd AI.ProfilePhotoMaker.API
dotnet ef database update --connection "Server=tcp:sql-apm-1754278427.database.windows.net,1433;Initial Catalog=aiprofilemaker;User ID=sqladmin;Password=[REPLACE_WITH_ACTUAL_PASSWORD];Encrypt=True;"
```

### 3️⃣ UI Container Build & Deploy
```bash
# Build UI image
az acr build --registry acrapmsimple78215 --image ui:latest AI.ProfilePhotoMaker.UI

# Deploy UI container app
az containerapp create \
  --name ui-apm-simple \
  --resource-group aiprofilemaker-staging \
  --environment env-apm-simple \
  --image acrapmsimple78215.azurecr.io/ui:latest \
  --registry-server acrapmsimple78215.azurecr.io \
  --registry-username acrapmsimple78215 \
  --registry-password [FROM_ACR] \
  --target-port 80 \
  --ingress external \
  --env-vars "API_BASE_URL=https://api-apm-simple.nicestone-1ec028d4.eastus.azurecontainerapps.io"
```

### 4️⃣ Upload Style Images
```bash
# Upload style preview images to blob storage
cd AI.ProfilePhotoMaker.API/style-previews
# Use existing upload scripts with new storage account
```

### 5️⃣ End-to-End Test
```bash
# Verify complete functionality
curl https://api-apm-simple.nicestone-1ec028d4.eastus.azurecontainerapps.io/health
curl https://ui-apm-simple.[URL]/
# Test image generation and style previews
```

---

## 🔧 Configuration Values

### Environment Variables
```bash
# API Container
ConnectionStrings__DefaultConnection="Server=tcp:sql-apm-1754278427.database.windows.net,1433;Initial Catalog=aiprofilemaker;User ID=sqladmin;Password=[REPLACE_WITH_ACTUAL_PASSWORD];Encrypt=True;"

# UI Container  
API_BASE_URL="https://api-apm-simple.nicestone-1ec028d4.eastus.azurecontainerapps.io"

# Storage Account (add after creation)
AZURE_STORAGE_CONNECTION_STRING="[FROM_STORAGE_ACCOUNT]"
```

### Container Registry Credentials
```bash
# Registry: acrapmsimple78215.azurecr.io
# Username: acrapmsimple78215  
# Password: [GET_FROM_AZURE]
```

---

## ✅ Success Criteria

**Step 1**: Storage account responds to blob operations
**Step 2**: Database schema created, no migration errors  
**Step 3**: UI loads, can reach API endpoints
**Step 4**: Style preview images display correctly
**Step 5**: Complete user workflow (upload → generate → download)

---

## 🚨 Rollback Plan

If any step fails:
1. **API is stable** - can continue with current version
2. **Database** - migrations are additive, safe to retry
3. **UI/Storage** - can redeploy without impact to API
4. **Images** - can re-upload without system impact

**Current system remains functional during all operations.**

---

*Generated: $(date)*
*API Status: RUNNING ✅*
*Next Action: Create storage account*