import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-out-of-credits-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './out-of-credits-modal.component.html',
  styleUrls: ['./out-of-credits-modal.component.sass'],
})
export class OutOfCreditsModalComponent {
  @Input() isVisible = false;
  @Output() closed = new EventEmitter<void>();
  @Output() upgradeClicked = new EventEmitter<void>();

  onClose(): void {
    this.closed.emit();
  }

  onUpgradeClick(): void {
    this.upgradeClicked.emit();
  }

  onOverlayClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-overlay')) {
      this.onClose();
    }
  }
}
