import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnInit,
  Output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { GalleryFilterControlsComponent } from './gallery-filter-controls/gallery-filter-controls.component';
import { GalleryPaginationComponent } from './gallery-pagination/gallery-pagination.component';
import { GalleryImageActionsComponent } from './gallery-image-actions/gallery-image-actions.component';

export interface GalleryImage {
  id: number;
  url: string;
  thumbnailUrl?: string;
  title: string;
  description?: string;
  style?: string;
  createdAt: Date;
  status: 'processing' | 'completed' | 'failed';
  type: 'generated' | 'original' | 'enhanced';
  downloadUrl?: string;
}

@Component({
  selector: 'app-photo-gallery',
  standalone: true,
  imports: [
    CommonModule,
    GalleryFilterControlsComponent,
    GalleryPaginationComponent,
    GalleryImageActionsComponent,
  ],
  template: `
    <div class="photo-gallery">
      <app-gallery-filter-controls
        [title]="title"
        [filterType]="filterType"
        [viewMode]="viewMode"
        [pageSize]="pageSize"
        [filteredImages]="filteredImages"
        [selectedImages]="selectedImages"
        [showBulkActions]="showBulkActions"
        [allowSelection]="allowSelection"
        (filterChange)="onFilterChange($event)"
        (viewModeChange)="setViewMode($event)"
        (pageSizeChange)="changePageSize($event)"
        (selectAll)="selectAll()"
        (downloadSelected)="downloadSelected()"
      >
      </app-gallery-filter-controls>

      <div [class]="'gallery-content ' + viewMode">
        <!-- Empty State -->
        <div class="empty-state" *ngIf="filteredImages.length === 0">
          <div class="empty-icon">📸</div>
          <h4>No images yet</h4>
          <p>Upload some photos or generate new ones to see them here.</p>
        </div>

        <!-- Grid View -->
        <div class="gallery-grid" *ngIf="viewMode === 'grid' && filteredImages.length > 0">
          <div
            class="gallery-item"
            *ngFor="let image of paginatedImages; trackBy: trackByImageId"
            [class.processing]="image.status === 'processing'"
            [class.failed]="image.status === 'failed'"
          >
            <div class="image-container" [class.selected]="isSelected(image)">
              <img
                [src]="image.thumbnailUrl || image.url"
                [alt]="image.title"
                class="gallery-image"
                (load)="onImageLoad($event)"
                (error)="onImageError($event)"
                (click)="onImageClick(image, $event)"
              />

              <!-- Selection Overlay -->
              <div class="selection-overlay" (click)="toggleSelection(image)">
                <div class="checkmark">
                  <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
                    <path
                      d="M20 6L9 17L4 12"
                      stroke="currentColor"
                      stroke-width="3"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                    />
                  </svg>
                </div>
              </div>

              <!-- Status Overlay -->
              <div class="status-overlay" *ngIf="image.status !== 'completed'">
                <div class="status-content">
                  <div class="spinner" *ngIf="image.status === 'processing'"></div>
                  <div class="error-icon" *ngIf="image.status === 'failed'">⚠️</div>
                  <span class="status-text">
                    {{ image.status === 'processing' ? 'Processing...' : 'Failed' }}
                  </span>
                </div>
              </div>

              <!-- Type Badge -->
              <div [class]="'type-badge ' + image.type">
                {{ getTypeBadgeText(image.type) }}
              </div>

              <!-- View Button -->
              <app-gallery-image-actions
                [image]="image"
                viewMode="grid"
                [showViewButton]="true"
                (view)="openImage($event)"
                (download)="downloadImage($event)"
                (share)="shareImage($event)"
                (delete)="deleteImage($event)"
              >
              </app-gallery-image-actions>
            </div>

            <div class="image-info">
              <h4 class="image-title">{{ image.title }}</h4>
              <p class="image-meta">
                <span class="image-style" *ngIf="image.style">{{
                  formatStyleName(image.style)
                }}</span>
                <span class="image-date">{{ formatDate(image.createdAt) }}</span>
              </p>
              <app-gallery-image-actions
                [image]="image"
                viewMode="grid"
                [showViewButton]="false"
                (view)="openImage($event)"
                (download)="downloadImage($event)"
                (share)="shareImage($event)"
                (refine)="refineImage($event)"
                (delete)="deleteImage($event)"
              >
              </app-gallery-image-actions>
            </div>
          </div>
        </div>

        <!-- List View -->
        <div class="gallery-list" *ngIf="viewMode === 'list' && filteredImages.length > 0">
          <div
            class="list-item"
            *ngFor="let image of paginatedImages; trackBy: trackByImageId"
            [class.processing]="image.status === 'processing'"
            [class.failed]="image.status === 'failed'"
            [class.selected]="isSelected(image)"
          >
            <div class="list-thumbnail" (click)="onImageClick(image, $event)">
              <img [src]="image.thumbnailUrl || image.url" [alt]="image.title" />
            </div>

            <div class="list-content">
              <div class="list-header">
                <h4 class="list-title">{{ image.title }}</h4>
                <div [class]="'type-badge small ' + image.type">
                  {{ getTypeBadgeText(image.type) }}
                </div>
              </div>
              <p class="list-description" *ngIf="image.description">{{ image.description }}</p>
              <div class="list-meta">
                <span class="meta-item" *ngIf="image.style"
                  >Style: {{ formatStyleName(image.style) }}</span
                >
                <span class="meta-item">{{ formatDate(image.createdAt) }}</span>
                <span [class]="'meta-item status-text ' + image.status">
                  {{ getStatusText(image.status) }}
                </span>
              </div>
            </div>

            <app-gallery-image-actions
              [image]="image"
              viewMode="list"
              [showViewButton]="true"
              (view)="openImage($event)"
              (download)="downloadImage($event)"
              (share)="shareImage($event)"
              (refine)="refineImage($event)"
              (delete)="deleteImage($event)"
            >
            </app-gallery-image-actions>
          </div>
        </div>
      </div>

      <!-- Pagination Controls -->
      <app-gallery-pagination
        [totalItems]="filteredImages.length"
        [pageSize]="pageSize"
        [currentPage]="currentPage"
        (pageChange)="goToPage($event)"
        (pageSizeChange)="changePageSize($event)"
      >
      </app-gallery-pagination>
    </div>
  `,
  styleUrls: ['./photo-gallery.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PhotoGalleryComponent implements OnInit, OnChanges {
  @Input() images: GalleryImage[] = [];
  @Input() title = 'Photo Workspace';
  @Input() allowSelection = true;
  @Input() showBulkActions = true;

  // Make Math available in template
  mathHelper = Math;

  @Output() imageClick = new EventEmitter<GalleryImage>();
  @Output() imageDownload = new EventEmitter<GalleryImage>();
  @Output() imageShare = new EventEmitter<GalleryImage>();
  @Output() imageRefine = new EventEmitter<GalleryImage>();
  @Output() imageDelete = new EventEmitter<GalleryImage>();
  @Output() bulkDownload = new EventEmitter<GalleryImage[]>();

  viewMode: 'grid' | 'list' = 'grid';
  filterType = 'generated';
  selectedImages: GalleryImage[] = [];
  filteredImages: GalleryImage[] = [];

  // Pagination properties
  currentPage = 1;
  pageSize = 12;
  totalPages = 1;
  paginatedImages: GalleryImage[] = [];

  ngOnInit(): void {
    this.updateFilteredImages();
  }

  ngOnChanges(): void {
    this.updateFilteredImages();
    // Deselect any images that no longer exist after deletion
    this.selectedImages = this.selectedImages.filter(sel =>
      this.images.some(img => img.id === sel.id)
    );
  }

  setViewMode(mode: 'grid' | 'list'): void {
    this.viewMode = mode;
  }

  onFilterChange(filterType: string): void {
    this.filterType = filterType;
    this.updateFilteredImages();
  }

  updateFilteredImages(): void {
    const normalizedFilter =
      this.filterType === null || this.filterType === undefined ? 'generated' : this.filterType;

    this.filterType = normalizedFilter;

    if (normalizedFilter === 'all') {
      this.filteredImages = [...this.images];
    } else {
      this.filteredImages = this.images.filter(img => img.type === normalizedFilter);
    }

    this.updatePagination();
  }

  updatePagination(): void {
    if (this.pageSize <= 0) {
      this.totalPages = 0;
      this.currentPage = 1;
      this.paginatedImages = [];
      return;
    }

    this.totalPages = Math.ceil(this.filteredImages.length / this.pageSize);

    if (this.totalPages === 0) {
      this.currentPage = 1;
      this.paginatedImages = [];
      return;
    }

    if (this.currentPage < 1) {
      this.currentPage = 1;
    } else if (this.currentPage > this.totalPages) {
      this.currentPage = this.totalPages;
    }

    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    this.paginatedImages = this.filteredImages.slice(startIndex, endIndex);
  }

  trackByImageId(index: number, image: GalleryImage): number {
    return image.id;
  }

  openImage(image: GalleryImage): void {
    this.imageClick.emit(image);
  }

  downloadImage(image: GalleryImage): void {
    this.imageDownload.emit(image);
  }

  shareImage(image: GalleryImage): void {
    this.imageShare.emit(image);
  }

  refineImage(image: GalleryImage): void {
    this.imageRefine.emit(image);
  }

  deleteImage(image: GalleryImage): void {
    this.imageDelete.emit(image);
  }

  selectAll(): void {
    if (this.selectedImages.length === this.filteredImages.length) {
      this.selectedImages = [];
    } else {
      this.selectedImages = [...this.filteredImages];
    }
  }

  downloadSelected(): void {
    this.bulkDownload.emit(this.selectedImages);
  }

  clearSelections(): void {
    this.selectedImages = [];
  }

  isSelected(image: GalleryImage): boolean {
    return this.selectedImages.some(selected => selected.id === image.id);
  }

  toggleSelection(image: GalleryImage): void {
    const index = this.selectedImages.findIndex(selected => selected.id === image.id);
    if (index > -1) {
      // Create a new array without the deselected image to trigger OnPush change detection
      this.selectedImages = [
        ...this.selectedImages.slice(0, index),
        ...this.selectedImages.slice(index + 1),
      ];
    } else {
      // Reassign with a new array including the newly selected image
      this.selectedImages = [...this.selectedImages, image];
    }
  }

  onImageClick(image: GalleryImage, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.toggleSelection(image);
  }

  onImageLoad(_event: unknown): void {
    // Handle successful image load
  }

  onImageError(event: Event): void {
    // Handle image load error gracefully
    const target = event.target as HTMLImageElement;
    console.warn('Image failed to load:', target.src);

    // Create a simple gray placeholder with an icon
    const canvas = document.createElement('canvas');
    canvas.width = 300;
    canvas.height = 300;
    const ctx = canvas.getContext('2d');

    if (ctx) {
      // Gray background
      ctx.fillStyle = '#f3f4f6';
      ctx.fillRect(0, 0, 300, 300);

      // Darker gray border
      ctx.strokeStyle = '#e5e7eb';
      ctx.lineWidth = 2;
      ctx.strokeRect(1, 1, 298, 298);

      // Simple image icon
      ctx.fillStyle = '#9ca3af';
      ctx.font = '48px Arial';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText('📷', 150, 120);

      // Error text
      ctx.font = '14px Arial';
      ctx.fillText('Image unavailable', 150, 180);

      // Set the canvas as the image source
      target.src = canvas.toDataURL();
    }
  }

  getTypeBadgeText(type: string): string {
    switch (type) {
      case 'generated':
        return 'AI Generated';
      case 'original':
        return 'Original';
      case 'enhanced':
        return 'Enhanced';
      default:
        return type;
    }
  }

  getStatusText(status: string): string {
    switch (status) {
      case 'processing':
        return 'Processing...';
      case 'completed':
        return 'Ready';
      case 'failed':
        return 'Failed';
      default:
        return status;
    }
  }

  formatDate(date: Date): string {
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(date));
  }

  formatStyleName(style: string): string {
    if (!style) {
      return '';
    }
    return style
      .replace(/[-_/]/g, ' ') // Replace dashes, underscores, and slashes with spaces
      .split(' ')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(' ');
  }

  // Pagination methods
  goToPage(page: number): void {
    this.currentPage = page;
    this.updatePagination();
  }

  changePageSize(size: number): void {
    this.pageSize = size;
    this.currentPage = 1;
    this.updatePagination();
  }
}
