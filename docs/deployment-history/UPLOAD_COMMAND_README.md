# Style Previews Upload Command

A C# console command for uploading local style preview images to Azure Blob Storage.

## Overview

The upload command provides a secure, production-ready way to upload the 23 style preview images from the local `style-previews/` directory to Azure Blob Storage. The command includes comprehensive validation, error handling, and progress reporting.

## Commands

### Upload Style Previews

```bash
dotnet run -- upload-previews [FLAGS]
```

**Flags:**
- `--dry-run` - Simulate upload without actually uploading files
- `--force` - Overwrite existing files in Azure (default: skip existing files)
- `--help` - Show help information

**Examples:**
```bash
# Upload all style preview images
dotnet run -- upload-previews

# Simulate upload (demo mode if Azure not configured)
dotnet run -- upload-previews --dry-run

# Force overwrite existing files
dotnet run -- upload-previews --force

# Combine flags
dotnet run -- upload-previews --dry-run --force
```

### List Style Previews

```bash
dotnet run -- list-previews
```

Shows the status of all local style preview files and whether they exist in Azure Blob Storage.

## Configuration

The command requires Azure Blob Storage configuration in `appsettings.json`:

```json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "profile-images"
  }
}
```

Or via connection strings:

```json
{
  "ConnectionStrings": {
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=..."
  }
}
```

## Features

### 🛡️ Production Safety
- **Validation**: Checks local files exist before upload
- **Conflict Detection**: Skips existing files unless `--force` is used
- **Error Handling**: Comprehensive error reporting with specific error messages
- **Dry Run Mode**: Test operations without actual uploads

### 📊 Progress Reporting
- Real-time upload progress with file names and sizes
- Success/failure indicators for each file
- Summary statistics (uploaded, skipped, failed counts)
- File size reporting in bytes

### 🔧 Smart Configuration
- Auto-detects Azure Storage configuration
- Falls back to demo mode for testing without Azure
- Validates connection string format
- Clear error messages for configuration issues

### 🎯 Deployment Ready
- Uses existing `IStorageService` abstraction
- Follows established patterns from migration commands
- Proper dependency injection integration
- Structured logging with appropriate log levels

## Implementation Details

### File Processing
- Scans `style-previews/` directory for `.jpg` files
- Uploads to `style-previews` container in Azure Blob Storage
- Sets proper content types (`image/jpeg`)
- Public blob access for web serving

### Error Recovery
- Graceful handling of network issues
- Per-file error reporting (doesn't stop on single failure)
- Validation of successful uploads
- Detailed error messages for troubleshooting

### Architecture
- **`UploadStylePreviewsService`**: Core upload logic
- **`UploadCommandService`**: Command-line argument parsing and validation
- **`Program.cs`**: Integration with existing command system
- Uses existing `AzureBlobStorageService` for Azure operations

## Demo Mode

When Azure Storage is not configured, the command can run in demo mode with `--dry-run`:

```
=== DEMO MODE: Style Previews Upload ===

⚠️  Running in demo mode without Azure Storage configuration

Found 22 image files to process:
  • medical.jpg (100,502 bytes)
  • corporate.jpg (87,168 bytes)
  • linkedin.jpg (93,432 bytes)
  [... more files ...]

Demo Upload Simulation:
STATUS   SIZE        FILE
------   ---------   ----
🔍 DEMO    100,502   medical.jpg
🔍 DEMO     87,168   corporate.jpg
[... more files ...]

=== Demo Summary ===
Total Files: 22
Total Size: 2,336,337 bytes
```

## Exit Codes

- **0**: Success (all files uploaded or valid dry-run)
- **1**: Error (configuration issues, upload failures, or missing files)

## Files Created

1. **`Services/UploadStylePreviewsService.cs`** - Core upload service with progress reporting
2. **`Services/UploadCommandService.cs`** - Command-line handling and validation
3. **`Program.cs`** - Updated with upload command integration

## Usage in Production

1. Configure Azure Storage connection string
2. Run the command: `dotnet run -- upload-previews`
3. Verify uploads with: `dotnet run -- list-previews`
4. Files will be accessible at: `https://{storage-account}.blob.core.windows.net/style-previews/{filename}`

The implementation is production-ready with comprehensive validation, error handling, and logging suitable for deployment scenarios.