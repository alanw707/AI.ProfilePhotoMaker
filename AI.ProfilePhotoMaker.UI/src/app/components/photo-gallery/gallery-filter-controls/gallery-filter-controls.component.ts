import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GalleryImage } from '../photo-gallery.component';

export interface FilterControls {
  filterType: string;
  viewMode: 'grid' | 'list';
  pageSize: number;
}

@Component({
  selector: 'app-gallery-filter-controls',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gallery-filter-controls.component.html',
  styleUrls: ['./gallery-filter-controls.component.sass']
})
export class GalleryFilterControlsComponent {
  @Input() title: string = 'Photo Gallery';
  @Input() filterType: string = 'generated';
  @Input() viewMode: 'grid' | 'list' = 'grid';
  @Input() pageSize: number = 12;
  @Input() filteredImages: GalleryImage[] = [];
  @Input() selectedImages: GalleryImage[] = [];
  @Input() showBulkActions: boolean = true;
  @Input() allowSelection: boolean = true;

  @Output() filterChange = new EventEmitter<string>();
  @Output() viewModeChange = new EventEmitter<'grid' | 'list'>();
  @Output() pageSizeChange = new EventEmitter<number>();
  @Output() selectAll = new EventEmitter<void>();
  @Output() downloadSelected = new EventEmitter<void>();

  onFilterChange(event: any) {
    this.filterChange.emit(event.target.value);
  }

  setViewMode(mode: 'grid' | 'list') {
    this.viewModeChange.emit(mode);
  }

  onPageSizeChange(event: Event) {
    const target = event.target as HTMLSelectElement;
    if (target && target.value) {
      this.pageSizeChange.emit(+target.value);
    }
  }

  onSelectAll() {
    this.selectAll.emit();
  }

  onDownloadSelected() {
    this.downloadSelected.emit();
  }

  get selectAllText(): string {
    return this.selectedImages.length === this.filteredImages.length ? 'Deselect All' : 'Select All';
  }

  get shouldShowBulkActions(): boolean {
    return this.showBulkActions && this.filteredImages.length > 1;
  }
}