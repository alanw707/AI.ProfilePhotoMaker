import { Injectable } from '@angular/core';

export type SignupCtaType = 'headshots' | 'enhance' | 'pricing';

export interface SignupIntent {
  sourcePage: string;
  ctaType: SignupCtaType;
  timestamp: number;
}

@Injectable({
  providedIn: 'root',
})
export class IntentTrackingService {
  private static readonly STORAGE_KEY = 'signupIntent';
  private static readonly EXPIRY_MS = 7 * 24 * 60 * 60 * 1000;

  storeIntent(intent: SignupIntent): void {
    if (!this.isValidIntent(intent)) {
      return;
    }

    try {
      sessionStorage.setItem(IntentTrackingService.STORAGE_KEY, JSON.stringify(intent));
    } catch {
      // Ignore storage failures and keep flow uninterrupted.
    }
  }

  getIntent(): SignupIntent | null {
    const rawValue = sessionStorage.getItem(IntentTrackingService.STORAGE_KEY);
    if (!rawValue) {
      return null;
    }

    try {
      const parsed = JSON.parse(rawValue) as SignupIntent;
      if (!this.isValidIntent(parsed)) {
        this.clearIntent();
        return null;
      }

      if (Date.now() - parsed.timestamp > IntentTrackingService.EXPIRY_MS) {
        this.clearIntent();
        return null;
      }

      return parsed;
    } catch {
      this.clearIntent();
      return null;
    }
  }

  clearIntent(): void {
    sessionStorage.removeItem(IntentTrackingService.STORAGE_KEY);
  }

  isValidIntent(intent: unknown): intent is SignupIntent {
    if (!intent || typeof intent !== 'object') {
      return false;
    }

    const candidate = intent as Partial<SignupIntent>;
    const validType =
      candidate.ctaType === 'headshots' ||
      candidate.ctaType === 'enhance' ||
      candidate.ctaType === 'pricing';

    return (
      typeof candidate.sourcePage === 'string' &&
      candidate.sourcePage.length > 0 &&
      validType &&
      typeof candidate.timestamp === 'number' &&
      Number.isFinite(candidate.timestamp)
    );
  }

  hasHeadshotsIntent(): boolean {
    return this.getIntent()?.ctaType === 'headshots';
  }
}
