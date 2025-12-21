import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';

export type CookieCategory = 'essential' | 'preferences' | 'analytics' | 'marketing';

export interface CookieConsentState {
  version: number;
  updatedAt: string;
  preferences: boolean;
  analytics: boolean;
  marketing: boolean;
}

const CONSENT_STORAGE_KEY = 'cookie-consent-v1';
const CONSENT_VERSION = 1;

@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  private readonly consentSubject = new BehaviorSubject<CookieConsentState | null>(null);
  readonly consent$ = this.consentSubject.asObservable();
  private readonly preferencesOpenSubject = new Subject<void>();
  readonly preferencesOpen$ = this.preferencesOpenSubject.asObservable();

  constructor() {
    this.loadConsent();
  }

  getConsent(): CookieConsentState | null {
    return this.consentSubject.value;
  }

  hasConsent(): boolean {
    return !!this.consentSubject.value;
  }

  isCategoryAllowed(category: CookieCategory): boolean {
    if (category === 'essential') {
      return true;
    }
    const consent = this.consentSubject.value;
    return !!consent?.[category];
  }

  setConsent(selection: Pick<CookieConsentState, 'preferences' | 'analytics' | 'marketing'>): void {
    const state: CookieConsentState = {
      version: CONSENT_VERSION,
      updatedAt: new Date().toISOString(),
      preferences: selection.preferences,
      analytics: selection.analytics,
      marketing: selection.marketing,
    };
    this.persistConsent(state);
    this.consentSubject.next(state);
  }

  clearConsent(): void {
    if (!this.canUseStorage()) {
      return;
    }
    window.localStorage.removeItem(CONSENT_STORAGE_KEY);
    this.consentSubject.next(null);
  }

  requestPreferencesOpen(): void {
    this.preferencesOpenSubject.next();
  }

  private loadConsent(): void {
    if (!this.canUseStorage()) {
      return;
    }
    const raw = window.localStorage.getItem(CONSENT_STORAGE_KEY);
    if (!raw) {
      this.consentSubject.next(null);
      return;
    }

    try {
      const parsed = JSON.parse(raw) as CookieConsentState;
      if (parsed.version !== CONSENT_VERSION) {
        this.consentSubject.next(null);
        return;
      }
      this.consentSubject.next(parsed);
    } catch {
      this.consentSubject.next(null);
    }
  }

  private persistConsent(state: CookieConsentState): void {
    if (!this.canUseStorage()) {
      return;
    }
    window.localStorage.setItem(CONSENT_STORAGE_KEY, JSON.stringify(state));
  }

  private canUseStorage(): boolean {
    return typeof window !== 'undefined' && !!window.localStorage;
  }
}
