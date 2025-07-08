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
  type: 'generated' | 'original';
  downloadUrl?: string;
}

@Component({
  selector: 'app-photo-gallery',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="photo-gallery">
      <div class="gallery-header">
        <div class="header-left">
          <h3>{{title}}</h3>
          <div class="filter-controls">
            <select class="filter-select" [value]="filterType" (change)="onFilterChange($event)">
              <option value="all">All Images</option>
              <option value="generated">Generated</option>
              <option value="original">Original</option>
            </select>
          </div>
        </div>
        <div class="header-center">
          <div class="view-toggle">
            <button 
              class="toggle-btn" 
              [class.active]="viewMode === 'grid'"
              (click)="setViewMode('grid')"
              aria-label="Grid view">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
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
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
                <line x1="8" y1="6" x2="21" y2="6" stroke="currentColor" stroke-width="2"/>
                <line x1="8" y1="12" x2="21" y2="12" stroke="currentColor" stroke-width="2"/>
                <line x1="8" y1="18" x2="21" y2="18" stroke="currentColor" stroke-width="2"/>
                <line x1="3" y1="6" x2="3.01" y2="6" stroke="currentColor" stroke-width="2"/>
                <line x1="3" y1="12" x2="3.01" y2="12" stroke="currentColor" stroke-width="2"/>
                <line x1="3" y1="18" x2="3.01" y2="18" stroke="currentColor" stroke-width="2"/>
              </svg>
            </button>
          </div>
        </div>
        <div class="header-right">
          <div class="action-controls" *ngIf="filteredImages.length > 1">
            <button class="action-btn select-all-btn" (click)="selectAll()">
              {{selectedImages.length === filteredImages.length ? 'Deselect All' : 'Select All'}}
            </button>
            <button 
              class="action-btn download-btn" 
              (click)="downloadSelected()"
              [disabled]="selectedImages.length === 0">
              Download ({{selectedImages.length}})
            </button>
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
            *ngFor="let image of paginatedImages; trackBy: trackByImageId"
            [class.processing]="image.status === 'processing'"
            [class.failed]="image.status === 'failed'">
            
            <div class="image-container" [class.selected]="isSelected(image)">
              <img 
                [src]="image.thumbnailUrl || image.url" 
                [alt]="image.title"
                class="gallery-image"
                (load)="onImageLoad($event)"
                (error)="onImageError($event)"
                (click)="onImageClick(image, $event)">
              
              <!-- Selection Overlay -->
              <div class="selection-overlay" (click)="toggleSelection(image)">
                <div class="checkmark">
                  <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
                    <path d="M20 6L9 17L4 12" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>
                </div>
              </div>
              
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
              
              <!-- View Button -->
              <button class="view-btn" (click)="openImage(image); $event.stopPropagation()" title="View Image">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke="currentColor" stroke-width="2"/>
                  <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2"/>
                </svg>
              </button>
            </div>

            <div class="image-info">
              <h4 class="image-title">{{image.title}}</h4>
              <p class="image-meta">
                <span class="image-style" *ngIf="image.style">{{formatStyleName(image.style)}}</span>
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
            *ngFor="let image of paginatedImages; trackBy: trackByImageId"
            [class.processing]="image.status === 'processing'"
            [class.failed]="image.status === 'failed'"
            [class.selected]="isSelected(image)">
            
            <div class="list-thumbnail" (click)="onImageClick(image, $event)">
              <img [src]="image.thumbnailUrl || image.url" [alt]="image.title">
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
                <span class="meta-item" *ngIf="image.style">Style: {{formatStyleName(image.style)}}</span>
                <span class="meta-item">{{formatDate(image.createdAt)}}</span>
                <span class="meta-item status-text" [class]="image.status">
                  {{getStatusText(image.status)}}
                </span>
              </div>
            </div>

            <div class="list-actions">
              <button 
                class="action-btn view-btn" 
                (click)="openImage(image)"
                [disabled]="image.status !== 'completed'"
                title="View Image">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke="currentColor" stroke-width="2"/>
                  <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2"/>
                </svg>
              </button>
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

      <!-- Pagination Controls -->
      <div class="pagination-section" *ngIf="filteredImages.length > pageSize">
        <div class="pagination-info">
          <span>Showing {{(currentPage - 1) * pageSize + 1}}-{{Math.min(currentPage * pageSize, filteredImages.length)}} of {{filteredImages.length}} images</span>
          <select class="page-size-select" [value]="pageSize" (change)="onPageSizeChange($event)">
            <option value="12">12 per page</option>
            <option value="24">24 per page</option>
            <option value="48">48 per page</option>
          </select>
        </div>
        
        <div class="pagination-controls">
          <button 
            class="pagination-btn" 
            (click)="previousPage()" 
            [disabled]="currentPage === 1">
            Previous
          </button>
          
          <div class="page-numbers">
            <button 
              *ngFor="let page of getPageNumbers()"
              class="page-btn"
              [class.active]="page === currentPage"
              [class.ellipsis]="page === -1"
              [disabled]="page === -1"
              (click)="page !== -1 && goToPage(page)">
              {{page === -1 ? '...' : page}}
            </button>
          </div>
          
          <button 
            class="pagination-btn" 
            (click)="nextPage()" 
            [disabled]="currentPage === totalPages">
            Next
          </button>
        </div>
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

  // Make Math available in template
  Math = Math;

  @Output() imageClick = new EventEmitter<GalleryImage>();
  @Output() imageDownload = new EventEmitter<GalleryImage>();
  @Output() imageShare = new EventEmitter<GalleryImage>();
  @Output() imageDelete = new EventEmitter<GalleryImage>();
  @Output() bulkDownload = new EventEmitter<GalleryImage[]>();

  viewMode: 'grid' | 'list' = 'grid';
  filterType: string = 'generated';
  selectedImages: GalleryImage[] = [];
  filteredImages: GalleryImage[] = [];
  
  // Pagination properties
  currentPage: number = 1;
  pageSize: number = 12;
  totalPages: number = 1;
  paginatedImages: GalleryImage[] = [];

  ngOnInit() {
    this.updateFilteredImages();
  }

  ngOnChanges() {
    this.updateFilteredImages();
    // Deselect any images that no longer exist after deletion
    this.selectedImages = this.selectedImages.filter(sel => this.images.some(img => img.id === sel.id));
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
    this.updatePagination();
  }

  updatePagination() {
    this.totalPages = Math.ceil(this.filteredImages.length / this.pageSize);
    if (this.currentPage > this.totalPages) {
      this.currentPage = Math.max(1, this.totalPages);
    }
    
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    this.paginatedImages = this.filteredImages.slice(startIndex, endIndex);
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

  clearSelections() {
    this.selectedImages = [];
  }

  isSelected(image: GalleryImage): boolean {
    return this.selectedImages.some(selected => selected.id === image.id);
  }

  toggleSelection(image: GalleryImage) {
    const index = this.selectedImages.findIndex(selected => selected.id === image.id);
    if (index > -1) {
      this.selectedImages.splice(index, 1);
    } else {
      this.selectedImages.push(image);
    }
  }

  onImageClick(image: GalleryImage, event?: Event) {
    if (event) {
      event.stopPropagation();
    }
    this.toggleSelection(image);
  }

  onImageLoad(event: any) {
    // Handle successful image load
  }

  onImageError(event: any) {
    // Handle image load error gracefully
    console.warn('Image failed to load:', event.target.src);
    
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
      event.target.src = canvas.toDataURL();
    }
  }

  getTypeBadgeText(type: string): string {
    switch (type) {
      case 'generated': return 'AI Generated';
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

  formatStyleName(style: string): string {
    if (!style) return '';
    return style
      .replace(/[-_/]/g, ' ')  // Replace dashes, underscores, and slashes with spaces
      .split(' ')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(' ');
  }

  // Pagination methods
  goToPage(page: number) {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.updatePagination();
    }
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.updatePagination();
    }
  }

  previousPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.updatePagination();
    }
  }

  changePageSize(size: number) {
    this.pageSize = size;
    this.currentPage = 1;
    this.updatePagination();
  }

  onPageSizeChange(event: Event) {
    const target = event.target as HTMLSelectElement;
    if (target && target.value) {
      this.changePageSize(+target.value);
    }
  }

  getPageNumbers(): number[] {
    const pages: number[] = [];
    const maxVisible = 5;
    
    if (this.totalPages <= maxVisible) {
      for (let i = 1; i <= this.totalPages; i++) {
        pages.push(i);
      }
    } else {
      pages.push(1);
      
      if (this.currentPage > 3) {
        pages.push(-1); // Ellipsis
      }
      
      const start = Math.max(2, this.currentPage - 1);
      const end = Math.min(this.totalPages - 1, this.currentPage + 1);
      
      for (let i = start; i <= end; i++) {
        if (i !== 1 && i !== this.totalPages) {
          pages.push(i);
        }
      }
      
      if (this.currentPage < this.totalPages - 2) {
        pages.push(-1); // Ellipsis
      }
      
      if (this.totalPages > 1) {
        pages.push(this.totalPages);
      }
    }
    
    return pages;
  }
}