# Azure Storage Credentials Setup Guide

## Current Configuration Status

**Storage Account:** `aipmstv16j74jubocuukg`
**Container:** `style-previews`
**Public Blob URL:** `https://aipmstv16j74jubocuukg.blob.core.windows.net/style-previews/`

## Credential Configuration Options

### Option 1: Connection String (Recommended for Testing)

Create a connection string with the following format:
```
DefaultEndpointsProtocol=https;AccountName=aipmstv16j74jubocuukg;AccountKey={ACCOUNT_KEY};EndpointSuffix=core.windows.net
```

**Configuration Locations:**
- `appsettings.Test.json`: `ConnectionStrings.AzureStorage`
- Environment Variable: `AzureStorage__ConnectionString`
- Command line: `--AzureStorage:ConnectionString`

### Option 2: SAS Token (Secure Alternative)

For testing without full account access:
```
BlobEndpoint=https://aipmstv16j74jubocuukg.blob.core.windows.net/;SharedAccessSignature={SAS_TOKEN}
```

### Option 3: Managed Identity (Production)

For Azure-hosted environments:
```json
{
  "AzureStorage": {
    "ServiceUri": "https://aipmstv16j74jubocuukg.blob.core.windows.net/",
    "UseDefaultAzureCredential": true
  }
}
```

## Required Permissions for Testing

The credentials need the following permissions on the `style-previews` container:

- **Read**: To test image accessibility
- **List**: To enumerate blobs (optional for advanced tests)
- **Write**: To upload test images (for post-upload verification)

## Setup Steps

### Step 1: Obtain Azure Credentials

```bash
# Option A: Using Azure CLI
az storage account keys list --account-name aipmstv16j74jubocuukg --resource-group {RESOURCE_GROUP}

# Option B: Create SAS token
az storage container generate-sas \
  --account-name aipmstv16j74jubocuukg \
  --name style-previews \
  --permissions rl \
  --expiry 2024-12-31 \
  --https-only
```

### Step 2: Configure Test Environment

Create `appsettings.Test.json` with proper credentials:
```json
{
  "ConnectionStrings": {
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=aipmstv16j74jubocuukg;AccountKey={YOUR_KEY};EndpointSuffix=core.windows.net"
  },
  "AzureStorage": {
    "ContainerName": "style-previews"
  }
}
```

### Step 3: Environment Variable Setup

For local testing:
```bash
export AzureStorage__ConnectionString="DefaultEndpointsProtocol=https;AccountName=aipmstv16j74jubocuukg;AccountKey={YOUR_KEY};EndpointSuffix=core.windows.net"
```

For CI/CD:
```yaml
env:
  AzureStorage__ConnectionString: ${{ secrets.AZURE_STORAGE_CONNECTION_STRING }}
```

## Security Considerations

1. **Never commit actual credentials** to the repository
2. **Use environment variables** for CI/CD
3. **Rotate keys regularly** 
4. **Use SAS tokens** when possible for limited access
5. **Enable Azure Storage logging** for audit trails

## Validation Commands

Test the connection:
```bash
# Test API connectivity
dotnet run list-previews

# Test upload functionality  
dotnet run upload-previews --dry-run
```

## Troubleshooting

### Common Issues

1. **"Connection string not configured"**
   - Check environment variables
   - Verify appsettings.json format
   - Ensure no typos in configuration keys

2. **"Access denied"**
   - Verify account key is correct
   - Check container permissions
   - Ensure container exists

3. **"Container not found"**
   - Verify container name is `style-previews`
   - Check storage account name
   - Ensure container has public read access

### Test Connection

```bash
# Quick connection test
az storage blob list --account-name aipmstv16j74jubocuukg --container-name style-previews --auth-mode key
```