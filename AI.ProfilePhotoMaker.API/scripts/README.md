# Style Preview Image Upload Scripts

This directory contains multiple scripts to upload style preview images to Azure Blob Storage for the AI ProfilePhotoMaker application.

## Quick Start

The fastest way to upload style preview images is to use one of the provided scripts. Choose the method that works best for your environment:

### Method 1: PowerShell Script (Recommended for Windows/WSL)

```bash
# Set your Azure Storage connection string
export AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net"

# Run the upload script
pwsh ./upload-style-previews.ps1

# Or with options
pwsh ./upload-style-previews.ps1 -Force -Verbose
```

### Method 2: Bash Script (Linux/macOS)

```bash
# Set your Azure Storage connection string
export AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net"

# Make script executable and run
chmod +x ./upload-style-previews.sh
./upload-style-previews.sh

# Or with environment variables
FORCE=true DRY_RUN=false ./upload-style-previews.sh
```

### Method 3: .NET Console Application

```bash
# Build and run the uploader
cd StylePreviewUploader
dotnet build
dotnet run

# Or with options
dotnet run -- --force --verbose --dry-run
```

## Prerequisites

### For PowerShell Script
- PowerShell Core (pwsh) installed
- Azure PowerShell module (Az.Storage) - will be auto-installed
- Azure Storage connection string

### For Bash Script
- Azure CLI installed and configured (`az login`)
- Either `AZURE_STORAGE_CONNECTION_STRING` or `AZURE_STORAGE_ACCOUNT` environment variable
- curl (for API testing)

### For .NET Application
- .NET 8.0 SDK
- Azure Storage connection string

## Configuration

### Azure Storage Connection String

You need an Azure Storage connection string. You can get this from:

1. **Azure Portal**: Storage Account → Access Keys → Connection String
2. **Azure CLI**: `az storage account show-connection-string --name youraccount --resource-group yourgroup`

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `AZURE_STORAGE_CONNECTION_STRING` | Azure Storage connection string | Required |
| `AZURE_CONTAINER_NAME` | Container name | `profile-images-staging` |
| `PREVIEWS_PATH` | Path to style preview images | `../style-previews` |
| `DRY_RUN` | Show what would be uploaded without uploading | `false` |
| `FORCE` | Overwrite existing files | `false` |

## Script Options

### PowerShell Script Options

```powershell
./upload-style-previews.ps1 [options]

Options:
  -ConnectionString <string>  Azure Storage connection string
  -ContainerName <string>     Container name (default: profile-images-staging)
  -PreviewsPath <string>      Path to previews directory (default: ../style-previews)
  -Force                      Overwrite existing files
  -DryRun                     Show what would be uploaded
  -Verbose                    Enable verbose output
```

### Bash Script Environment Variables

```bash
export AZURE_STORAGE_CONNECTION_STRING="your-connection-string"
export AZURE_CONTAINER_NAME="profile-images-staging"  # optional
export PREVIEWS_PATH="../style-previews"              # optional
export DRY_RUN="false"                                # optional
export FORCE="false"                                  # optional
```

### .NET Application Options

```bash
dotnet run -- [options]

Options:
  -c, --connection-string <string>  Azure Storage connection string
  --container <string>              Container name (default: profile-images-staging)
  -p, --path <string>               Path to previews directory (default: ../../style-previews)
  -f, --force                       Overwrite existing files
  -d, --dry-run                     Show what would be uploaded
  -v, --verbose                     Enable verbose output
  -h, --help                        Show help message
```

## Usage Examples

### Upload All Images (First Time)

```bash
# PowerShell
export AZURE_STORAGE_CONNECTION_STRING="your-connection-string"
pwsh ./upload-style-previews.ps1

# Bash
export AZURE_STORAGE_CONNECTION_STRING="your-connection-string"
./upload-style-previews.sh

# .NET
dotnet run --project StylePreviewUploader
```

### Dry Run (Test Without Uploading)

```bash
# PowerShell
pwsh ./upload-style-previews.ps1 -DryRun

# Bash
DRY_RUN=true ./upload-style-previews.sh

# .NET
dotnet run --project StylePreviewUploader -- --dry-run
```

### Force Overwrite Existing Images

```bash
# PowerShell
pwsh ./upload-style-previews.ps1 -Force

# Bash
FORCE=true ./upload-style-previews.sh

# .NET
dotnet run --project StylePreviewUploader -- --force
```

### Verbose Output

```bash
# PowerShell
pwsh ./upload-style-previews.ps1 -Verbose

# Bash (verbose is default)
./upload-style-previews.sh

# .NET
dotnet run --project StylePreviewUploader -- --verbose
```

