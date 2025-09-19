import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
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
  styleUrls: ['./gallery-filter-controls.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GalleryFilterControlsComponent {
  @Input() title = 'Photo Gallery';
  @Input() filterType = 'generated';
  @Input() viewMode: 'grid' | 'list' = 'grid';
  @Input() pageSize = 12;
  @Input() filteredImages: GalleryImage[] = [];
  @Input() selectedImages: GalleryImage[] = [];
  @Input() showBulkActions = true;
  @Input() allowSelection = true;

  @Output() filterChange = new EventEmitter<string>();
  @Output() viewModeChange = new EventEmitter<'grid' | 'list'>();
  @Output() pageSizeChange = new EventEmitter<number>();
  @Output() selectAll = new EventEmitter<void>();
  @Output() downloadSelected = new EventEmitter<void>();

  onFilterChange(event: Event): void {
    const target = event.target as HTMLSelectElement | null;
    if (!target) {
      return;
    }
    this.filterChange.emit(target.value ?? '');
  }

  setViewMode(mode: 'grid' | 'list'): void {
    this.viewModeChange.emit(mode);
  }

  onPageSizeChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    if (target && target.value) {
      this.pageSizeChange.emit(+target.value);
    }
  }

  onSelectAll(): void {
    this.selectAll.emit();
  }

  onDownloadSelected(): void {
    this.downloadSelected.emit();
  }

  get selectAllText(): string {
    return this.selectedImages.length === this.filteredImages.length
      ? 'Deselect All'
      : 'Select All';
  }

  get shouldShowBulkActions(): boolean {
    return this.showBulkActions && this.filteredImages.length > 1;
  }
}
