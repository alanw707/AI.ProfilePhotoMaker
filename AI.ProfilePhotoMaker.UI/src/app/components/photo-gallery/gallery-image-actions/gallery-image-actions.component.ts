import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GalleryImage } from '../photo-gallery.component';

@Component({
  selector: 'app-gallery-image-actions',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gallery-image-actions.component.html',
  styleUrls: ['./gallery-image-actions.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GalleryImageActionsComponent {
  @Input() image!: GalleryImage;
  @Input() viewMode: 'grid' | 'list' = 'grid';
  @Input() showViewButton = true;

  @Output() view = new EventEmitter<GalleryImage>();
  @Output() download = new EventEmitter<GalleryImage>();
  @Output() share = new EventEmitter<GalleryImage>();
  @Output() delete = new EventEmitter<GalleryImage>();

  onView(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.view.emit(this.image);
  }

  onDownload(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.download.emit(this.image);
  }

  onShare(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.share.emit(this.image);
  }

  onDelete(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.delete.emit(this.image);
  }

  get isCompleted(): boolean {
    return this.image.status === 'completed';
  }
}