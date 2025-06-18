import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface GalleryImage {
  id: number;
  url: string;
  thumbnailUrl?: string;
  title: string;
  description?: string;
  style?: string;
  createdAt: Date;
  status: 'processing' | 'completed' | 'failed';
  type: 'generated' | 'enhanced' | 'original';
  downloadUrl?: string;
}

@Component({
  selector: 'app-photo-gallery',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="photo-gallery">
      <div class="gallery-header">
        <h3>{{title}}</h3>
        <div class="gallery-controls">
          <div class="view-toggle">
            <button 
              class="toggle-btn" 
              [class.active]="viewMode === 'grid'"
              (click)="setViewMode('grid')"
              aria-label="Grid view">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <rect x="3" y="3" width="7" height="7" stroke="currentColor" stroke-width="2"/>
                <rect x="14" y="3" width="7" height="7" stroke="currentColor" stroke-width="2"/>
                <rect x="3" y="14" width="7" height="7" stroke="currentColor" stroke-width="2"/>
                <rect x="14" y="14" width="7" height="7" stroke="currentColor" stroke-width="2"/>
              </svg>
            </button>
            <button 
              class="toggle-btn" 
              [class.active]="viewMode === 'list'"
              (click)="setViewMode('list')"
              aria-label="List view">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <line x1="8" y1="6" x2="21" y2="6" stroke="currentColor" stroke-width="2"/>
                <line x1="8" y1="12" x2="21" y2="12" stroke="currentColor" stroke-width="2"/>
                <line x1="8" y1="18" x2="21" y2="18" stroke="currentColor" stroke-width="2"/>
                <line x1="3" y1="6" x2="3.01" y2="6" stroke="currentColor" stroke-width="2"/>
                <line x1="3" y1="12" x2="3.01" y2="12" stroke="currentColor" stroke-width="2"/>
                <line x1="3" y1="18" x2="3.01" y2="18" stroke="currentColor" stroke-width="2"/>
              </svg>
            </button>
          </div>
          <div class="filter-controls">
            <select class="filter-select" (change)="onFilterChange($event)">
              <option value="all">All Images</option>
              <option value="generated">Generated</option>
              <option value="enhanced">Enhanced</option>
              <option value="original">Original</option>
            </select>
          </div>
        </div>
      </div>

      <div class="gallery-content" [class]="viewMode">
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
            *ngFor="let image of filteredImages; trackBy: trackByImageId"
            [class.processing]="image.status === 'processing'"
            [class.failed]="image.status === 'failed'">
            
            <div class="image-container" (click)="openImage(image)">
              <img 
                [src]="image.thumbnailUrl || image.url" 
                [alt]="image.title"
                class="gallery-image"
                (load)="onImageLoad($event)"
                (error)="onImageError($event)">
              
              <!-- Status Overlay -->
              <div class="status-overlay" *ngIf="image.status !== 'completed'">
                <div class="status-content">
                  <div class="spinner" *ngIf="image.status === 'processing'"></div>
                  <div class="error-icon" *ngIf="image.status === 'failed'">⚠️</div>
                  <span class="status-text">
                    {{image.status === 'processing' ? 'Processing...' : 'Failed'}}
                  </span>
                </div>
              </div>

              <!-- Type Badge -->
              <div class="type-badge" [class]="image.type">
                {{getTypeBadgeText(image.type)}}
              </div>
            </div>

            <div class="image-info">
              <h4 class="image-title">{{image.title}}</h4>
              <p class="image-meta">
                <span class="image-style" *ngIf="image.style">{{image.style}}</span>
                <span class="image-date">{{formatDate(image.createdAt)}}</span>
              </p>
              <div class="image-actions">
                <button 
                  class="action-btn download-btn" 
                  (click)="downloadImage(image)"
                  [disabled]="image.status !== 'completed'"
                  title="Download">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <path d="M21 15V19C21 20.1046 20.1046 21 19 21H5C3.89543 21 3 20.1046 3 19V15" stroke="currentColor" stroke-width="2"/>
                    <polyline points="7,10 12,15 17,10" stroke="currentColor" stroke-width="2"/>
                    <line x1="12" y1="15" x2="12" y2="3" stroke="currentColor" stroke-width="2"/>
                  </svg>
                </button>
                <button 
                  class="action-btn share-btn" 
                  (click)="shareImage(image)"
                  [disabled]="image.status !== 'completed'"
                  title="Share">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <circle cx="18" cy="5" r="3" stroke="currentColor" stroke-width="2"/>
                    <circle cx="6" cy="12" r="3" stroke="currentColor" stroke-width="2"/>
                    <circle cx="18" cy="19" r="3" stroke="currentColor" stroke-width="2"/>
                    <line x1="8.59" y1="13.51" x2="15.42" y2="17.49" stroke="currentColor" stroke-width="2"/>
                    <line x1="15.41" y1="6.51" x2="8.59" y2="10.49" stroke="currentColor" stroke-width="2"/>
                  </svg>
                </button>
                <button 
                  class="action-btn delete-btn" 
                  (click)="deleteImage(image)"
                  title="Delete">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <polyline points="3,6 5,6 21,6" stroke="currentColor" stroke-width="2"/>
                    <path d="M19,6V20C19,21.1046 18.1046,22 17,22H7C5.89543,22 5,21.1046 5,20V6M8,6V4C8,2.89543 8.89543,2 10,2H14C15.1046,2 16,2.89543 16,4V6" stroke="currentColor" stroke-width="2"/>
                  </svg>
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- List View -->
        <div class="gallery-list" *ngIf="viewMode === 'list' && filteredImages.length > 0">
          <div 
            class="list-item" 
            *ngFor="let image of filteredImages; trackBy: trackByImageId"
            [class.processing]="image.status === 'processing'"
            [class.failed]="image.status === 'failed'">
            
            <div class="list-thumbnail" (click)="openImage(image)">
              <img [src]="image.thumbnailUrl || image.url" [alt]="image.title">
              <div class="status-indicator" [class]="image.status"></div>
            </div>

            <div class="list-content">
              <div class="list-header">
                <h4 class="list-title">{{image.title}}</h4>
                <div class="type-badge small" [class]="image.type">
                  {{getTypeBadgeText(image.type)}}
                </div>
              </div>
              <p class="list-description" *ngIf="image.description">{{image.description}}</p>
              <div class="list-meta">
                <span class="meta-item" *ngIf="image.style">Style: {{image.style}}</span>
                <span class="meta-item">{{formatDate(image.createdAt)}}</span>
                <span class="meta-item status-text" [class]="image.status">
                  {{getStatusText(image.status)}}
                </span>
              </div>
            </div>

            <div class="list-actions">
              <button 
                class="action-btn download-btn" 
                (click)="downloadImage(image)"
                [disabled]="image.status !== 'completed'">
                Download
              </button>
              <button 
                class="action-btn share-btn" 
                (click)="shareImage(image)"
                [disabled]="image.status !== 'completed'">
                Share
              </button>
              <button 
                class="action-btn delete-btn" 
                (click)="deleteImage(image)">
                Delete
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Bulk Actions -->
      <div class="bulk-actions" *ngIf="filteredImages.length > 1">
        <button class="btn btn-secondary" (click)="selectAll()">
          {{selectedImages.length === filteredImages.length ? 'Deselect All' : 'Select All'}}
        </button>
        <button 
          class="btn btn-primary" 
          (click)="downloadSelected()"
          [disabled]="selectedImages.length === 0">
          Download Selected ({{selectedImages.length}})
        </button>
      </div>
    </div>
  `,
  styleUrls: ['./photo-gallery.component.sass']
})
export class PhotoGalleryComponent implements OnInit {
  @Input() images: GalleryImage[] = [];
  @Input() title: string = 'Photo Gallery';
  @Input() allowSelection: boolean = true;
  @Input() showBulkActions: boolean = true;

  @Output() imageClick = new EventEmitter<GalleryImage>();
  @Output() imageDownload = new EventEmitter<GalleryImage>();
  @Output() imageShare = new EventEmitter<GalleryImage>();
  @Output() imageDelete = new EventEmitter<GalleryImage>();
  @Output() bulkDownload = new EventEmitter<GalleryImage[]>();

  viewMode: 'grid' | 'list' = 'grid';
  filterType: string = 'all';
  selectedImages: GalleryImage[] = [];
  filteredImages: GalleryImage[] = [];

  ngOnInit() {
    this.updateFilteredImages();
  }

  ngOnChanges() {
    this.updateFilteredImages();
  }

  setViewMode(mode: 'grid' | 'list') {
    this.viewMode = mode;
  }

  onFilterChange(event: any) {
    this.filterType = event.target.value;
    this.updateFilteredImages();
  }

  updateFilteredImages() {
    if (this.filterType === 'all') {
      this.filteredImages = [...this.images];
    } else {
      this.filteredImages = this.images.filter(img => img.type === this.filterType);
    }
  }

  trackByImageId(index: number, image: GalleryImage): number {
    return image.id;
  }

  openImage(image: GalleryImage) {
    this.imageClick.emit(image);
  }

  downloadImage(image: GalleryImage) {
    this.imageDownload.emit(image);
  }

  shareImage(image: GalleryImage) {
    this.imageShare.emit(image);
  }

  deleteImage(image: GalleryImage) {
    this.imageDelete.emit(image);
  }

  selectAll() {
    if (this.selectedImages.length === this.filteredImages.length) {
      this.selectedImages = [];
    } else {
      this.selectedImages = [...this.filteredImages];
    }
  }

  downloadSelected() {
    this.bulkDownload.emit(this.selectedImages);
  }

  onImageLoad(event: any) {
    // Handle successful image load
  }

  onImageError(event: any) {
    // Handle image load error - could set a placeholder
    event.target.src = 'assets/placeholder-image.png';
  }

  getTypeBadgeText(type: string): string {
    switch (type) {
      case 'generated': return 'AI Generated';
      case 'enhanced': return 'Enhanced';
      case 'original': return 'Original';
      default: return type;
    }
  }

  getStatusText(status: string): string {
    switch (status) {
      case 'processing': return 'Processing...';
      case 'completed': return 'Ready';
      case 'failed': return 'Failed';
      default: return status;
    }
  }

  formatDate(date: Date): string {
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(new Date(date));
  }
}