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
  private _widgetId?: string;

  constructor(
    private _zone: NgZone,
    private _cdr: ChangeDetectorRef
  ) {}

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
          this._zone.run(() => this.tokenChange.emit(token));
        },
      };

      options['expired-callback'] = () => {
        this._zone.run(() => this.tokenChange.emit(''));
      };
      options['error-callback'] = () => {
        this._zone.run(() => this.tokenChange.emit(''));
      };

      this._widgetId = turnstile.render(this.containerRef.nativeElement, options);
    } catch {
      this.error = 'Bot protection failed to load. Please refresh and try again.';
      this._cdr.markForCheck();
    }
  }

  reset(): void {
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
