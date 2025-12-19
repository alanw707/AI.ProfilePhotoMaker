import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  NgZone,
  OnDestroy,
  Output,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';

type TurnstileTheme = 'auto' | 'light' | 'dark';

let turnstileScriptPromise: Promise<void> | null = null;

function loadTurnstileScript(): Promise<void> {
  if (turnstileScriptPromise) {
    return turnstileScriptPromise;
  }

  turnstileScriptPromise = new Promise<void>((resolve, reject) => {
    if (typeof window === 'undefined') {
      resolve();
      return;
    }

    if ((window as any).turnstile?.render) {
      resolve();
      return;
    }

    const existing = document.querySelector<HTMLScriptElement>('script[data-turnstile-script]');
    if (existing) {
      existing.addEventListener('load', () => resolve());
      existing.addEventListener('error', () => reject(new Error('Turnstile script failed to load')));
      return;
    }

    const script = document.createElement('script');
    script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
    script.async = true;
    script.defer = true;
    script.dataset['turnstileScript'] = 'true';
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('Turnstile script failed to load'));
    document.head.appendChild(script);
  });

  return turnstileScriptPromise;
}

@Component({
  selector: 'app-turnstile',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './turnstile.component.html',
  styleUrls: ['./turnstile.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TurnstileComponent implements AfterViewInit, OnDestroy {
  @Input({ required: true }) siteKey = '';
  @Input() theme: TurnstileTheme = 'auto';
  @Output() tokenChange = new EventEmitter<string>();

  @ViewChild('container', { static: true }) containerRef!: ElementRef<HTMLDivElement>;

  error = '';
  errorCode = '';
  private _widgetId?: string;

  constructor(
    private _zone: NgZone,
    private _cdr: ChangeDetectorRef
  ) {}

  private getFriendlyErrorMessage(code?: string): string {
    if (!code) {
      return 'Bot protection failed. Please refresh and try again.';
    }

    // See: https://developers.cloudflare.com/turnstile/troubleshooting/client-side-errors/error-codes/
    const base = code.trim();

    if (base === '400020' || base === '110100' || base === '110110') {
      return `Bot protection is unavailable right now (code ${base}). Please refresh and try again.`;
    }

    if (base === '110200') {
      return `Bot protection is not available for this domain (code ${base}). Please refresh and try again.`;
    }

    if (base === '200500') {
      return `Bot protection was blocked by your browser (code ${base}). Disable content blockers and refresh.`;
    }

    if (base.startsWith('11060') || base.startsWith('11062')) {
      return `Bot protection timed out (code ${base}). Please retry.`;
    }

    return `Bot protection failed (code ${base}). Please refresh and try again.`;
  }

  async ngAfterViewInit(): Promise<void> {
    if (!this.siteKey) {
      return;
    }

    try {
      await loadTurnstileScript();

      const turnstile = (window as any).turnstile;
      if (!turnstile?.render) {
        this.error = 'Bot protection failed to load. Please refresh and try again.';
        this._cdr.markForCheck();
        return;
      }

      const options: any = {
        sitekey: this.siteKey,
        theme: this.theme,
        callback: (token: string) => {
          this._zone.run(() => {
            this.error = '';
            this.errorCode = '';
            this.tokenChange.emit(token);
            this._cdr.markForCheck();
          });
        },
      };

      options['expired-callback'] = () => {
        this._zone.run(() => {
          this.tokenChange.emit('');
          this._cdr.markForCheck();
        });
      };
      options['error-callback'] = (code?: string) => {
        this._zone.run(() => {
          this.errorCode = (code ?? '').trim();
          this.error = this.getFriendlyErrorMessage(this.errorCode);
          this.tokenChange.emit('');
          this._cdr.markForCheck();
        });
        return true;
      };

      this._widgetId = turnstile.render(this.containerRef.nativeElement, options);
    } catch {
      this.error = 'Bot protection failed to load. Please refresh and try again.';
      this._cdr.markForCheck();
    }
  }

  reset(): void {
    this.error = '';
    this.errorCode = '';
    this._cdr.markForCheck();

    if (!this._widgetId) {
      return;
    }

    try {
      (window as any).turnstile?.reset?.(this._widgetId);
    } catch {}

    this._zone.run(() => this.tokenChange.emit(''));
  }

  ngOnDestroy(): void {
    if (!this._widgetId) {
      return;
    }

    try {
      (window as any).turnstile?.remove?.(this._widgetId);
    } catch {}
  }
}
