import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface BiometricConsentState {
  version: number;
  accepted: boolean;
  acceptedAt: string;
}

const CONSENT_STORAGE_KEY = 'biometric-consent-v1';
const CONSENT_VERSION = 1;

@Injectable({ providedIn: 'root' })
export class BiometricConsentService {
  private readonly consentSubject = new BehaviorSubject<BiometricConsentState | null>(null);
  readonly consent$ = this.consentSubject.asObservable();

  constructor() {
    this.loadConsent();
  }

  getConsent(): BiometricConsentState | null {
    return this.consentSubject.value;
  }

  hasConsent(): boolean {
    return !!this.consentSubject.value?.accepted;
  }

  acceptConsent(): void {
    const state: BiometricConsentState = {
      version: CONSENT_VERSION,
      accepted: true,
      acceptedAt: new Date().toISOString(),
    };
    this.persistConsent(state);
    this.consentSubject.next(state);
  }

  revokeConsent(): void {
    if (!this.canUseStorage()) {
      return;
    }
    window.localStorage.removeItem(CONSENT_STORAGE_KEY);
    this.consentSubject.next(null);
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
      const parsed = JSON.parse(raw) as BiometricConsentState;
      if (parsed.version !== CONSENT_VERSION) {
        this.consentSubject.next(null);
        return;
      }
      this.consentSubject.next(parsed);
    } catch {
      this.consentSubject.next(null);
    }
  }

  private persistConsent(state: BiometricConsentState): void {
    if (!this.canUseStorage()) {
      return;
    }
    window.localStorage.setItem(CONSENT_STORAGE_KEY, JSON.stringify(state));
  }

  private canUseStorage(): boolean {
    return typeof window !== 'undefined' && !!window.localStorage;
  }
}
