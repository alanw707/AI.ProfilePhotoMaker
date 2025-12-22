import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import {
  CookieConsentService,
  CookieConsentState,
} from '../../../services/cookie-consent.service';

interface CookiePreferences {
  preferences: boolean;
  analytics: boolean;
  marketing: boolean;
}

@Component({
  selector: 'app-cookie-consent',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './cookie-consent.component.html',
  styleUrls: ['./cookie-consent.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CookieConsentComponent implements OnInit, OnDestroy {
  shouldShowBanner = false;
  showPreferences = false;
  preferences: CookiePreferences = { preferences: false, analytics: false, marketing: false };

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly cookieConsentService: CookieConsentService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.cookieConsentService.consent$.pipe(takeUntil(this.destroy$)).subscribe(consent => {
      this.shouldShowBanner = !consent;
      this.syncPreferences(consent);
      this.cdr.markForCheck();
    });

    this.cookieConsentService.preferencesOpen$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.openPreferences());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  openPreferences(): void {
    this.showPreferences = true;
    this.cdr.markForCheck();
  }

  closePreferences(): void {
    this.showPreferences = false;
    this.cdr.markForCheck();
  }

  acceptAll(): void {
    this.cookieConsentService.setConsent({
      preferences: true,
      analytics: true,
      marketing: true,
    });
    this.showPreferences = false;
    this.cdr.markForCheck();
  }

  rejectNonEssential(): void {
    this.cookieConsentService.setConsent({
      preferences: false,
      analytics: false,
      marketing: false,
    });
    this.showPreferences = false;
    this.cdr.markForCheck();
  }

  savePreferences(): void {
    this.cookieConsentService.setConsent({
      preferences: this.preferences.preferences,
      analytics: this.preferences.analytics,
      marketing: this.preferences.marketing,
    });
    this.showPreferences = false;
    this.cdr.markForCheck();
  }

  private syncPreferences(consent: CookieConsentState | null): void {
    if (!consent) {
      this.preferences = { preferences: false, analytics: false, marketing: false };
      return;
    }
    this.preferences = {
      preferences: consent.preferences,
      analytics: consent.analytics,
      marketing: consent.marketing,
    };
  }
}
