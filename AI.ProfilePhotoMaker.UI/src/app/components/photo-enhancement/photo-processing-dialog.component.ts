import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, Input, OnDestroy, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-photo-processing-dialog',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog aria-labelledby="photo-processing-title" aria-describedby="photo-processing-help"
      (cancel)="hide($event)" (keydown)="keepFocus($event)">
      <span class="spinner" aria-hidden="true"></span>
      <h2 id="photo-processing-title">{{ title }}</h2>
      <p role="status" aria-live="polite" aria-atomic="true">{{ status || 'Your request is being processed…' }}</p>
      <p id="photo-processing-help">You can hide this window while work continues. If the connection drops,
        return to this workspace and resume the saved request if shown. Otherwise contact support before trying another edit.</p>
      <button type="button" (click)="hide()">Hide progress — keep working</button>
    </dialog>
    <button #reopen *ngIf="hidden" class="reopen" type="button" (click)="show()">
      {{ title }} · Show progress
    </button>
  `,
  styleUrl: './photo-processing-dialog.component.css',
})
export class PhotoProcessingDialogComponent implements AfterViewInit, OnDestroy {
  @Input() title = 'Processing your photo';
  @Input() status = '';
  @ViewChild('dialog', { static: true }) dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('reopen') reopen?: ElementRef<HTMLButtonElement>;
  hidden = false;
  private previousFocus: HTMLElement | null = null;
  private workspace: Element | null = null;

  constructor(private host: ElementRef<HTMLElement>, private cdr: ChangeDetectorRef) {}

  ngAfterViewInit(): void {
    this.workspace = this.host.nativeElement.closest('app-photo-enhancement');
    const focused = document.activeElement;
    this.previousFocus = focused instanceof HTMLElement && this.workspace?.contains(focused) ? focused : null;
    this.show();
  }

  show(): void {
    this.hidden = false;
    if (!this.dialog.nativeElement.open) {
      this.dialog.nativeElement.showModal();
    }
  }

  keepFocus(event: KeyboardEvent): void {
    // The modal has one action; cycle both Tab directions through that control.
    if (event.key === 'Tab') {
      event.preventDefault();
      this.dialog.nativeElement.querySelector('button')?.focus();
    }
  }

  hide(event?: Event): void {
    event?.preventDefault();
    this.dialog.nativeElement.close();
    this.hidden = true;
    this.cdr.detectChanges();
    this.reopen?.nativeElement.focus();
  }

  ngOnDestroy(): void {
    this.dialog.nativeElement.close();
    const workspace = this.workspace;
    requestAnimationFrame(() => {
      if (!workspace?.isConnected) {
        return;
      }
      const target = workspace.querySelector<HTMLElement>('.error-section h2') ??
        workspace.querySelector<HTMLElement>('.proof-results-heading h2') ??
        (this.previousFocus?.isConnected && !this.previousFocus.matches(':disabled') ? this.previousFocus :
          workspace.querySelector<HTMLElement>('.file-preview-section h2'));
      if (target?.matches('h2')) {
        target.setAttribute('tabindex', '-1');
      }
      target?.focus();
    });
  }
}
