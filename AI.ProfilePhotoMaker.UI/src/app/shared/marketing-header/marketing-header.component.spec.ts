import { BehaviorSubject } from 'rxjs';
import { ElementRef } from '@angular/core';
import { Router } from '@angular/router';

import { MarketingHeaderComponent } from './marketing-header.component';
import { AuthService } from '../../services/auth.service';
import { NavigationService } from '../../services/navigation.service';
import { ThemeService } from '../../services/theme.service';
import { IntentTrackingService } from '../../services/intent-tracking.service';

describe('MarketingHeaderComponent register intent behavior', () => {
  const createComponent = () => {
    const isAuthenticated$ = new BehaviorSubject(false);
    const authService = {
      isAuthenticated$,
      logout: jasmine.createSpy('logout'),
    } as unknown as AuthService;

    const router = {
      navigate: jasmine.createSpy('navigate'),
      events: new BehaviorSubject({} as any),
    } as unknown as Router;
    const navigationService = jasmine.createSpyObj<NavigationService>('NavigationService', [
      'navigateToSection',
    ]);
    const cdr = jasmine.createSpyObj('ChangeDetectorRef', ['markForCheck']);
    const elementRef = new ElementRef<HTMLElement>(document.createElement('div'));
    const themeService = {
      theme$: new BehaviorSubject<'light' | 'dark'>('light'),
      toggleTheme: jasmine.createSpy('toggleTheme'),
    } as unknown as ThemeService;
    const intentTracking = jasmine.createSpyObj<IntentTrackingService>('IntentTrackingService', [
      'storeIntent',
    ]);

    const component = new MarketingHeaderComponent(
      authService,
      router,
      navigationService,
      cdr,
      elementRef,
      themeService,
      intentTracking
    );

    return { component, intentTracking };
  };

  it('captures register intent on plain click', () => {
    const { component, intentTracking } = createComponent();

    component.onRegisterLinkClick(new MouseEvent('click', { button: 0 }));

    expect(intentTracking.storeIntent).toHaveBeenCalledTimes(1);
  });

  it('does not capture register intent on modifier click', () => {
    const { component, intentTracking } = createComponent();

    component.onRegisterLinkClick(new MouseEvent('click', { button: 0, ctrlKey: true }));

    expect(intentTracking.storeIntent).not.toHaveBeenCalled();
  });

  it('refreshes register intent timestamp after ttl', () => {
    const { component } = createComponent();
    let now = 1_000_000;
    spyOn(Date, 'now').and.callFake(() => now);

    const firstIntentTimestamp = JSON.parse(component.getRegisterQueryParams()['intent']).timestamp;

    now += 4 * 60 * 1000;
    const beforeTtlTimestamp = JSON.parse(component.getRegisterQueryParams()['intent']).timestamp;

    now += 2 * 60 * 1000;
    const afterTtlTimestamp = JSON.parse(component.getRegisterQueryParams()['intent']).timestamp;

    expect(beforeTtlTimestamp).toBe(firstIntentTimestamp);
    expect(afterTtlTimestamp).toBeGreaterThan(firstIntentTimestamp);
  });
});
