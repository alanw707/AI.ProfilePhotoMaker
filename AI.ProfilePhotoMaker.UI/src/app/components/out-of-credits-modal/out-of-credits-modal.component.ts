import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-out-of-credits-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './out-of-credits-modal.component.html',
  styleUrls: ['./out-of-credits-modal.component.sass'],
})
export class OutOfCreditsModalComponent implements OnChanges {
  @Input() isVisible = false;
  @Output() closed = new EventEmitter<void>();
  @Output() upgradeClicked = new EventEmitter<void>();
  @ViewChild('modalCard') modalCard?: ElementRef<HTMLElement>;

  private _lastFocusedElement: HTMLElement | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['isVisible']) {
      return;
    }

    if (this.isVisible) {
      this._lastFocusedElement = document.activeElement as HTMLElement | null;
      setTimeout(() => this._focusFirstElement(), 0);
      return;
    }

    this._lastFocusedElement?.focus();
  }

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

  @HostListener('document:keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent): void {
    if (!this.isVisible) {
      return;
    }

    event.preventDefault();
    this.onClose();
  }

  @HostListener('document:keydown.tab', ['$event'])
  onTabKey(event: KeyboardEvent): void {
    if (!this.isVisible || !this.modalCard) {
      return;
    }

    const focusable = this._getFocusableElements();

    if (focusable.length === 0) {
      event.preventDefault();
      this.modalCard.nativeElement.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement as HTMLElement | null;
    const isInsideModal = active ? this.modalCard.nativeElement.contains(active) : false;

    if (event.shiftKey) {
      if (active === first || !isInsideModal) {
        event.preventDefault();
        last.focus();
      }
      return;
    }

    if (active === last || !isInsideModal) {
      event.preventDefault();
      first.focus();
    }
  }

  private _focusFirstElement(): void {
    const focusable = this._getFocusableElements();

    if (focusable.length > 0) {
      focusable[0].focus();
      return;
    }

    this.modalCard?.nativeElement.focus();
  }

  private _getFocusableElements(): HTMLElement[] {
    if (!this.modalCard) {
      return [];
    }

    const selectors = [
      'button:not([disabled])',
      '[href]:not([aria-disabled="true"])',
      'input:not([disabled])',
      'select:not([disabled])',
      'textarea:not([disabled])',
      '[tabindex]:not([tabindex="-1"])',
    ].join(',');

    return Array.from(this.modalCard.nativeElement.querySelectorAll<HTMLElement>(selectors)).filter(
      element => element.offsetParent !== null
    );
  }
}
