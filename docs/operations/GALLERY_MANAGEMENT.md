# Gallery Management & Image Operations

## Overview

The Gallery Management system provides a comprehensive interface for users to view, filter, download, and manage their generated AI profile photos. It includes advanced filtering, bulk operations, and self-healing capabilities to ensure data consistency between the filesystem and database.

## Core Features

### Image Gallery Display

1. **Grid Layout**
   - Responsive grid (4 columns desktop, 2 mobile)
   - Lazy loading for performance
   - Thumbnail generation for quick preview
   - Full-size modal view on click

2. **Image Metadata**
   - Style name and generation date
   - File size and dimensions
   - Download count tracking
   - Enhancement status indicator

### Filtering & Search

#### Filter Options

```typescript
interface GalleryFilters {
  styles: string[];        // Filter by selected styles
  dateRange: DateRange;    // Filter by generation date
  imageType: ImageType;    // Selfie, Generated, Enhanced
  showDeleted: boolean;    // Include soft-deleted images
}
```

#### Implementation

```typescript
@Component({
  selector: 'app-gallery-filter',
  template: `
    <div class="filter-controls">
      <mat-select [(value)]="selectedStyles" multiple>
        <mat-option *ngFor="let style of availableStyles" 
                    [value]="style">
          {{style.displayName}}
        </mat-option>
      </mat-select>
      
      <mat-date-range-picker>
        <input matStartDate [(ngModel)]="startDate">
        <input matEndDate [(ngModel)]="endDate">
      </mat-date-range-picker>
    </div>
  `
})
```

### Image Operations

#### Single Image Actions

1. **Download**
   - Direct download with original filename
   - Tracks download count
   - Generates download link dynamically

2. **Delete**
   - Soft delete with confirmation dialog
   - Removes from view immediately
   - Background cleanup after retention period

3. **Enhance** (for generated images)
   - One-click enhancement
   - Shows before/after preview
   - Consumes 1 credit

#### Bulk Operations

```typescript
interface BulkOperations {
  downloadSelected(): Promise<void>;  // ZIP download
  deleteSelected(): Promise<void>;    // Batch delete
  enhanceSelected(): Promise<void>;   // Batch enhance
}
```

### Pagination

#### Implementation

```typescript
interface PaginationConfig {
  pageSize: number;      // Default: 12
  currentPage: number;   // 1-based index
  totalItems: number;    // From API
  totalPages: number;    // Calculated
}

// API call with pagination
this.http.get<PagedResult<ProcessedImage>>('/api/images', {
  params: {
    page: this.currentPage,
    pageSize: this.pageSize,
    ...filters
  }
});
```

## Self-Healing Gallery System

### Problem Addressed

Gallery automatically detects and repairs inconsistencies between filesystem and database, ensuring all generated images are visible.

### Auto-Repair Mechanism

```typescript
async checkAndRepairGallery(): Promise<void> {
  if (this.isRepairInProgress) return;
  
  try {
    const response = await this.http.post('/api/images/reconcile', {
      checkFilesystem: true,
      createMissingRecords: true
    }).toPromise();
    
    if (response.repairedCount > 0) {
      await this.refreshGallery();
      this.notificationService.success(
        `Found and added ${response.repairedCount} images`
      );
    }
  } catch (error) {
    console.error('Gallery repair failed:', error);
  }
}
```

### Reconciliation Process

1. **Filesystem Scan**
   - Scans `/generated/{userId}/` directory
   - Identifies image files by pattern
   - Extracts metadata from filename

2. **Database Comparison**
   - Queries existing ProcessedImage records
   - Identifies missing entries
   - Preserves existing data

3. **Record Creation**
   - Creates missing database entries
   - Sets appropriate timestamps
   - Maintains data integrity

## Technical Architecture

### Backend Services

#### ImageController Endpoints

```csharp
[ApiController]
[Route("api/[controller]")]
public class ImageController : BaseController
{
    [HttpGet("gallery")]
    public async Task<IActionResult> GetGalleryImages(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? styles = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        // Pagination and filtering logic
    }
    
    [HttpDelete("{imageId}")]
    public async Task<IActionResult> DeleteImage(int imageId)
    {
        // Soft delete implementation
    }
    
    [HttpPost("reconcile")]
    public async Task<IActionResult> ReconcileImages()
    {
        // Self-healing logic
    }
}
```

