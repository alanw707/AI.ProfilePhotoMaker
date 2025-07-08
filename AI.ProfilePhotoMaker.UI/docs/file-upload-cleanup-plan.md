# File Upload Section Component Cleanup Plan

## Current State Analysis

**FileUploadSectionComponent** (578 lines) - Complete implementation:
- ✅ File selection and validation
- ✅ Drag and drop handling  
- ✅ Quality checking with face detection
- ✅ File preview management
- ✅ Upload process with progress tracking
- ✅ Uploaded image deletion
- ✅ Event emission for parent component integration

**DashboardComponent** (1,295 lines) - Contains duplicate logic:
- ❌ Duplicate file handling methods (lines 962-1084)
- ❌ Duplicate quality validation methods (lines 1117-1282)
- ❌ Duplicate file preview methods (lines 1138-1151)
- ❌ Redundant file management state

## Duplicate Methods to Remove from DashboardComponent

### 1. File Selection & Drag/Drop (lines 962-990)
- `onFileSelected(event: any)`
- `onDragOver(event: DragEvent)`
- `onDragLeave(event: DragEvent)`
- `onDrop(event: DragEvent)`

### 2. File Handling Core Logic (lines 992-1084)
- `handleFileSelection(files: File[])`
- All file validation and quality checking logic

### 3. Quality Validation Methods (lines 1117-1282)
- `checkImageQuality()`
- `checkAndCorrectImageQuality()`
- `validateImageQuality(files: File[])`
- `getImageDimensions(file: File)`

### 4. File Preview Methods (lines 1138-1151)
- `getFilePreview(file: File)`
- File preview cache management

### 5. Helper Methods (lines 1108-1116)
- `getValidFilesCount()`
- `getInvalidFilesCount()`

## State Properties to Remove

### File Selection State
- `selectedFiles: File[]` - handled by FileUploadSectionComponent
- `selectedFilesWithQuality: SelectedFileWithQuality[]` - handled by FileUploadSectionComponent
- `isDragOver: boolean` - handled by FileUploadSectionComponent
- `isCheckingQuality: boolean` - handled by FileUploadSectionComponent
- `qualityCheckProgress: string` - handled by FileUploadSectionComponent
- `qualityCheckErrors: QualityCheckError[]` - handled by FileUploadSectionComponent
- `filePreviewCache: Map<File, string>` - handled by FileUploadSectionComponent

### Methods to Keep (Essential for Dashboard Logic)
- `onFilesSelected(files: File[])` - Event handler for FileUploadSectionComponent
- `onUploadCompleted(uploadedFiles: any[])` - Event handler for FileUploadSectionComponent
- `onUploadProgress(progress: number)` - Event handler for FileUploadSectionComponent
- `onQualityCheckCompleted(result: QualityCheckResult)` - Event handler for FileUploadSectionComponent
- `onFileRemoved(index: number)` - Event handler for FileUploadSectionComponent
- `onUploadedImageDeleted(event)` - Event handler for FileUploadSectionComponent
- `removeFile(idx: number)` - Keep for backward compatibility
- `uploadImages()` - Keep for backward compatibility (may be used elsewhere)

## Imports to Review
- Remove unused imports related to file handling if no longer needed
- Keep imports needed for event handling types

## Expected Size Reduction
- Remove ~200 lines of duplicate file handling logic
- Remove ~8 duplicate state properties
- Reduce DashboardComponent from 1,295 to ~1,095 lines (15% reduction)

## Verification Steps
1. Ensure all file upload functionality continues to work through FileUploadSectionComponent
2. Verify event bindings in template are complete
3. Test file selection, quality checking, upload progress, and deletion
4. Confirm no console errors after cleanup
5. Validate upload workflow remains intact

## Implementation Strategy
1. Remove duplicate methods in batches
2. Remove duplicate state properties
3. Clean up unused imports
4. Test functionality after each major removal
5. Ensure proper event handling remains intact