### Custom Container and Path

```bash
# PowerShell
pwsh ./upload-style-previews.ps1 -ContainerName "my-container" -PreviewsPath "/path/to/images"

# Bash
export AZURE_CONTAINER_NAME="my-container"
export PREVIEWS_PATH="/path/to/images"
./upload-style-previews.sh

# .NET
dotnet run --project StylePreviewUploader -- --container "my-container" --path "/path/to/images"
```

## Expected Style Preview Images

The scripts will upload images from the `style-previews` directory. Expected files:

- `academic.jpg`
- `artistic.jpg`
- `author.jpg`
- `casual.jpg`
- `consultant.jpg`
- `corporate.jpg`
- `creative.jpg`
- `digital-nomad.jpg`
- `edgy-urban.jpg`
- `entrepreneur.jpg`
- `executive.jpg`
- `fashion.jpg`
- `fitness.jpg`
- `glamour.jpg`
- `influencer.jpg`
- `legal.jpg`
- `linkedin.jpg`
- `medical.jpg`
- `spiritual.jpg`
- `startup.jpg`
- `tech-professional.jpg`

## Verification

After upload, the scripts will:

1. **Generate Sample URLs**: Show public Azure Blob Storage URLs for verification
2. **Test API Endpoint**: Test the `/api/style-preview/list` endpoint to ensure images are accessible
3. **Display Statistics**: Show upload summary with counts and file sizes

### Manual Verification

You can manually verify the upload by:

1. **Azure Portal**: Storage Account → Containers → profile-images-staging → style-previews/
2. **API Endpoint**: `GET https://your-api-url/api/style-preview/list`
3. **Frontend**: Check that style preview images appear in the application

## Troubleshooting

### Common Issues

1. **Connection String Invalid**
   ```
   ❌ Failed to connect to Azure Storage: The remote server returned an error: (403) Forbidden
   ```
   - Verify your connection string is correct
   - Check Azure Storage account permissions

2. **Container Access Denied**
   ```
   ❌ Failed to create/verify container: (403) This request is not authorized
   ```
   - Ensure your storage account has Blob Contributor permissions
   - Verify the connection string includes the correct access key

3. **Files Not Found**
   ```
   ❌ Style previews directory not found: ../style-previews
   ```
   - Check that the `style-previews` directory exists
   - Verify the path is correct relative to the script location

4. **Azure CLI Not Logged In** (Bash script only)
   ```
   ❌ Not logged in to Azure. Please run 'az login' first.
   ```
   - Run `az login` and authenticate

5. **PowerShell Module Missing** (PowerShell script only)
   ```
   Installing Az.Storage module...
   ```
   - The script will auto-install the module
   - You may need to restart PowerShell after first installation

### Debug Mode

For detailed debugging:

```bash
# PowerShell
pwsh ./upload-style-previews.ps1 -Verbose

# .NET
dotnet run --project StylePreviewUploader -- --verbose

# Check Azure CLI version (Bash)
az --version
```

### Manual Upload Test

Test a single file upload manually:

```bash
# Azure CLI
az storage blob upload \
  --container-name profile-images-staging \
  --name "style-previews/test.jpg" \
  --file "../style-previews/corporate.jpg" \
  --content-type "image/jpeg"
```

## Integration with CI/CD

These scripts can be integrated into CI/CD pipelines:

### GitHub Actions Example

```yaml
- name: Upload Style Previews
  run: |
    export AZURE_STORAGE_CONNECTION_STRING="${{ secrets.AZURE_STORAGE_CONNECTION_STRING }}"
    ./scripts/upload-style-previews.sh
  working-directory: AI.ProfilePhotoMaker.API
```

### Azure DevOps Pipeline Example

```yaml
- script: |
    export AZURE_STORAGE_CONNECTION_STRING="$(AZURE_STORAGE_CONNECTION_STRING)"
    ./scripts/upload-style-previews.sh
  displayName: 'Upload Style Previews'
  workingDirectory: 'AI.ProfilePhotoMaker.API'
```

## Security Notes

1. **Never commit connection strings** to version control
2. **Use environment variables** or secure secret management
3. **Limit access** to the minimum required permissions
4. **Rotate access keys** regularly
5. **Use managed identities** in Azure environments when possible

## Support

If you encounter issues:

1. Check the troubleshooting section above
2. Verify your Azure Storage configuration
3. Test with `--dry-run` first
4. Check Azure Storage account permissions
5. Verify the style preview images exist and are valid