### Frontend Components

#### Gallery Component Structure

```
photo-gallery/
├── photo-gallery.component.ts       # Main gallery container
├── gallery-filter-controls/         # Filter UI
├── gallery-image-actions/          # Action buttons
└── gallery-pagination/             # Page navigation
```

#### State Management

```typescript
export class GalleryStateService {
  private images$ = new BehaviorSubject<ProcessedImage[]>([]);
  private filters$ = new BehaviorSubject<GalleryFilters>({});
  private loading$ = new BehaviorSubject<boolean>(false);
  
  readonly displayedImages$ = combineLatest([
    this.images$,
    this.filters$
  ]).pipe(
    map(([images, filters]) => this.applyFilters(images, filters))
  );
}
```

### Database Optimization

#### Indexes for Performance

```sql
CREATE INDEX idx_processed_images_user_date 
ON ProcessedImages(UserId, GeneratedAt DESC);

CREATE INDEX idx_processed_images_style 
ON ProcessedImages(StyleName);

CREATE INDEX idx_processed_images_type_deleted 
ON ProcessedImages(ImageType, IsDeleted);
```

## User Experience Features

### Loading States

```typescript
<div class="gallery-container">
  <div *ngIf="loading$ | async" class="loading-overlay">
    <mat-spinner></mat-spinner>
    <p>Loading your photos...</p>
  </div>
  
  <div *ngIf="(images$ | async)?.length === 0" class="empty-state">
    <img src="assets/empty-gallery.svg">
    <h3>No photos yet</h3>
    <p>Generate your first AI profile photo!</p>
    <button mat-raised-button color="primary" 
            routerLink="/app/enhance">
      Get Started
    </button>
  </div>
</div>
```

### Image Preview Modal

```typescript
@Component({
  selector: 'app-image-preview',
  template: `
    <div class="preview-modal" (click)="close()">
      <img [src]="imageUrl" (click)="$event.stopPropagation()">
      <div class="preview-actions">
        <button mat-icon-button (click)="download()">
          <mat-icon>download</mat-icon>
        </button>
        <button mat-icon-button (click)="delete()">
          <mat-icon>delete</mat-icon>
        </button>
      </div>
    </div>
  `
})
```

### Responsive Design

```scss
.gallery-grid {
  display: grid;
  gap: 1rem;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  
  @media (max-width: 768px) {
    grid-template-columns: repeat(2, 1fr);
    gap: 0.5rem;
  }
  
  @media (max-width: 480px) {
    grid-template-columns: 1fr;
  }
}
```

## Performance Optimization

### Image Loading

1. **Lazy Loading**
   - Load images as they enter viewport
   - Use Intersection Observer API
   - Placeholder images during load

2. **Thumbnail Generation**
   - Server-side thumbnail creation
   - Multiple sizes for different views
   - CDN integration for fast delivery

3. **Caching Strategy**
   - Browser cache for static images
   - Service worker for offline access
   - API response caching (5 minutes)

### Batch Operations

```typescript
async downloadMultiple(imageIds: number[]): Promise<void> {
  // Create download queue
  const queue = new DownloadQueue(maxConcurrent: 3);
  
  // Add all images to queue
  imageIds.forEach(id => {
    queue.add(() => this.downloadImage(id));
  });
  
  // Process queue with progress updates
  await queue.process((progress) => {
    this.updateProgress(progress);
  });
}
```

## Error Handling

### Common Scenarios

1. **Image Not Found**
   - Check filesystem if database miss
   - Attempt recovery via reconciliation
   - Show user-friendly error

2. **Download Failures**
   - Retry with exponential backoff
   - Provide alternative download method
   - Log errors for debugging

3. **Gallery Load Errors**
   - Show cached data if available
   - Provide refresh option
   - Graceful degradation

## Best Practices

1. **Data Consistency**
   - Run reconciliation on gallery load
   - Monitor webhook failures
   - Regular database maintenance

2. **User Experience**
   - Show loading states clearly
   - Provide feedback for all actions
   - Implement optimistic updates

3. **Performance**
   - Paginate large result sets
   - Implement virtual scrolling
   - Optimize image delivery

## API Reference

See [API Reference](./API_REFERENCE.md#gallery) for detailed endpoint documentation.